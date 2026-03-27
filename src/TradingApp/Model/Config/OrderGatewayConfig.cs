namespace Model.Config
{
    public sealed record OrderGatewayConfig
    {
        public const string SectionName = "Gateway";

        public int Port { get; set; }
        public int QueueCapacity { get; set; }
        public int SerializerLengthPrefixBytes { get; set; }
    }
}
