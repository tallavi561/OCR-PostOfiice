using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.MiddleServices
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
                "Analyze this shipping label image.",
                "Extract the 'ship to' and 'ship from' details as JSON objects.",
                "If multiple labels are detected, return an array.",
                "Fill missing data with null.",
                "Expand abbreviations to full names.",
                "Add country codes to phone numbers."
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
