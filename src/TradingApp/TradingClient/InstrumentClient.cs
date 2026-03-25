using Model.Domain;
using System.Text.Json;

namespace TradingClient
{
    public sealed class InstrumentClient
    {
        private const string BaseUri = "instruments/";
        private readonly HttpClient _httpClient;

        public InstrumentClient(InstrumentClientConfig config)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(config.Hostname);
        }

        public async Task<List<Instrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(BaseUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var instruments = JsonSerializer.Deserialize<List<Instrument>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (instruments == null)
                {
                    throw new TradingClientExceptions.InstrumentsNotFoundException();
                }

                return instruments;
            }
            catch (HttpRequestException ex)
            {
                switch (ex.StatusCode)
                {
                    case System.Net.HttpStatusCode.NotFound:
                        throw new TradingClientExceptions.InstrumentsNotFoundException();
                    default:
                        throw new TradingClientExceptions.UnexpectedErrorException("An unexpected error occurred while fetching instruments.");
                }
            }
            catch (Exception)
            {
                throw new TradingClientExceptions.UnexpectedErrorException("An unexpected error occurred while fetching instruments.");
            }
        }
    }
}
