using Infrastructure.Event;
using Microsoft.AspNetCore.Mvc;
using Model.Domain;
using Model.Event;
using Repository;
using System.Threading.Channels;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IEventBus _eventBus;

        public AccountsController(IAccountRepository accountRepository, IEventBus eventBus)
        {
            _accountRepository = accountRepository;
            _eventBus = eventBus;
        }

        private class ChannelEventHandler : IEventHandler<AccountUpdateEvent>
        {
            private readonly ChannelWriter<AccountUpdateEvent> _writer;

            public ChannelEventHandler(ChannelWriter<AccountUpdateEvent> writer)
            {
                _writer = writer;
            }

            public async Task HandleAsync(AccountUpdateEvent @event, CancellationToken cancellationToken = default)
            {
                await _writer.WriteAsync(@event, cancellationToken);
            }
        }

        [HttpPost]
        public IActionResult CreateAccount([FromBody] CreateAccountRequest? request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest("Username is required.");
            }

            if (request.InitialBalance < 0)
            {
                return BadRequest("InitialBalance must be non-negative.");
            }

            var account = new Account
            {
                Username = request.Username,
                TotalBalance = request.InitialBalance,
                AvailableBalance = request.InitialBalance,
                Holdings = new List<Holding>()
            };

            if (!_accountRepository.TryAdd(account))
            {
                return Conflict($"Account '{account.Username}' already exists.");
            }

            return Created($"/api/account/{account.Username}", account);
        }

        [HttpGet("stream/{username}")]
        public async Task<IActionResult> StreamAccount(string username, CancellationToken cancellationToken)
        {
            if (!_accountRepository.TryGet(username, out var account))
            {
                return NotFound();
            }

            Response.ContentType = "text/event-stream";

            var initialJson = System.Text.Json.JsonSerializer.Serialize(account);
            await Response.WriteAsync(initialJson + '\n', cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<AccountUpdateEvent>();
            var handler = new ChannelEventHandler(channel.Writer);
            
            _eventBus.Subscribe(handler);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (channel.Reader.TryRead(out var updateEvent) && string.Equals(updateEvent.Username, username, StringComparison.OrdinalIgnoreCase))
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(updateEvent);
                        await Response.WriteAsync(json + '\n', cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _eventBus.Unsubscribe(handler);
            }

            return new EmptyResult();
        }
    }
}
