using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PowerLinesWeb.Accuracy;
using System.Threading.Tasks;

namespace PowerLinesWeb.Controllers
{
    public class AccuracyController : Controller
    {
        private readonly ILogger<AccuracyController> logger;
        IAccuracyApi accuracyApi;

        public AccuracyController(ILogger<AccuracyController> logger, IAccuracyApi accuracyApi)
        {
            this.logger = logger;
            this.accuracyApi = accuracyApi;
        }

        public IActionResult Index()
        {
            var accuracy = Task.Run(() => accuracyApi.GetAccuracy()).Result;
            return View(accuracy);
        }
    }
}
