namespace PowerLinesWeb.Fixtures;

public interface IFixtureService
{
    List<PowerLinesWeb.Models.Fixture> Get();
    PowerLinesWeb.Models.FixturePage GetPage();
}
