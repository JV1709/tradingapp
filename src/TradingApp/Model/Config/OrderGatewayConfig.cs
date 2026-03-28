namespace Model.Config
{
    public sealed record OrderGatewayConfig
    {
        public int Port { get; set; }
        public int SerializerLengthPrefixBytes { get; set; }
    }
}
