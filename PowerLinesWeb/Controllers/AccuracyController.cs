using Microsoft.AspNetCore.Mvc;
using PowerLinesWeb.Accuracy;

namespace PowerLinesWeb.Controllers;

[Route("[controller]")]
public class AccuracyController(IAccuracyApi accuracyApi) : Controller
{
    readonly IAccuracyApi accuracyApi = accuracyApi;

    [Route("")]
    [HttpGet]
    public IActionResult Index()
    {
        var accuracy = Task.Run(() => accuracyApi.GetAccuracy()).Result;
        return View(accuracy);
    }
}
