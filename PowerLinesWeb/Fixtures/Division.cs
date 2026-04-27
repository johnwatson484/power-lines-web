namespace PowerLinesWeb.Fixtures;

public class Division
{
    public string Country { get; set; }
    public int CountryRank { get; set; }
    public string Name { get; set; }
    public int Tier { get; set; }

    public Division(string divisionCode)
    {
        SetProperties(divisionCode);
    }

    private void SetProperties(string divisionCode)
    {
        switch (divisionCode)
        {
            case "E0": SetProperties("England", 1, "Premier League", 1); break;
            case "E1": SetProperties("England", 1, "Championship", 2); break;
            case "E2": SetProperties("England", 1, "League 1", 3); break;
            case "E3": SetProperties("England", 1, "League 2", 4); break;
            case "EC": SetProperties("England", 1, "National League", 5); break;
            case "B1": SetProperties("Belgium", 9, "Pro League", 1); break;
            case "D1": SetProperties("Germany", 4, "Bundesliga", 1); break;
            case "D2": SetProperties("Germany", 4, @"2. Bundesliga", 2); break;
            case "F1": SetProperties("France", 5, "Ligue 1", 1); break;
            case "F2": SetProperties("France", 5, "Ligue 2", 2); break;
            case "G1": SetProperties("Greece", 10, "Super League", 1); break;
            case "I1": SetProperties("Italy", 3, "Serie A", 1); break;
            case "I2": SetProperties("Italy", 3, "Serie B", 2); break;
            case "N1": SetProperties("Netherlands", 6, "Eredivisie", 1); break;
            case "P1": SetProperties("Portugal", 7, "Primeira Liga", 1); break;
            case "SC0": SetProperties("Scotland", 8, "Premiership", 1); break;
            case "SC1": SetProperties("Scotland", 8, "Championship", 2); break;
            case "SC2": SetProperties("Scotland", 8, "League 1", 3); break;
            case "SC3": SetProperties("Scotland", 8, "League 2", 4); break;
            case "SP1": SetProperties("Spain", 2, "La Liga", 1); break;
            case "SP2": SetProperties("Spain", 2, "Segunda Division", 2); break;
            case "T1": SetProperties("Turkey", 11, "Süper Lig", 1); break;
            default: SetProperties("Unknown", 12, "Unknown", 1); break;
        }
    }

    private void SetProperties(string country, int countryRank, string name, int tier)
    {
        Country = country;
        CountryRank = countryRank;
        Name = name;
        Tier = tier;
    }
}
