using System.Collections.Generic;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Services
{
    public interface IFixtureService
    {
        List<Fixture> GetFixtures();
    }
}
