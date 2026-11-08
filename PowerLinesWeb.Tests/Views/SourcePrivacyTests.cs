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

namespace PowerLinesWeb.Tests.Views;

public class SourcePrivacyTests
{
    const string Private = "PRIVATE_SOURCE_METADATA";

    [TestCase(false)]
    [TestCase(true)]
    public async Task Fixtures_show_prices_without_source_or_import_diagnostics(bool available)
    {
        var page = new FixturePage
        {
            KickoffTimeZone = Private,
            Feeds = [new() { Division = Private, Status = Private }],
            Fixtures = [new()
            {
                HomeTeam = "Home", AwayTeam = "Away", Division = "Premier League",
                Recommended = "H", LowerRecommended = "H", HomeProbability = .7m,
                MarketHome = 2.5m, MarketDraw = 3, MarketAway = 4,
                Source = Private, Status = Private, Support = Private, FeedStatus = Private,
                ForecastOrigin = Private, PriceKind = Private, AvailableOffer = available
            }]
        };

        var html = await Render("Fixtures", page);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain(Private));
            Assert.That(html, Does.Not.Contain("Status / price support"));
            Assert.That(html, Does.Not.Contain("import").IgnoreCase);
            Assert.That(html, Does.Contain("Home v Away"));
            Assert.That(html, Does.Contain(2.5m.ToString("F2")));
            Assert.That(html, Does.Contain(available ? "Available prices" : "Indicative prices"));
            Assert.That(System.Text.RegularExpressions.Regex.Matches(html, "<th>").Count, Is.EqualTo(6));
            Assert.That(html, Does.Contain("data-tier=\"strong\" data-price=\"false\""));
        });
    }

    [Test]
    public async Task Empty_fixtures_do_not_direct_visitors_to_import_coverage()
    {
        var html = await Render("Fixtures", new FixturePage());
        Assert.That(html, Does.Contain("No upcoming fixtures."));
        Assert.That(html, Does.Not.Contain("import").IgnoreCase);
    }

    [Test]
    public async Task Accuracy_shows_scores_without_report_provenance_or_free_text_labels()
    {
        var page = new AccuracyPage
        {
            RunId = Private, Eligible = 100,
            Candidates = [new() { Candidate = Private, Forecasts = 80, LogLoss = .9 }],
            ReportJson = """
                {
                  "SourceUrl": "PRIVATE_SOURCE_METADATA",
                  "DatasetHash": "PRIVATE_SOURCE_METADATA",
                  "Shortlist": { "Count": 20, "Home": 12, "Away": 8, "HitRate": 0.7, "MeanProbability": 0.68, "CalibrationGap": -0.02 },
                  "Strong": { "Count": 12 }, "Broader": { "Count": 8 },
                  "ModelMarketComparison": { "Count": 80, "CandidateLogLoss": 0.9, "BaselineLogLoss": null },
                  "Promotion": { "FairOddsModel": "PRIVATE_SOURCE_METADATA", "WiderShortlist": "PRIVATE_SOURCE_METADATA" }
                }
                """
        };

        var html = await Render("Accuracy", page);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain(Private));
            Assert.That(html, Does.Not.Contain("DatasetHash"));
            Assert.That(html, Does.Not.Contain("<pre>"));
            Assert.That(html, Does.Contain("Candidate 1"));
            Assert.That(html, Does.Contain(.9.ToString("F4")));
            Assert.That(html, Does.Contain("20 (12 home, 8 away)"));
            Assert.That(html, Does.Contain("market log loss: Unavailable"));
        });
    }

    static async Task<string> Render<T>(string view, T model)
    {
        // Build MVC alone: never start the application, database migrations or workers.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FixturesController).Assembly.GetName().Name
        });
        builder.Services.AddControllersWithViews();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var action = new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());
        var result = services.GetRequiredService<ICompositeViewEngine>().GetView(null, $"/Views/{view}/Index.cshtml", isMainPage: false);
        Assert.That(result.Success, Is.True, string.Join(", ", result.SearchedLocations ?? []));
        using var writer = new StringWriter();
        var data = new ViewDataDictionary<T>(services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        var context = new ViewContext(action, result.View, data,
            new TempDataDictionary(action.HttpContext, services.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions());
        await result.View.RenderAsync(context);
        return writer.ToString();
    }
}
