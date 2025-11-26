using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices
{
      public class DetectionService
      {
            private readonly AiDetectorAPI _aiDetector;

            public DetectionService()
            {
                  _aiDetector = new AiDetectorAPI();
            }

            public async Task<List<BoundingBox>> DetectPackagesAsync(string imagePath)
            {
                  Logger.LogInfo("Detecting packages...");
                  var boxes = await _aiDetector.Detect(imagePath);

                  if (boxes == null || boxes.Count == 0)
                  {
                        Logger.LogInfo("No bounding boxes found.");
                        return new List<BoundingBox>();
                  }
                  return boxes;
            }
      }
}
