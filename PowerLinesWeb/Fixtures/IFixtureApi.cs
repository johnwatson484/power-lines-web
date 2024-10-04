using PowerLinesWeb.Models;

namespace PowerLinesWeb.Fixtures;

public interface IFixtureApi
{
    Task<List<Fixture>> GetFixtures();
}
