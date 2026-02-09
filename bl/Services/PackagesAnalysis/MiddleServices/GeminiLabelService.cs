using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices
{
      public class GeminiLabelService
      {
            private readonly GeminiAPI _gemini;

            public GeminiLabelService(GeminiAPI gemini)
            {
                  _gemini = gemini;
            }

            private static string GetPropertiesPrompt()
            {
                  return string.Join("\n", new[]
                  {
        "Analyze the shipping label image and extract ALL package details.",
        "Return the result STRICTLY in this JSON format:",
        "[",
        "  {",
        "    \"barcode\": string,",
        "    \"from\": {",
        "      \"name\": string,",
        "      \"phone\": string,",
        "      \"email\": string,",
        "      \"address\": {",
        "        \"country\": string,",
        "        \"state\": string,",
        "        \"region\": string,",
        "        \"city\": string,",
        "        \"postalCode\": string,",
        "        \"streetAndHouse\": string",
        "      }",
        "    },",
        "    \"to\": {",
        "      \"name\": string,",
        "      \"phone\": string,",
        "      \"email\": string,",
        "      \"address\": {",
        "        \"country\": string,",
        "        \"state\": string,",
        "        \"region\": string,",
        "        \"city\": string,",
        "        \"postalCode\": string,",
        "        \"streetAndHouse\": string",
        "      }",
        "    },",
        "    \"weight\": number,",
        "    \"date\": string,",
        "    \"contentDescription\": string[]",
        "  }",
        "]",
        "",
        "Rules:",
        "- If multiple labels are detected → return multiple objects in the array.",
        "- Expand abbreviations to full names (e.g., \"St\" → \"Street\").",
        "- Add country codes to all phone numbers.",
        "- If any field is missing or unreadable → set it to null.",
        "- Dates must be ISO format when possible.",
        "- Do NOT include explanations — return JSON only."
    });
            }

            private async Task<List<PackageDetails>> ProcessSingleImageAsync(byte[] imageBytes, int index)
            {
                  try
                  {

                        List<PackageDetails>? result = await _gemini.AnalyzeImageFromBytesAsync(imageBytes, GetPropertiesPrompt());

                        return result ?? [];
                  }
                  catch (Exception ex)
                  {
                        // תפיסת שגיאה נקודתית כדי לא להכשיל את כל שאר התמונות
                        Logger.LogError($"Error analyzing image #{index + 1}: {ex.Message}");
                        return [];
                  }
            }

            // the images are given in groups of 3 (from 3 sides), so we need to analyze them in parallel
            public async Task<List<List<PackageDetails>>> AnalyzeAllImagesAsync(List<byte[]?> imagesToAnalyze)
            {
                  if (imagesToAnalyze == null || !imagesToAnalyze.Any())
                  {
                        Logger.LogInfo("No images to process.");
                        return [];
                  }

                  // יצירת רשימת משימות - כל תמונה נשלחת לפונקציית העיבוד הפרטנית
                  var tasks = imagesToAnalyze
                      .Where(img => img != null && img.Length > 0)
                      .Select((img, index) => ProcessSingleImageAsync(img!, index))
                      .ToList();

                  // המתנה לסיום כל המשימות במקביל
                  var resultsArray = await Task.WhenAll(tasks);

                  // סינון תוצאות ריקות והחזרה כרשימה
                  return resultsArray.Where(res => res != null).ToList();
            }
      }
}
