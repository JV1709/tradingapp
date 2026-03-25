using Microsoft.AspNetCore.Mvc;
using Model.Domain;
using System.Collections.Concurrent;

namespace TradingPlatformAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, Account> Accounts = new(StringComparer.OrdinalIgnoreCase);

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

            if (!Accounts.TryAdd(account.Username, account))
            {
                return Conflict($"Account '{account.Username}' already exists.");
            }

            return Created($"/api/account/{account.Username}", account);
        }

        [HttpGet("stream/{username}")]
        public async Task<IActionResult> StreamAccount(string username, CancellationToken cancellationToken)
        {
            if (!Accounts.TryGetValue(username, out var account))
            {
                return NotFound();
            }

            Response.ContentType = "text/event-stream";
            var random = new Random();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var accountUpdate = new Account
                    {
                        Username = account.Username,
                        AvailableBalance = account.AvailableBalance + random.Next(-10, 10),
                        TotalBalance = account.TotalBalance,
                        Holdings = account.Holdings
                    };

                    Accounts[username] = accountUpdate;
                    await Response.WriteAsJsonAsync(accountUpdate, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    await Task.Delay(3000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }

            return new EmptyResult();
        }
    }
}
