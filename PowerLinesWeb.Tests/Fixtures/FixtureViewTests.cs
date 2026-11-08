using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PowerLinesWeb.Controllers;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Tests.Fixtures;

public class FixtureViewTests
{
    WebApplication app;

    [OneTimeSetUp]
    public void Initialize()
    {
        // Render compiled views without starting the application or its live workers.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FixturesController).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Services.AddControllersWithViews();
        app = builder.Build();
    }

    [TestCase(.6, .25, .15, new[] { 0, 3 })]
    [TestCase(.2, .6, .2, new[] { 1, 4 })]
    [TestCase(.15, .25, .6, new[] { 2, 5 })]
    [TestCase(.4, .4, .2, new[] { 0, 1, 3, 4 })]
    [TestCase(.333333, .333333, .333333, new[] { 0, 1, 2, 3, 4, 5 })]
    [TestCase(.40001, .39999, .2, new[] { 0, 3 })]
    [SetCulture("")]
    public async Task Saved_forecast_bolds_only_highest_outcomes_and_corresponding_odds(double home, double draw, double away, int[] boldIndices)
    {
        var fixture = new Fixture
        {
            HasForecast = true, IsValid = false, Status = "Kickoff passed",
            Date = new DateTime(2025, 8, 2, 12, 0, 0),
            HomeTeam = "Home", AwayTeam = "Away", Recommended = "X", LowerRecommended = "X",
            HomeProbability = (decimal)home, DrawProbability = (decimal)draw, AwayProbability = (decimal)away,
            HomeOdds = 1m / (decimal)home, DrawOdds = 1m / (decimal)draw, AwayOdds = 1m / (decimal)away
        };
        var html = await Render(fixture);
        var spans = Regex.Matches(html, "<span(?: class=\"([^\"]*)\")?>([^<]*)</span>");
        Assert.That(spans, Has.Count.EqualTo(6));
        Assert.That(Enumerable.Range(0, spans.Count).Where(i => spans[i].Groups[1].Value == "font-weight-bold"), Is.EqualTo(boldIndices));
        var expected = new[]
        {
            fixture.HomeProbability.Value.ToString("P1"), fixture.DrawProbability.Value.ToString("P1"), fixture.AwayProbability.Value.ToString("P1"),
            fixture.HomeOdds.Value.ToString("F2"), fixture.DrawOdds.Value.ToString("F2"), fixture.AwayOdds.Value.ToString("F2")
        };
        Assert.That(spans.Select(s => s.Groups[2].Value), Is.EqualTo(expected));
        Assert.That(html, Does.Contain("data-date=\"2025-08-02\" data-tier=\"none\" data-price=\"false\""));
    }

    [Test]
    public async Task Missing_forecast_shows_status_instead_of_values()
    {
        var html = await Render(new Fixture { Date = new DateTime(2025, 8, 2), HasForecast = false, Status = "Awaiting analysis", Recommended = "X", LowerRecommended = "X" });
        Assert.That(html, Does.Contain("colspan=\"2\" class=\"text-muted\">Awaiting analysis</td>"));
        Assert.That(html, Does.Not.Contain("font-weight-bold"));
    }

    async Task<string> Render(Fixture fixture)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());
        var view = services.GetRequiredService<ICompositeViewEngine>().GetView(null, "/Views/Fixtures/Index.cshtml", false);
        Assert.That(view.Success, Is.True);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var data = new ViewDataDictionary<FixturePage>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = new FixturePage { Fixtures = [fixture], Today = fixture.Date.Date }
        };
        await view.View.RenderAsync(new ViewContext(context, view.View, data,
            new TempDataDictionary(context.HttpContext, services.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    [OneTimeTearDown]
    public async Task Dispose() => await app.DisposeAsync();
}
