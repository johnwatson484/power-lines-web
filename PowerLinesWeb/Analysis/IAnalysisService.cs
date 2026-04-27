namespace PowerLinesWeb.Analysis;

public interface IAnalysisService
{
    AnalysisMatchOdds GetMatchOdds(AnalysisFixture fixture);
}
