using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PowerLinesWeb.Fixtures;
using System.Threading.Tasks;

namespace PowerLinesWeb.Controllers
{
    public class FixturesController : Controller
    {
        readonly IFixtureApi fixtureApi;

        public FixturesController(IFixtureApi fixtureApi)
        {
            this.fixtureApi = fixtureApi;
        }

        public IActionResult Index()
        {
            var fixtures = Task.Run(() => fixtureApi.GetFixtures()).Result;
            return View(fixtures);
        }
    }
}
