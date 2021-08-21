using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PowerLinesWeb.Accuracy;
using System.Threading.Tasks;

namespace PowerLinesWeb.Controllers
{
    public class AccuracyController : Controller
    {
        readonly IAccuracyApi accuracyApi;

        public AccuracyController(IAccuracyApi accuracyApi)
        {
            this.accuracyApi = accuracyApi;
        }

        public IActionResult Index()
        {
            var accuracy = Task.Run(() => accuracyApi.GetAccuracy()).Result;
            return View(accuracy);
        }
    }
}
