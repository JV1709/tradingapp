using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Config;
using Model.Domain;

namespace TradingPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstrumentController : ControllerBase
    {
        private readonly MarketConfig _config;

        public InstrumentController(IOptions<MarketConfig> config)
        {
            _config = config.Value;
        }

        [HttpGet]
        public ActionResult<List<Instrument>> GetInstruments()
        {
            return Ok(_config.Instruments);
        }
    }
}
