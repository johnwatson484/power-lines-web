using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Fixtures;

public class FixtureService(ApplicationDbContext dbContext) : IFixtureService
{
    readonly ApplicationDbContext dbContext = dbContext;

    public List<PowerLinesWeb.Models.Fixture> Get()
    {
        var result = new List<PowerLinesWeb.Models.Fixture>();
        var startDate = DateTime.UtcNow.AddDays(-1).Date;

        var fixtures = dbContext.Fixtures.AsNoTracking()
            .Include(x => x.MatchOdds)
            .Where(x => x.Date >= startDate && x.MatchOdds != null);

        foreach (var fixture in fixtures)
        {
            var division = new Division(fixture.Division);
            result.Add(new PowerLinesWeb.Models.Fixture
            {
                FixtureId = fixture.FixtureId,
                Country = division.Country,
                CountryRank = division.CountryRank,
                Division = division.Name,
                Tier = division.Tier,
                Date = fixture.Date,
                HomeTeam = fixture.HomeTeam,
                AwayTeam = fixture.AwayTeam,
                HomeOdds = fixture.MatchOdds?.Home ?? 0,
                DrawOdds = fixture.MatchOdds?.Draw ?? 0,
                AwayOdds = fixture.MatchOdds?.Away ?? 0,
                HomeGoals = fixture.MatchOdds?.HomeGoals ?? 0,
                AwayGoals = fixture.MatchOdds?.AwayGoals ?? 0,
                ExpectedGoals = fixture.MatchOdds?.ExpectedGoals ?? 0,
                IsValid = fixture.MatchOdds != null && !(fixture.MatchOdds.Home == 0 && fixture.MatchOdds.Draw == 0 && fixture.MatchOdds.Away == 0),
                Recommended = fixture.MatchOdds?.Recommended ?? "X",
                LowerRecommended = fixture.MatchOdds?.LowerRecommended ?? "X",
                ValueSelection = fixture.MatchOdds?.ValueSelection ?? "X",
                ValueEdge = fixture.MatchOdds?.ValueEdge ?? 0,
                ValueOdds = fixture.MatchOdds?.ValueOdds ?? 0,
                ValueStake = fixture.MatchOdds?.ValueStake ?? 0,
                Calculated = fixture.MatchOdds?.Calculated ?? default
            });
        }

        return result.OrderBy(x => x.Date).ThenBy(x => x.CountryRank).ThenBy(x => x.Tier).ThenBy(x => x.HomeTeam).ToList();
    }
}
