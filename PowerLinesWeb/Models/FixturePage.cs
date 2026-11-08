namespace PowerLinesWeb.Models;

public class FixturePage
{
    public List<Fixture> Fixtures { get; set; } = [];
    public decimal Higher { get; set; }
    public decimal Lower { get; set; }
    public DateTime Today { get; set; }
}
