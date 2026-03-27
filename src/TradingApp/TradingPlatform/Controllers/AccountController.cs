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
    public class AccountController : ControllerBase
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IEventBus _eventBus;

        public AccountController(IAccountRepository accountRepository, IEventBus eventBus)
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

            await Response.WriteAsJsonAsync(account, cancellationToken);
            await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<AccountUpdateEvent>();
            var handler = new ChannelEventHandler(channel.Writer);
            
            _eventBus.Subscribe(handler);

            try
            {
                await foreach (var accountUpdate in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (string.Equals(accountUpdate.Username, username, StringComparison.OrdinalIgnoreCase))
                    {
                        await Response.WriteAsJsonAsync(accountUpdate, cancellationToken);
                        await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
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
