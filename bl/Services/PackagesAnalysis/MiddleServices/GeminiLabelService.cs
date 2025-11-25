using CameraAnalyzer.bl.APIs;
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

            private string GetPrompt()
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


            public async Task<List<string>> AnalyzeAllAsync(List<string> cropPaths)
            {
                  var tasks = cropPaths.Select(async path =>
                  {
                        try
                        {
                              var result = await _gemini.AnalyzeImageFromStorageAsync(path, GetPrompt());
                              return result;
                        }
                        catch (Exception ex)
                        {
                              Logger.LogError("Gemini failed for " + path + ": " + ex.Message);
                              return null;
                        }
                  });

                  var rawResults = await Task.WhenAll(tasks);

                  List<string> cleaned = rawResults
                      .Where(r => r is not null)
                      .Select(r => r!)         // ה־! אומר לקומפיילר: בשלב הזה זה בטוח לא null
                      .ToList();

                  return cleaned;
            }
      }
}
