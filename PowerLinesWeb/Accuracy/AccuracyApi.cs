using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace PowerLinesWeb.Accuracy;

public class AccuracyApi(IOptions<AccuracyOptions> accuracyOptions) : IAccuracyApi
{
    readonly AccuracyOptions accuracyOptions = accuracyOptions.Value;

    public async Task<List<Models.Accuracy>> GetAccuracy()
    {
        var accuracy = new List<Models.Accuracy>();

        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(string.Format("{0}/{1}", accuracyOptions.Endpoint, accuracyOptions.Accuracy));
            string apiResponse = await response.Content.ReadAsStringAsync();
            accuracy = JsonConvert.DeserializeObject<List<Models.Accuracy>>(apiResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Accuracy API unavailable: {0}", ex);
        }

        return accuracy;
    }
}
