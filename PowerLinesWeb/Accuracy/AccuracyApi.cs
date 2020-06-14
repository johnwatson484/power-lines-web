using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using PowerLinesWeb.Models;
using Newtonsoft.Json;
using System;

namespace PowerLinesWeb.Accuracy
{
    public class AccuracyApi : IAccuracyApi
    {
        AccuracyUrl accuracyUrl;

        public AccuracyApi(AccuracyUrl accuracyUrl)
        {
            this.accuracyUrl = accuracyUrl;
        }

        public async Task<List<Models.Accuracy>> GetAccuracy()
        {
            var accuracy = new List<Models.Accuracy>();

            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(string.Format("{0}/{1}", accuracyUrl.Endpoint, accuracyUrl.Accuracy)))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        accuracy = JsonConvert.DeserializeObject<List<Models.Accuracy>>(apiResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Accuracy API unavailable: {0}", ex);
            }

            return accuracy;
        }
    }
}
