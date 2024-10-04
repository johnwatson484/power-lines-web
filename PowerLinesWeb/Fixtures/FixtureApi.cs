using PowerLinesWeb.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;

namespace PowerLinesWeb.Fixtures;

public class FixtureApi(IOptions<FixtureOptions> fixtureOptions) : IFixtureApi
{
    readonly FixtureOptions fixtureOptions = fixtureOptions.Value;

    public async Task<List<Fixture>> GetFixtures()
    {
        var fixtures = new List<Fixture>();

        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(string.Format("{0}/{1}", fixtureOptions.Endpoint, fixtureOptions.Fixtures));
            string apiResponse = await response.Content.ReadAsStringAsync();
            fixtures = JsonConvert.DeserializeObject<List<Fixture>>(apiResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fixtures API unavailable: {0}", ex);
        }

        return fixtures;
    }
}
