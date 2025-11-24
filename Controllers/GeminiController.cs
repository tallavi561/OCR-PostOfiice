// using Microsoft.AspNetCore.Mvc;
// using System.Threading.Tasks;
// using CameraAnalyzer.bl.Models;
// using CameraAnalyzer.bl.Services;
// using CameraAnalyzer.bl.Utils;

// namespace CameraAnalyzer.Controllers
// {
//     [ApiController]
//     [Route("api/v1/[controller]")]
//     public class GeminiController : ControllerBase
//     {
//         private readonly IGeminiService _geminiService;

//         public GeminiController(IGeminiService geminiService)
//         {
//             _geminiService = geminiService;
//         }

//         [HttpGet("analyzeFixedImage")]
//         public async Task<IActionResult> AnalyzeFixedImageAsync([FromQuery] string? prompt)
//         {
//             Logger.LogInfo("GeminiController.AnalyzeFixedImageAsync called.");

//             var result = await _geminiService.AnalyzeFixedImageAsync(prompt);


//             if (!System.IO.File.Exists(imagePath))
//             {
//                 Logger.LogError("Service returned null result.");
//                 return BadRequest("Gemini service returned null result.");
//             }
//             List<BoundingBox> BoundingBoxes = PackagesDetector.Detect("./test1.png", 0.01f);
//             var x = "1";
//             BoundingBoxes.ForEach((boundingBox) =>
//             {
//                 ImagesProcessing.CropAndSaveImage(boundingBox.X1, boundingBox.Y1, boundingBox.X2, boundingBox.Y2, "./test1.png", "./cropped_outputs/" + x + ".png");
//                 x += "1";

//             });

//             string base64Image;
//             using (var memoryStream = new MemoryStream())
//             {
//                 await using (var fileStream = System.IO.File.OpenRead(imagePath))
//                     await fileStream.CopyToAsync(memoryStream);
//                 base64Image = Convert.ToBase64String(memoryStream.ToArray());
//             }

//             string mimeType = "image/png";
//             Logger.LogInfo($"Sending fixed image '{imagePath}' to Gemini API...");

//             string? result = await _geminiAPI.AnalyzeImageAsync(base64Image, prompt, mimeType);

//             if (string.IsNullOrWhiteSpace(result))
//             {
//                 Logger.LogError("Gemini returned empty response for image.");
//                 return BadRequest("Gemini returned no text output.");
//             }

//             Logger.LogInfo("Gemini image analysis completed successfully: " + result);

//             try
//             {
//                 var boxes = System.Text.Json.JsonSerializer.Deserialize<List<CameraAnalyzer.bl.Models.BoundingBox>>(result);

//                 if (boxes == null || boxes.Count == 0)
//                 {
//                     Logger.LogError("No bounding boxes found in Gemini response.");
//                     return Ok("No bounding boxes detected.");
//                 }

//                 string sourceImagePath = "./test1.png";
//                 int index = 1;

//                 foreach (var box in boxes)
//                 {
//                     string newFilePath = $"./cropped_outputs/crop_{index}.png";
//                     ImagesProcessing.CropAndSaveImage(box.X1, box.Y1, box.X2, box.Y2, sourceImagePath, newFilePath);
//                     index++;
//                 }

//                 return Ok(new { message = "Cropping completed.", count = boxes.Count });
//             }
//             catch (Exception ex)
//             {
//                 Logger.LogError("Error parsing bounding box JSON: " + ex.Message);
//                 return BadRequest("Invalid bounding box JSON format.");
//             }



//         }
//     }
// }
