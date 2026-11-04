namespace PowerLinesWeb.Accuracy;

public interface IAccuracyService
{
    List<PowerLinesWeb.Models.Accuracy> Get();
    PowerLinesWeb.Models.AccuracyPage GetPage();
}
