using System.Collections.Generic;
using System.Threading.Tasks;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Accuracy
{
    public interface IAccuracyApi
    {
        Task<List<Models.Accuracy>> GetAccuracy();
    }
}
