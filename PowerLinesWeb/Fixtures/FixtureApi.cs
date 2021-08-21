using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using PowerLinesWeb.Models;
using Newtonsoft.Json;
using System;

namespace PowerLinesWeb.Fixtures
{
    public class FixtureApi : IFixtureApi
    {
        readonly FixtureUrl fixtureUrl;

        public FixtureApi(FixtureUrl fixtureUrl)
        {
            this.fixtureUrl = fixtureUrl;
        }

        public async Task<List<Fixture>> GetFixtures()
        {
            var fixtures = new List<Fixture>();

            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(string.Format("{0}/{1}", fixtureUrl.Endpoint, fixtureUrl.Fixtures)))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        fixtures = JsonConvert.DeserializeObject<List<Fixture>>(apiResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fixtures API unavailable: {0}", ex);
            }

            return fixtures;
        }
    }
}
