
using CameraAnalyzer.bl.Utils;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Services.FtpPolling;
using CameraAnalyzer.bl.Services.StickersExtractor.Workflow;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow
{
      public interface IPackagesAnalysisWorkflow
      {
            Task<List<PackageDetails>> AnalyzeMultipuleImagesAsync(List<DetectionResponse> extractoredImages, string labelsCompany);
            Task<List<PackageDetails>> AnalyzeSingleImageAsync(DetectionResponse extractedImage, string labelsCompany);
      }
      public class PackagesAnalysisWorkflow : IPackagesAnalysisWorkflow
      {
            private readonly GeminiLabelService _gemini;
            private readonly WorkflowOutputService _output;

            // All dependencies are injected from DI
            public PackagesAnalysisWorkflow(
                GeminiLabelService geminiLabelService,
                WorkflowOutputService output)
            {
                  _gemini = geminiLabelService;
                  _output = output;
            }

            public async Task<List<PackageDetails>> AnalyzeMultipuleImagesAsync(List<DetectionResponse> extractoredImages, string labelsCompany)
            {

                  // Create a list of Tasks to process all images in parallel
                  var tasks = extractoredImages.Select(async extractoredImage =>
                  {
                        Logger.LogInfo("Processing LabelName: " + extractoredImage.LabelName);

                        // 1) Detect packages in the image
                        List<byte[]?>? labelsImages = extractoredImage.ImageBase64 != null ? new List<byte[]?> { Convert.FromBase64String(extractoredImage.ImageBase64) } : null;


                        if (labelsImages == null)
                        {
                              // No packages found → return empty list for this image
                              return null;
                        }


                        // 2) Analyze all crops using Gemini
                        // time for Gemini analysis
                        var geminiStartTime = DateTime.UtcNow;
                        Logger.LogInfo($"Detected {labelsImages.Count} potential packages in image '{extractoredImage.LabelName}'. Starting Gemini analysis...");
                        List<List<PackageDetails>> geminiAnalysis = await _gemini.AnalyzeAllImagesAsync(labelsImages);
                        Logger.LogInfo($"👽 Gemini analysis completed for image '{extractoredImage.LabelName}'. Found {geminiAnalysis.Sum(g => g.Count)} packages across all crops.");
                        // time for Gemini analysis
                        var geminiEndTime = DateTime.UtcNow;
                        var geminiDuration = geminiEndTime - geminiStartTime;
                        Logger.LogInfo($"👽 Gemini analysis & string adapter time for image '{extractoredImage.LabelName}': {geminiDuration.TotalSeconds} seconds.");
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
            public async Task<List<PackageDetails>> AnalyzeSingleImageAsync(DetectionResponse extractedImage, string labelsCompany)
            {
                  Logger.LogInfo("Processing LabelName: " + extractedImage.LabelName);

                  // 1) Convert base64 to byte array
                  byte[]? labelImage = extractedImage.ImageBase64 != null
                      ? Convert.FromBase64String(extractedImage.ImageBase64)
                      : null;

                  if (labelImage == null)
                  {
                        Logger.LogWarning($"No image data found for '{extractedImage.LabelName}'");
                        return new List<PackageDetails>();
                  }

                  // 2) Analyze the image using Gemini
                  var geminiStartTime = DateTime.UtcNow;
                  Logger.LogInfo($"Starting Gemini analysis for image '{extractedImage.LabelName}'...");

                  List<List<PackageDetails>> geminiAnalysis = await _gemini.AnalyzeAllImagesAsync(new List<byte[]?> { labelImage });

                  var geminiEndTime = DateTime.UtcNow;
                  var geminiDuration = geminiEndTime - geminiStartTime;
                  Logger.LogInfo($"👽 Gemini analysis completed for image '{extractedImage.LabelName}'. Found {geminiAnalysis.Sum(g => g.Count)} packages.");
                  Logger.LogInfo($"👽 Gemini analysis time for image '{extractedImage.LabelName}': {geminiDuration.TotalSeconds} seconds.");

                  if (geminiAnalysis == null || !geminiAnalysis.Any())
                  {
                        Logger.LogWarning($"No analysis results returned for '{extractedImage.LabelName}'");
                        return new List<PackageDetails>();
                  }

                  // 3) Flatten the results
                  List<PackageDetails> resultForImage = new List<PackageDetails>();
                  foreach (List<PackageDetails> packageList in geminiAnalysis)
                  {
                        resultForImage.AddRange(packageList);
                  }

                  Logger.LogInfo($"Total packages analyzed from image '{extractedImage.LabelName}': {resultForImage.Count}");
                  Logger.LogDebug("Package details: " + System.Text.Json.JsonSerializer.Serialize(resultForImage));

                  return resultForImage;
            }

      }
}
