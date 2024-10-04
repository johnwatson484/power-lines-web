using Microsoft.AspNetCore.Mvc;
using PowerLinesWeb.Fixtures;

namespace PowerLinesWeb.Controllers;

[Route("[controller]")]
public class FixturesController(IFixtureApi fixtureApi) : Controller
{
    readonly IFixtureApi fixtureApi = fixtureApi;

    [Route("")]
    public IActionResult Index()
    {
        var fixtures = Task.Run(() => fixtureApi.GetFixtures()).Result;
        return View(fixtures);
    }
}
