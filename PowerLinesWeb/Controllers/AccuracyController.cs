using Microsoft.AspNetCore.Mvc;
using PowerLinesWeb.Accuracy;

namespace PowerLinesWeb.Controllers;

[Route("[controller]")]
public class AccuracyController(IAccuracyService accuracyService) : Controller
{
    readonly IAccuracyService accuracyService = accuracyService;

    [Route("")]
    [HttpGet]
    public IActionResult Index()
    {
        var accuracy = accuracyService.Get();
        return View(accuracy);
    }
}
