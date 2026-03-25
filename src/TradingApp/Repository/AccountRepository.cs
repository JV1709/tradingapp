using Model.Domain;
using System.Collections.Concurrent;

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

        public bool TryAdd(Account account)
        {
            ArgumentNullException.ThrowIfNull(account);
            return _accounts.TryAdd(account.Username, account);
        }

        public void AddOrUpdate(Account account)
        {
            ArgumentNullException.ThrowIfNull(account);
            _accounts.AddOrUpdate(account.Username, account, (_, _) => account);
        }

        public bool TryGet(string username, out Account account)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            return _accounts.TryGetValue(username, out account!);
        }

        public IReadOnlyCollection<Account> GetAll()
        {
            return _accounts.Values.ToArray();
        }

        public bool Remove(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            return _accounts.TryRemove(username, out _);
        }
    }
}
