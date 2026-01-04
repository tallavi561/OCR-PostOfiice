using System.Text.Json;
using CameraAnalyzer.bl.Models;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices
{
    public class WorkflowOutputService
    {
        public PackageDetails? BuildJson(string geminiResult)
        {

            var parsed = JsonSerializer.Deserialize<PackageDetails>(geminiResult);

            return parsed;
        }
    }
}
