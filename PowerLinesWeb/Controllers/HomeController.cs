using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Controllers;

[Route("")]
public class HomeController : Controller
{
    [Route("")]
    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.Location = "Home";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
