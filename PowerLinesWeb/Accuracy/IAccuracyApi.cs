namespace PowerLinesWeb.Accuracy;

public interface IAccuracyApi
{
    Task<List<Models.Accuracy>> GetAccuracy();
}

