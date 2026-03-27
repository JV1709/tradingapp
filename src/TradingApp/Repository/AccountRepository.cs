using Model.Domain;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

namespace Repository
{
    public interface IAccountRepository
    {
        bool TryAdd(Account account);
        void AddOrUpdate(Account account);
        bool TryGet(string username, out Account account);
        IReadOnlyCollection<Account> GetAll();
        bool Remove(string username);
    }

    public class AccountRepository : IAccountRepository
    {
        private readonly ConcurrentDictionary<string, Account> _accounts = new(StringComparer.OrdinalIgnoreCase);

        private static Account Clone(Account account) => JsonSerializer.Deserialize<Account>(JsonSerializer.Serialize(account))!;

        public bool TryAdd(Account account)
        {
            ArgumentNullException.ThrowIfNull(account);
            return _accounts.TryAdd(account.Username, Clone(account));
        }

        public void AddOrUpdate(Account account)
        {
            ArgumentNullException.ThrowIfNull(account);
            _accounts.AddOrUpdate(account.Username, Clone(account), (_, _) => Clone(account));
        }

        public bool TryGet(string username, out Account account)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            if (_accounts.TryGetValue(username, out var storedAccount))
            {
                account = Clone(storedAccount);
                return true;
            }
            account = null!;
            return false;
        }

        public IReadOnlyCollection<Account> GetAll()
        {
            return _accounts.Values.Select(Clone).ToArray();
        }

        public bool Remove(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            return _accounts.TryRemove(username, out _);
        }
    }
}
