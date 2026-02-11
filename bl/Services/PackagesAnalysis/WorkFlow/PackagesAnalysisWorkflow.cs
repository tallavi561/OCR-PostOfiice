
using CameraAnalyzer.bl.Utils;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Services.FtpPolling;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow
{
      public interface IPackagesAnalysisWorkflow
      {
            Task<List<PackageDetails>> AnalyzeImagesAsync(List<ImageFromFtp> imagesFromFTP, string labelsCompany);
      }
      public class PackagesAnalysisWorkflow : IPackagesAnalysisWorkflow
      {
            private readonly DetectionService _detector;
            private readonly GeminiLabelService _gemini;
            private readonly WorkflowOutputService _output;

            // All dependencies are injected from DI
            public PackagesAnalysisWorkflow(
                DetectionService detector,
                GeminiLabelService geminiLabelService,
                WorkflowOutputService output)
            {
                  _detector = detector;
                  _gemini = geminiLabelService;
                  _output = output;
            }

            public async Task<List<PackageDetails>> AnalyzeImagesAsync(List<ImageFromFtp> imagesFromFTP, string labelsCompany)
            {

                  // Create a list of Tasks to process all images in parallel
                  var tasks = imagesFromFTP.Select(async imageFromFTP =>
                  {
                        Logger.LogInfo("Processing image: " + imageFromFTP.ImageName);

                        // 1) Detect packages in the image
                        List<byte[]?> labelsImages = await _detector.DetectPackagesAsync(imageFromFTP, labelsCompany);
                        if (labelsImages == null)
                        {
                              // No packages found → return empty list for this image
                              return null;
                        }
                        

                        // 2) Analyze all crops using Gemini
                        // time for Gemini analysis
                        var geminiStartTime = DateTime.UtcNow;
                        Logger.LogInfo($"Detected {labelsImages.Count} potential packages in image '{imageFromFTP.ImageName}'. Starting Gemini analysis...");
                        List<List<PackageDetails>> geminiAnalysis = await _gemini.AnalyzeAllImagesAsync(labelsImages);
                        Logger.LogInfo($"Gemini analysis completed for image '{imageFromFTP.ImageName}'. Found {geminiAnalysis.Sum(g => g.Count)} packages across all crops.");
                        // time for Gemini analysis
                        var geminiEndTime = DateTime.UtcNow;
                        var geminiDuration = geminiEndTime - geminiStartTime;
                        Logger.LogInfo($"<> Gemini analysis & string adapter time for image '{imageFromFTP.ImageName}': {geminiDuration.TotalSeconds} seconds.");
                        if (geminiAnalysis == null)
                        {
                              return null;
                        }
                        // var geminiAnalysis = new List<string>();
                        // Build the JSON result for this image
                        List<PackageDetails> resultForImage = [];
                        foreach (List<PackageDetails> gA in geminiAnalysis)
                        {
                              resultForImage.AddRange(gA);

                        }

                        return resultForImage;
                  });

                  var allPackagesDetails = new List<PackageDetails>();

                  // ממתינים לכל המשימות במקביל
                  List<PackageDetails>?[] allDetails = await Task.WhenAll(tasks);

                  // שימוש ב-AddRange כדי לאחד את הרשימות
                  foreach (var detailsList in allDetails)
                  {
                        if (detailsList != null)
                        {
                              allPackagesDetails.AddRange(detailsList); // כאן היה התיקון מ-Add ל-AddRange
                        }
                  }
                  Logger.LogInfo($"Total packages analyzed from all images: {allPackagesDetails.Count}");
                  Logger.LogDebug("Packages details: " + System.Text.Json.JsonSerializer.Serialize(allPackagesDetails));
                  return allPackagesDetails;
            }

      }
}
