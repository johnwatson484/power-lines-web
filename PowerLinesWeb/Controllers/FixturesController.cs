using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PowerLinesWeb.Models;
using PowerLinesWeb.Services;

namespace PowerLinesWeb.Controllers
{
    public class FixturesController : Controller
    {
        private readonly ILogger<FixturesController> logger;
        IFixtureService fixtureService;

        public FixturesController(ILogger<FixturesController> logger, IFixtureService fixtureService)
        {
            this.logger = logger;
            this.fixtureService = fixtureService;
        }

        public IActionResult Index()
        {
            return View(fixtureService.GetFixtures());
        }
    }
}
