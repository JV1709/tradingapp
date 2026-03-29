using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Config;
using Model.Domain;

namespace TradingPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstrumentsController : ControllerBase
    {
        private readonly MarketConfig _config;

        public InstrumentsController(IOptions<MarketConfig> config)
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
