using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services
{
    public interface IGeminiService
    {
        Task<object> AnalyzeFixedImageAsync(string? prompt);
    }

    public class GeminiService : IGeminiService
    {
        private readonly GeminiAPI _geminiAPI;

        public GeminiService(GeminiAPI geminiAPI)
        {
            _geminiAPI = geminiAPI;
        }

        public async Task<object> AnalyzeFixedImageAsync(string? prompt)
        {
            try
            {
                prompt ??= GetPromptForBoundingBoxes();
                string imagePath = "./test1.png";

                if (!File.Exists(imagePath))
                {
                    Logger.LogError($"Image not found at: {imagePath}");
                    return new { error = $"File '{imagePath}' not found." };
                }

                string base64Image = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath));
                string mimeType = "image/png";

                Logger.LogInfo($"Sending fixed image '{imagePath}' to Gemini API...");

                string? result = await _geminiAPI.AnalyzeImageAsync(base64Image, prompt, mimeType);

                if (string.IsNullOrWhiteSpace(result))
                {
                    Logger.LogError("Gemini returned empty response for image.");
                    return new { error = "Gemini returned no text output." };
                }

                Logger.LogInfo("Gemini image analysis completed successfully.");

                List<BoundingBox>? boxes = null;

                try
                {
                    boxes = JsonSerializer.Deserialize<List<BoundingBox>>(result);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error parsing bounding box JSON: " + ex.Message);
                    return new { error = "Invalid bounding box JSON format." };
                }

                if (boxes == null || boxes.Count == 0)
                {
                    Logger.LogError("No bounding boxes found in Gemini response.");
                    return new { message = "No bounding boxes detected." };
                }

                Directory.CreateDirectory("./cropped_outputs");

                int index = 1;
                foreach (var box in boxes)
                {
                    string newFilePath = $"./cropped_outputs/crop_{index}.png";
                    ImagesProcessing.CropAndSaveImage(box.X1, box.Y1, box.X2, box.Y2, imagePath, newFilePath);
                    index++;
                }

                Logger.LogInfo($"Cropping completed. {boxes.Count} cropped images saved.");
                return new { message = "Cropping completed.", count = boxes.Count };
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error in AnalyzeFixedImageAsync: " + ex.Message);
                return new { error = "Unexpected server error." };
            }
        }

        private string GetPromptForBoundingBoxes()
        {
            return string.Join("\n", new[]
            {
                "Analyze the attached image of packages.",
                "Detect all packages visible in the image.",
                "For each detected package, return its bounding box coordinates as pixel values relative to the top-left corner of the image.",
                "Return the result strictly as a JSON array, where each element is an object in the format:",
                "[{\"X1\": <left>, \"Y1\": <top>, \"X2\": <right>, \"Y2\": <bottom>}]",
                "Do not include any explanations, comments, or additional text — only the JSON array."
            });
        }
    }
}
