using Infrastructure.Event;
using Infrastructure.Queue;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model.Config;
using Model.Domain;
using Model.Event;
using Model.Request;
using Moq;
using Repository;
using System.Net.WebSockets;
using System.Text.Json;

namespace OrderGateway.Tests
{
    public class OrderGatewayServiceTests
    {
        // ─── helpers ───────────────────────────────────────────────────────────

        private static OrderGatewayService BuildService(
            IPartitionedMPSCQueueSystem<GatewayRequest> queueSystem,
            IOrderRepository? orderRepo = null,
            IEventBus? eventBus = null)
        {
            var config = Options.Create(new ParallelismConfig { PartitionCount = 1 });
            return new OrderGatewayService(
                NullLogger<OrderGatewayService>.Instance,
                queueSystem,
                orderRepo ?? Mock.Of<IOrderRepository>(r => r.GetByAccountKey(It.IsAny<string>()) == new List<Order>()),
                eventBus ?? Mock.Of<IEventBus>(),
                config);
        }

        private static string GetPartitionKey(string account) => (Math.Abs(account.GetHashCode()) % 1).ToString();

        private static PartitionedMPSCQueueSystem<GatewayRequest> QueueWithKey(string key) =>
            new PartitionedMPSCQueueSystem<GatewayRequest>(new[] { GetPartitionKey(key) });

        private static Order MakeOrder(string accountKey) => new Order
        {
            OrderId = Guid.NewGuid(),
            AccountKey = accountKey,
            Symbol = "AAPL",
            TotalQuantity = 10,
            Price = 150m,
            Side = Side.Buy,
            Status = OrderStatus.New
        };

        // ─── session routing ────────────────────────────────────────────────────

        [Fact]
        public async Task ProcessWebSocketSession_ClosesSocket_WhenAccountKeyNotInQueue()
        {
            var queueSystem = new PartitionedMPSCQueueSystem<GatewayRequest>(new[] { "other-account" });
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            await service.ProcessWebSocketSessionAsync(ws, "unknown-account", CancellationToken.None);

            Assert.Equal(WebSocketState.Closed, ws.State);
        }

        // ─── read loop (inbound orders) ─────────────────────────────────────────

        [Fact]
        public async Task ProcessWebSocketSession_EnqueuesPlaceOrder_WhenValidMessageReceived()
        {
            const string account = "acc1";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            var request = new PlaceOrderRequest
            {
                AccountKey = account,
                Symbol = "AAPL",
                Quantity = 5,
                Price = 100m,
                Side = Side.Buy
            };

            ws.EnqueueInbound(JsonSerializer.Serialize(request));
            ws.SimulateClientClose();

            await service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);

            queueSystem.TryGetQueue(GetPartitionKey(account), out var queue);
            Assert.True(queue!.TryDequeue(out var item));
            Assert.Equal(GatewayRequestType.PlaceOrder, item.Type);
            Assert.Equal(account, item.PlaceOrderRequest!.AccountKey);
            Assert.Equal("AAPL", item.PlaceOrderRequest.Symbol);
        }

        [Fact]
        public async Task ProcessWebSocketSession_EnqueuesCancelOrder_WhenCancelMessageReceived()
        {
            const string account = "acc2";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            var cancelRequest = new CancelOrderRequest { OrderId = Guid.NewGuid() };

            ws.EnqueueInbound(JsonSerializer.Serialize(cancelRequest));
            ws.SimulateClientClose();

            await service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);

            queueSystem.TryGetQueue(GetPartitionKey(account), out var queue);
            Assert.True(queue!.TryDequeue(out var item));
            Assert.Equal(GatewayRequestType.CancelOrder, item.Type);
            Assert.Equal(cancelRequest.OrderId, item.CancelOrderRequest!.OrderId);
        }

        [Fact]
        public async Task ProcessWebSocketSession_EnqueuesMultipleMessages_InOrder()
        {
            const string account = "acc-multi";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            var r1 = new PlaceOrderRequest { AccountKey = account, Symbol = "AAPL", Quantity = 1, Price = 100m, Side = Side.Buy };
            var r2 = new PlaceOrderRequest { AccountKey = account, Symbol = "GOOG", Quantity = 2, Price = 200m, Side = Side.Sell };

            ws.EnqueueInbound(JsonSerializer.Serialize(r1));
            ws.EnqueueInbound(JsonSerializer.Serialize(r2));
            ws.SimulateClientClose();

            await service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);

            queueSystem.TryGetQueue(GetPartitionKey(account), out var queue);
            Assert.True(queue!.TryDequeue(out var item1));
            Assert.Equal("AAPL", item1.PlaceOrderRequest!.Symbol);
            Assert.True(queue.TryDequeue(out var item2));
            Assert.Equal("GOOG", item2.PlaceOrderRequest!.Symbol);
        }

        [Fact]
        public async Task ProcessWebSocketSession_DoesNotEnqueue_WhenPlaceOrderAccountKeyMismatch()
        {
            const string account = "acc3";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            var request = new PlaceOrderRequest
            {
                AccountKey = "different-account",
                Symbol = "GOOG",
                Quantity = 1,
                Price = 200m,
                Side = Side.Sell
            };

            ws.EnqueueInbound(JsonSerializer.Serialize(request));
            ws.SimulateClientClose();

            await service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);

            queueSystem.TryGetQueue(GetPartitionKey(account), out var queue);
            Assert.False(queue!.TryDequeue(out _));
        }

        [Fact]
        public async Task ProcessWebSocketSession_DoesNotCrash_WhenInvalidJsonReceived()
        {
            const string account = "acc4";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            ws.EnqueueInbound("not valid json {{{{");
            ws.SimulateClientClose();

            var ex = await Record.ExceptionAsync(() =>
                service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None));

            Assert.Null(ex);
        }

        [Fact]
        public async Task ProcessWebSocketSession_DoesNotCrash_WhenUnrecognizedJsonShapeReceived()
        {
            const string account = "acc5";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            ws.EnqueueInbound("{\"foo\": \"bar\"}");
            ws.SimulateClientClose();

            var ex = await Record.ExceptionAsync(() =>
                service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None));

            Assert.Null(ex);
        }

        // ─── write loop (outbound snapshots + event bus) ────────────────────────

        [Fact]
        public async Task ProcessWebSocketSession_SendsSnapshotOrders_OnConnect()
        {
            const string account = "snap1";
            var queueSystem = QueueWithKey(account);

            var existingOrder = MakeOrder(account);
            var repoMock = new Mock<IOrderRepository>();
            repoMock.Setup(r => r.GetByAccountKey(account))
                    .Returns(new List<Order> { existingOrder });

            var service = BuildService(queueSystem, repoMock.Object);
            var ws = new FakeWebSocket();

            // Start the session; wait until the snapshot message arrives, THEN close.
            var sessionTask = service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);
            await ws.WaitForNSentAsync(1, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            ws.SimulateClientClose();
            await sessionTask;

            Assert.Single(ws.SentMessages);
            var sent = JsonSerializer.Deserialize<OrderUpdateEvent>(ws.SentMessages[0]);
            Assert.NotNull(sent);
            Assert.Equal(existingOrder.OrderId, sent!.Order.OrderId);
            Assert.Equal("snapshot", sent.Remark);
        }

        [Fact]
        public async Task ProcessWebSocketSession_SendsAllSnapshotOrders_WhenMultipleExist()
        {
            const string account = "snap2";
            var queueSystem = QueueWithKey(account);

            var orders = new List<Order> { MakeOrder(account), MakeOrder(account), MakeOrder(account) };
            var repoMock = new Mock<IOrderRepository>();
            repoMock.Setup(r => r.GetByAccountKey(account)).Returns(orders);

            var service = BuildService(queueSystem, repoMock.Object);
            var ws = new FakeWebSocket();

            var sessionTask = service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);
            await ws.WaitForNSentAsync(3, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            ws.SimulateClientClose();
            await sessionTask;

            Assert.Equal(3, ws.SentMessages.Count);
            var sentIds = ws.SentMessages
                .Select(m => JsonSerializer.Deserialize<OrderUpdateEvent>(m)!.Order.OrderId)
                .ToHashSet();
            Assert.True(orders.All(o => sentIds.Contains(o.OrderId)));
        }

        [Fact]
        public async Task ProcessWebSocketSession_PushesEventBusUpdate_ToWebSocket()
        {
            const string account = "evt1";
            var queueSystem = QueueWithKey(account);

            var repoMock = new Mock<IOrderRepository>();
            repoMock.Setup(r => r.GetByAccountKey(account)).Returns(new List<Order>());

            IEventHandler<OrderUpdateEvent>? capturedHandler = null;
            var busMock = new Mock<IEventBus>();
            busMock.Setup(b => b.Subscribe(It.IsAny<IEventHandler<OrderUpdateEvent>>()))
                   .Callback<IEventHandler<OrderUpdateEvent>>(h => capturedHandler = h);

            var service = BuildService(queueSystem, repoMock.Object, busMock.Object);
            var ws = new FakeWebSocket();

            var sessionTask = service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);

            // Spin-wait for the event handler to be registered (happens synchronously at start of write loop)
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (capturedHandler == null && DateTime.UtcNow < deadline)
                await Task.Delay(5);
            Assert.NotNull(capturedHandler);

            var order = MakeOrder(account);
            var update = new OrderUpdateEvent { Order = order, Remark = "filled" };
            await capturedHandler!.HandleAsync(update);

            // Wait for that message to be sent, then close
            await ws.WaitForNSentAsync(1, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            ws.SimulateClientClose();
            await sessionTask;

            Assert.Contains(ws.SentMessages, m =>
            {
                var e = JsonSerializer.Deserialize<OrderUpdateEvent>(m);
                return e?.Order.OrderId == order.OrderId && e.Remark == "filled";
            });
        }

        [Fact]
        public async Task ProcessWebSocketSession_FiltersEventBusUpdates_ForOtherAccounts()
        {
            const string account = "evt2";
            var queueSystem = QueueWithKey(account);

            var repoMock = new Mock<IOrderRepository>();
            repoMock.Setup(r => r.GetByAccountKey(account)).Returns(new List<Order>());

            IEventHandler<OrderUpdateEvent>? capturedHandler = null;
            var busMock = new Mock<IEventBus>();
            busMock.Setup(b => b.Subscribe(It.IsAny<IEventHandler<OrderUpdateEvent>>()))
                   .Callback<IEventHandler<OrderUpdateEvent>>(h => capturedHandler = h);

            var service = BuildService(queueSystem, repoMock.Object, busMock.Object);
            var ws = new FakeWebSocket();

            var sessionTask = service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (capturedHandler == null && DateTime.UtcNow < deadline)
                await Task.Delay(5);

            // Publish an event for a *different* account — should be silently dropped
            var orderOther = MakeOrder("other-account");
            await capturedHandler!.HandleAsync(new OrderUpdateEvent { Order = orderOther, Remark = "filled" });

            // Give the write loop a brief moment to process (it will skip it), then close
            await Task.Delay(30);
            ws.SimulateClientClose();
            await sessionTask;

            Assert.Empty(ws.SentMessages);
        }

        [Fact]
        public async Task ProcessWebSocketSession_UnsubscribesFromEventBus_OnSessionEnd()
        {
            const string account = "unsub1";
            var queueSystem = QueueWithKey(account);

            var repoMock = new Mock<IOrderRepository>();
            repoMock.Setup(r => r.GetByAccountKey(account)).Returns(new List<Order>());

            var busMock = new Mock<IEventBus>();
            var service = BuildService(queueSystem, repoMock.Object, busMock.Object);
            var ws = new FakeWebSocket();

            var sessionTask = service.ProcessWebSocketSessionAsync(ws, account, CancellationToken.None);
            ws.SimulateClientClose();
            await sessionTask;

            busMock.Verify(b => b.Unsubscribe(It.IsAny<IEventHandler<OrderUpdateEvent>>()), Times.Once);
        }

        [Fact]
        public async Task ProcessWebSocketSession_CancellationToken_EndsSession()
        {
            const string account = "cancel1";
            var queueSystem = QueueWithKey(account);
            var service = BuildService(queueSystem);
            var ws = new FakeWebSocket();

            using var cts = new CancellationTokenSource(millisecondsDelay: 100);

            var ex = await Record.ExceptionAsync(() =>
                service.ProcessWebSocketSessionAsync(ws, account, cts.Token));

            Assert.Null(ex);
        }
    }
}
