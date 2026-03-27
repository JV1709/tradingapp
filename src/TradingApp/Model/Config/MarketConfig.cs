using Model.Domain;

namespace Model.Config
{
    public sealed record MarketConfig
    {
        public List<Instrument> Instruments { get; set; } = new();
    }
}
