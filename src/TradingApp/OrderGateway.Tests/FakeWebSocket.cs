using System.Net.WebSockets;
using System.Text;

namespace OrderGateway.Tests
{
    /// <summary>
    /// A fake WebSocket that lets tests inject messages to be "received" by the SUT
    /// and capture messages "sent" by the SUT back to the client.
    ///
    /// Lifecycle contract:
    ///   1. Pre-queue inbound messages with EnqueueInbound().
    ///   2. Call SimulateClientClose() to end the read loop once all inbound messages
    ///      have been consumed.  SimulateClientClose() does NOT close the socket until
    ///      all previously-enqueued inbound items have been dequeued, so the read loop
    ///      always finishes processing before it sees the close frame.
    ///   3. WaitForNSentAsync(n) can be awaited before closing so that write-loop tests
    ///      can assert on outbound messages without relying on Task.Delay.
    /// </summary>
    internal sealed class FakeWebSocket : WebSocket
    {
        // ── inbound side (server ← client) ──────────────────────────────────────
        private readonly Queue<string> _inbound = new();
        private bool _closeEnqueued;
        private readonly SemaphoreSlim _receiveSemaphore = new(0);

        // ── outbound side (server → client) ─────────────────────────────────────
        private readonly List<string> _sent = new();
        private readonly object _sentLock = new();
        // Listeners waiting for a minimum number of sent messages
        private readonly List<(int count, TaskCompletionSource tcs)> _sentWaiters = new();

        // ── state ────────────────────────────────────────────────────────────────
        private WebSocketState _state = WebSocketState.Open;

        // ── public API ───────────────────────────────────────────────────────────

        public IReadOnlyList<string> SentMessages
        {
            get { lock (_sentLock) return _sent.ToList(); }
        }

        /// <summary>Enqueue a text message for the server to receive.</summary>
        public void EnqueueInbound(string json)
        {
            lock (_sent) // reuse any available lock; nothing sensitive here
                _inbound.Enqueue(json);
            _receiveSemaphore.Release();
        }

        /// <summary>
        /// Signal that the client is closing.  ReceiveAsync will return a Close
        /// frame AFTER all previously-enqueued messages have been dequeued.
        /// </summary>
        public void SimulateClientClose()
        {
            _closeEnqueued = true;
            _receiveSemaphore.Release();
        }

        /// <summary>
        /// Returns a Task that completes once at least <paramref name="n"/> messages
        /// have been sent by the server, useful to avoid timing hacks in tests.
        /// </summary>
        public Task WaitForNSentAsync(int n, CancellationToken ct = default)
        {
            lock (_sentLock)
            {
                if (_sent.Count >= n) return Task.CompletedTask;
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                ct.Register(() => tcs.TrySetCanceled());
                _sentWaiters.Add((n, tcs));
                return tcs.Task;
            }
        }

        // ── WebSocket overrides ──────────────────────────────────────────────────

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() => _state = WebSocketState.Aborted;
        public override void Dispose() { }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await _receiveSemaphore.WaitAsync(cancellationToken);

            // If there is still an inbound message waiting, serve it
            if (_inbound.TryDequeue(out var json))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                int count = Math.Min(bytes.Length, buffer.Count);
                bytes.AsSpan(0, count).CopyTo(buffer.AsSpan());
                return new WebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage: true);
            }

            // Otherwise it must be a close signal
            _state = WebSocketState.CloseReceived;
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                WebSocketCloseStatus.NormalClosure, string.Empty);
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            if (messageType == WebSocketMessageType.Text)
            {
                lock (_sentLock)
                {
                    _sent.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
                    int count = _sent.Count;
                    foreach (var (threshold, tcs) in _sentWaiters.Where(w => count >= w.count))
                        tcs.TrySetResult();
                    _sentWaiters.RemoveAll(w => count >= w.count);
                }
            }
            return Task.CompletedTask;
        }
    }
}
