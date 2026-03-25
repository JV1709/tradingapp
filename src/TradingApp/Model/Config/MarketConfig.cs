using Model.Domain;

namespace Model.Config
{
    public class MarketConfig
    {
        public List<Instrument> Instruments { get; set; } = new();
    }
}
