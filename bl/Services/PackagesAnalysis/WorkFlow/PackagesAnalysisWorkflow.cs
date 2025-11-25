
using CameraAnalyzer.bl.Utils;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow
{
      public interface IPackagesAnalysisWorkflow
      {
            Task<List<PackageDetails>> AnalyzeImagesAsync(List<string> imagePaths);
      }
      public class PackagesAnalysisWorkflow : IPackagesAnalysisWorkflow
      {
            private readonly DetectionService _detector;
            private readonly CroppingService _cropper;
            private readonly GeminiLabelService _gemini;
            private readonly WorkflowOutputService _output;

            public PackagesAnalysisWorkflow(GoogleVisionAPI vision, GeminiAPI gemini)
            {
                  _detector = new DetectionService();
                  _cropper = new CroppingService();
                  _gemini = new GeminiLabelService(gemini);
                  _output = new WorkflowOutputService();
            }

            public async Task<List<PackageDetails>> AnalyzeImagesAsync(List<string> imagePaths)
            {
                  string imagePath = "./three.png";

                  // 1) Detect
                  var boxes = await _detector.DetectAsync(imagePath);
                  if (boxes.Count == 0)
                        return new List<PackageDetails>();

                  // 2) Crop
                  var crops = _cropper.CropAll(imagePath, boxes);

                  // 3) Analyze w/ Gemini
                  var geminiResults = await _gemini.AnalyzeAllAsync(crops);

                  foreach (var result in geminiResults)
                  {
                        Logger.LogInfo("Gemini Result: " + result);
                  }
                  // 4) Output JSON
                  return _output.BuildJson(geminiResults);
            }
      }
}


// using System.Text.Json;
// using CameraAnalyzer.bl.APIs;
// using CameraAnalyzer.bl.Models;
// using CameraAnalyzer.bl.Utils;
// // using YoloDotNet;
// namespace CameraAnalyzer.bl.Services
// {
//       public interface IPackagesAnalysisWorkflow
//       {
//             Task<string> AnalyzeImageAsync();
//             // YoloAPI api = new YoloAPI("models/yolov10x.onnx");
//       }

//       public class PackagesAnalysisWorkflow : IPackagesAnalysisWorkflow
//       {
//             private readonly GoogleVisionAPI _googleVisionAPI;
//             private readonly GeminiAPI _geminiApi;
//             // private readonly YoloAPI _yolo;
//             private readonly AiDetectorAPI _aiDetector;

//             public PackagesAnalysisWorkflow(GoogleVisionAPI googleVisionAPI,
//                                            GeminiAPI geminiApi)
//             {
//                   // _googleVisionAPI = googleVisionAPI;
//                   _geminiApi = geminiApi;
//                   // _yolo = new YoloAPI("models/yolov10x.onnx");
//                   _aiDetector = new AiDetectorAPI();
//             }
//             // private string GetPromptForBoundingBoxes()
//             // {
//             //       return string.Join("\n",
//             //       [
//             //     "Analyze the attached image of packages.",
//             //     "Detect all packages visible in the image.",
//             //     "For each detected package, return its bounding box coordinates as pixel values relative to the top-left corner of the image.",
//             //     "Return the result strictly as a JSON array, where each element is an object in the format:",
//             //     "[{\"X1\": <left>, \"Y1\": <top>, \"X2\": <right>, \"Y2\": <bottom>}]",
//             //     "Do not include any explanations, comments, or additional text — only the JSON array."
//             //       ]);
//             // }
//         private string GetPromptForAnalyzingShippingLabel()
//         {
//             string[] lines =
//             {
//                 "Analyze this shipping label image.",
//                 "Extract the 'ship to' and 'ship from' details as JSON objects.",
//                 "If multiple labels are detected, return an array of JSON objects.",
//                 "Do not add data that isn't visible in the image.",
//                 "If any data is missing, fill it as null.",
//                 "If countries/states are abbreviations, expand them to full names.",
//                 "For each phone number, include the correct country code (e.g. '+1', '+44').",
//                 "Ensure accuracy between 'to' and 'from' — if the text is near 'To:' or 'From:', it belongs there."
//             };
//             return string.Join("\n", lines);
//         }
//             public async Task<string> AnalyzeImageAsync()
//             {
//                   // Step 1: detect packages using Google Vision API
//                   Logger.LogInfo("Starting package detection using Google Vision API...");

//                   string base64Image = await ImagesProcessing.ConvertImageToBase64("./test1.png");
//                   // string jsonString  = 

//                   string fileName = "./three.png";
//                   // List<BoundingBox> boundingBoxes =   _yolo.Detect(fileName);
//                   List<BoundingBox> boundingBoxes = await _aiDetector.Detect(fileName);


//                   if (boundingBoxes == null || boundingBoxes.Count == 0)
//                   {
//                         Logger.LogInfo("No bounding boxes detected.");
//                         return "No bounding boxes detected.";
//                   }
//                   Logger.LogInfo($"Detected {boundingBoxes.Count} bounding boxes. Starting cropping...");

//                   // Step 2: crop images based on detected bounding boxes
//                   List<string> croppedImage = new List<string>();
//                   foreach (var box in boundingBoxes)
//                   {
//                         string newFilePath = $"./cropped_outputs/crop_{Guid.NewGuid()}.png";
//                         croppedImage.Add(newFilePath);
//                         ImagesProcessing.CropAndSaveImage(box.X1, box.Y1, box.X2, box.Y2, fileName, newFilePath, box.Confidence);
//                   }

//                   // Step 3: analyze each cropped image with Gemini
//                   var tasks = croppedImage.Select(async croppedImagePath =>
//                   {
//                         try
//                         {
//                               var result = await _geminiApi.AnalyzeImageFromStorageAsync(
//                 croppedImagePath,
//                 GetPromptForAnalyzingShippingLabel()
//             );

//                               if (result == null)
//                               {
//                                     Logger.LogError("Gemini returned null for: " + croppedImagePath);
//                                     return null;
//                               }

//                               return result;
//                         }
//                         catch (Exception ex)
//                         {
//                               Logger.LogError("Error analyzing image " + croppedImagePath + ": " + ex.Message);
//                               return null;
//                         }
//                   });


//                   var results = await Task.WhenAll(tasks);

//                   List<string?> geminiResponses = results.Where(r => r != null).ToList();
//                   Logger.LogInfo($"Gemini analysis completed for {geminiResponses.Count} images.");
//                   foreach (var response in geminiResponses)
//                   {
//                         Logger.LogInfo("Gemini Response: " + response);
//                   }
//                   return JsonSerializer.Serialize(geminiResponses);
//             }
//       }
// }