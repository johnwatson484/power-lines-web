using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PowerLinesWeb.Fixtures;
using System.Threading.Tasks;

namespace PowerLinesWeb.Controllers
{
    public class FixturesController : Controller
    {
        private readonly ILogger<FixturesController> logger;
        IFixtureApi fixtureApi;

        public FixturesController(ILogger<FixturesController> logger, IFixtureApi fixtureApi)
        {
            this.logger = logger;
            this.fixtureApi = fixtureApi;
        }

        public IActionResult Index()
        {
            var fixtures = Task.Run(() => fixtureApi.GetFixtures()).Result;
            return View(fixtures);
        }
    }
}
