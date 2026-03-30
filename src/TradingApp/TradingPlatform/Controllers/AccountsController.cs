using Infrastructure.Event;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Config;
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
        private readonly MarketConfig _config;

        public AccountsController(IAccountRepository accountRepository, IEventBus eventBus, IOptions<MarketConfig> config)
        {
            _accountRepository = accountRepository;
            _eventBus = eventBus;
            _config = config.Value;
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

            var holdings = _config.Instruments.Select(instr => new Holding
            {
                Symbol = instr.Symbol,
                TotalQuantity = 100,
                AvailableQuantity = 100
            }).ToList();

            var account = new Account
            {
                Username = request.Username,
                TotalBalance = request.InitialBalance,
                AvailableBalance = request.InitialBalance,
                Holdings = holdings
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
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var initialJson = System.Text.Json.JsonSerializer.Serialize(account);
            await Response.WriteAsync($"data: {initialJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<AccountUpdateEvent>();
            var handler = new ChannelEventHandler(channel.Writer);
            
            _eventBus.Subscribe(handler);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!await channel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        break;
                    }

                    while (channel.Reader.TryRead(out var updateEvent))
                    {
                        if (!string.Equals(updateEvent.Username, username, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var json = System.Text.Json.JsonSerializer.Serialize(updateEvent);
                        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
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
