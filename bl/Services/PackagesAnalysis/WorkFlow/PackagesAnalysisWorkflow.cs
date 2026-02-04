
using CameraAnalyzer.bl.Utils;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow
{
      public interface IPackagesAnalysisWorkflow
      {
            Task<List<PackageDetails>> AnalyzeImagesAsync(List<string> imagesPaths, string labelsCompany);
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

            public async Task<List<PackageDetails>> AnalyzeImagesAsync(List<string> imagesPaths, string labelsCompany)
            {
                  // Start time

                  // Logger.LogInfo($"Starting analysis for {imagesPaths.Count} images with labels company: {labelsCompany}");
                  

                  // Create a list of Tasks to process all images in parallel
                  var tasks = imagesPaths.Select(async imagePath =>
                  {
                        Logger.LogInfo("Processing image: " + imagePath);

                        // 1) Detect packages in the image
                        List<byte[]?> labelsImages = await _detector.DetectPackagesAsync(imagePath, labelsCompany);
                        if (labelsImages == null)
                        {
                              // No packages found → return empty list for this image
                              return null;
                        }

                        // 2) Analyze all crops using Gemini
                        List<List<PackageDetails>> geminiAnalysis = await _gemini.AnalyzeAllImagesAsync(labelsImages);

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
