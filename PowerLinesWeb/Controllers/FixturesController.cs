using Microsoft.AspNetCore.Mvc;
using PowerLinesWeb.Fixtures;

namespace PowerLinesWeb.Controllers;

[Route("[controller]")]
public class FixturesController(IFixtureService fixtureService) : Controller
{
    readonly IFixtureService fixtureService = fixtureService;

    [Route("")]
    public IActionResult Index()
    {
        var fixtures = fixtureService.Get();
        return View(fixtures);
    }
}
