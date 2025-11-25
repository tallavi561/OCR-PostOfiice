using System.Text.Json;
using CameraAnalyzer.bl.Models;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices
{
    public class WorkflowOutputService
    {
        public List<PackageDetails> BuildJson(List<string> geminiResults)
        {
            var allPackages = new List<PackageDetails>();

            foreach (var json in geminiResults)
            {
                var parsed = JsonSerializer.Deserialize<List<PackageDetails>>(json);

                if (parsed != null)
                {
                    allPackages.AddRange(parsed);
                }
            }

            return allPackages;
        }
    }
}
