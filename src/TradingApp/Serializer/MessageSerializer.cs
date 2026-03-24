namespace Serializer
{
    public class MessageSerializer : IMessageSerializer
    {
        private readonly int _lengthPrefixBytes;

        public MessageSerializer(int lengthPrefixBytes)
        {
            if (lengthPrefixBytes != 2 && lengthPrefixBytes != 4 && lengthPrefixBytes != 8)
                throw new ArgumentException("Length prefix bytes must be 2, 4, or 8.");
            
            _lengthPrefixBytes = lengthPrefixBytes;
        }

        public byte[] Serialize<T>(T message)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(message);
            var payload = System.Text.Encoding.UTF8.GetBytes(json);

            // Adding framing
            byte[] lengthPrefix = _lengthPrefixBytes switch
            {
                2 => BitConverter.GetBytes((short)payload.Length),
                4 => BitConverter.GetBytes(payload.Length),
                8 => BitConverter.GetBytes((long)payload.Length),
                _ => throw new InvalidOperationException()
            };

            var framedMessage = new byte[lengthPrefix.Length + payload.Length];

            Buffer.BlockCopy(lengthPrefix, 0, framedMessage, 0, lengthPrefix.Length);
            Buffer.BlockCopy(payload, 0, framedMessage, lengthPrefix.Length, payload.Length);

            return framedMessage;
        }

        public T Deserialize<T>(Stream stream)
        {
            var lengthBuffer = new byte[_lengthPrefixBytes];
            stream.ReadExactly(lengthBuffer);

            int payloadLength = _lengthPrefixBytes switch
            {
                2 => BitConverter.ToInt16(lengthBuffer, 0),
                4 => BitConverter.ToInt32(lengthBuffer, 0),
                8 => (int)BitConverter.ToInt64(lengthBuffer, 0),
                _ => throw new InvalidOperationException()
            };

            var payloadBuffer = new byte[payloadLength];
            stream.ReadExactly(payloadBuffer);

            var json = System.Text.Encoding.UTF8.GetString(payloadBuffer);
            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<T>(json);
                if (result == null)
                {
                    throw new InvalidOperationException("Deserialized message is null");
                }

                return result;
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize message", ex);
            }
        }

        public async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            var lengthBuffer = new byte[_lengthPrefixBytes];
            await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);

            int payloadLength = _lengthPrefixBytes switch
            {
                2 => BitConverter.ToInt16(lengthBuffer, 0),
                4 => BitConverter.ToInt32(lengthBuffer, 0),
                8 => (int)BitConverter.ToInt64(lengthBuffer, 0),
                _ => throw new InvalidOperationException()
            };

            var payloadBuffer = new byte[payloadLength];
            await stream.ReadExactlyAsync(payloadBuffer, cancellationToken);

            var json = System.Text.Encoding.UTF8.GetString(payloadBuffer);
            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<T>(json);
                if (result == null)
                {
                    throw new InvalidOperationException("Deserialized message is null");
                }

                return result;
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize message", ex);
            }
        }
    }
}
