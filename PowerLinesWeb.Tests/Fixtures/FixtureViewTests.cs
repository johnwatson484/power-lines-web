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

    [Test]
    [SetCulture("fr-FR")]
    public async Task Ranking_uses_full_precision_then_kickoff_then_identity_and_puts_missing_forecasts_last()
    {
        var day = new DateTime(2026, 11, 14);
        Fixture Make(int id, decimal? probability, int hour) => new()
        {
            FixtureId = id, Date = day.AddHours(hour), HasForecast = probability.HasValue,
            HomeProbability = probability, AwayProbability = .1m, DrawProbability = .2m,
            Recommended = "X", LowerRecommended = "X"
        };
        var html = await RenderPage(new FixturePage
        {
            Today = day,
            Fixtures = [Make(5, null, 10), Make(4, .69999m, 12), Make(3, .70001m, 16), Make(2, .70001m, 15), Make(1, .70001m, 15)]
        });
        var ids = Regex.Matches(html, "data-fixture-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value));
        Assert.That(ids, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        Assert.That(html, Does.Contain("data-probability=\"0.70001\""));
        Assert.That(html, Does.Contain("width: 70.00100%"));
    }

    [TestCase(.6, .25, .15, "Home", false)]
    [TestCase(.15, .25, .6, "Away", false)]
    [TestCase(.4, .2, .4, "Home / Away (equal)", false)]
    [TestCase(.3, .5, .2, "Home", true)]
    public async Task Best_win_outcome_is_independent_of_shortlist_and_draw_ranking(double home, double draw, double away, string expected, bool drawLeads)
    {
        var html = await Render(new Fixture
        {
            Date = new DateTime(2026, 11, 14, 15, 0, 0), HomeTeam = "Home", AwayTeam = "Away",
            HasForecast = true, IsValid = true, Recommended = "X", LowerRecommended = "X",
            HomeProbability = (decimal)home, DrawProbability = (decimal)draw, AwayProbability = (decimal)away
        });
        Assert.That(html, Does.Contain($"<strong>{expected}</strong>"));
        Assert.That(html.Contains("Draw is more likely"), Is.EqualTo(drawLeads));
        Assert.That(html, Does.Contain("class=\"win-probability\""));
    }

    [Test]
    public async Task Whole_day_ranking_labels_saved_forecasts_and_exposes_day_filter_metadata()
    {
        var html = await RenderPage(new FixturePage
        {
            Today = new DateTime(2026, 11, 14), FixtureTimeZoneId = "Europe/London",
            Fixtures = [new Fixture
            {
                FixtureId = 7, Date = new DateTime(2026, 11, 14, 15, 0, 0),
                DivisionCode = "E1", Division = "Championship", Country = "England",
                HasForecast = true, HasVerifiedKickoff = true, KickoffPassed = true,
                HomeProbability = .7m, DrawProbability = .2m, AwayProbability = .1m,
                Recommended = "X", LowerRecommended = "X"
            }]
        });
        Assert.That(html, Does.Contain("Kickoff passed"));
        Assert.That(html, Does.Contain("Kickoff times: Europe/London"));
        Assert.That(html, Does.Contain("data-division=\"E1\""));
        Assert.That(html, Does.Contain("England &#x2014; Championship"));
        Assert.That(html, Does.Contain("data-kickoff=\"15:00\""));
        Assert.That(html, Does.Contain("Showing 1 of 1 fixtures"));
        Assert.That(html, Does.Contain("data-probability=\"0.7\""));
    }

    [Test]
    public async Task Unverified_kickoff_is_labelled_instead_of_displaying_midnight()
    {
        var html = await Render(new Fixture { Date = new DateTime(2026, 11, 14), Recommended = "X", LowerRecommended = "X" });
        Assert.That(html, Does.Contain("Time unverified"));
        Assert.That(html, Does.Contain("data-kickoff=\"unverified\""));
        Assert.That(html, Does.Contain("data-probability=\"\""));
        Assert.That(html, Does.Not.Contain("class=\"win-probability\""));
    }

    Task<string> Render(Fixture fixture) => RenderPage(new FixturePage { Fixtures = [fixture], Today = fixture.Date.Date });

    async Task<string> RenderPage(FixturePage page)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());
        var view = services.GetRequiredService<ICompositeViewEngine>().GetView(null, "/Views/Fixtures/Index.cshtml", false);
        Assert.That(view.Success, Is.True);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var data = new ViewDataDictionary<FixturePage>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = page
        };
        await view.View.RenderAsync(new ViewContext(context, view.View, data,
            new TempDataDictionary(context.HttpContext, services.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    [OneTimeTearDown]
    public async Task Dispose() => await app.DisposeAsync();
}
