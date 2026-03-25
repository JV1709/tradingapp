namespace Serializer
{
    public interface IMessageSerializer
    {
        byte[] Serialize<T>(T message);
        T Deserialize<T>(Stream stream);
        Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);
    }
}
