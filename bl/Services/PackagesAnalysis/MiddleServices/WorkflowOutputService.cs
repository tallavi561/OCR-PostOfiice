using System.Text.Json;

namespace CameraAnalyzer.bl.Services.MiddleServices
{
    public class WorkflowOutputService
    {
        public string BuildJson(List<string> geminiResults)
        {
            return JsonSerializer.Serialize(geminiResults);
        }
    }
}
