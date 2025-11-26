// using System;
// using System.IO;
// using System.Net.Http;
// using System.Net.Http.Headers;
// using System.Text;
// using System.Text.Json;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Configuration;

// namespace CameraAnalyzer.bl.APIs
// {
//     public class GeminiAPI
//     {
//         private readonly string _apiKey;
//         private readonly HttpClient _httpClient;
//         private readonly string _apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models";
//         private readonly string _modelName = "gemini-2.0-flash";

//         public GeminiAPI(IConfiguration configuration)
//         {
//             _apiKey = configuration["GeminiAPI:ApiKey"]
//                 ?? throw new InvalidOperationException("Missing Gemini API key in configuration.");

//             _httpClient = new HttpClient();
//             _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
//         }

//         /// <summary>
//         /// Sends a text-only prompt to Gemini and returns its response.
//         /// </summary>
//         public async Task<string?> AskGeminiAsync(string prompt)
//         {
//             if (string.IsNullOrWhiteSpace(prompt))
//                 throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

//             var payload = new
//             {
//                 contents = new[]
//                 {
//                     new
//                     {
//                         parts = new[] { new { text = prompt } }
//                     }
//                 }
//             };

//             return await SendRequestAsync(payload);
//         }

//         /// <summary>
//         /// Sends a prompt and image (in base64) to Gemini for analysis.
//         /// </summary>
//         public async Task<string?> AnalyzeImageAsync(string base64ImageData, string prompt, string mimeType = "image/jpeg")
//         {
//             if (string.IsNullOrWhiteSpace(base64ImageData))
//                 throw new ArgumentException("Image data cannot be empty.", nameof(base64ImageData));

//             var payload = BuildImagePayload(prompt, base64ImageData, mimeType);
//             return await SendRequestAsync(payload);
//         }

//         /// <summary>
//         /// Loads image from local storage, encodes it to base64 and sends it for analysis.
//         /// </summary>
//         public async Task<string?> AnalyzeImageFromStorageAsync(string imagePath, string prompt, string mimeType = "image/jpeg")
//         {
//             if (!File.Exists(imagePath))
//                 throw new FileNotFoundException("Image file not found.", imagePath);

//             string base64Image;
//             await using (var fileStream = File.OpenRead(imagePath))
//             using (var memoryStream = new MemoryStream())
//             {
//                 await fileStream.CopyToAsync(memoryStream);
//                 base64Image = Convert.ToBase64String(memoryStream.ToArray());
//             }

//             var payload = BuildImagePayload(prompt, base64Image, mimeType);
//             return await SendRequestAsync(payload);
//         }

//         // ---------------------- PRIVATE HELPERS ----------------------

//         private object BuildImagePayload(string prompt, string base64ImageData, string mimeType)
//         {
//             return new
//             {
//                 contents = new[]
//                 {
//                     new
//                     {
//                         parts = new object[]
//                         {
//                             new { text = prompt },
//                             new
//                             {
//                                 inlineData = new
//                                 {
//                                     mimeType,
//                                     data = base64ImageData
//                                 }
//                             }
//                         }
//                     }
//                 }
//             };
//         }

//         private async Task<string?> SendRequestAsync(object payload)
//         {
//             var requestUri = $"{_apiEndpoint}/{_modelName}:generateContent?key={_apiKey}";
//             var jsonPayload = JsonSerializer.Serialize(payload);
//             var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

//             try
//             {
//                 var response = await _httpClient.PostAsync(requestUri, content);
//                 response.EnsureSuccessStatusCode();

//                 var responseBody = await response.Content.ReadAsStringAsync();
//                 return ParseResponse(responseBody);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Error calling Gemini API: {ex.Message}");
//                 return null;
//             }
//         }

//         private string? ParseResponse(string responseBody)
//         {
//             using var doc = JsonDocument.Parse(responseBody);

//             if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
//                 candidates.GetArrayLength() == 0)
//             {
//                 Console.WriteLine("Unexpected response structure from Gemini.");
//                 return null;
//             }

//             var text = candidates[0]
//                 .GetProperty("content")
//                 .GetProperty("parts")[0]
//                 .GetProperty("text")
//                 .GetString();

//             if (string.IsNullOrWhiteSpace(text))
//                 return null;

//             text = text.Trim();
//             if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
//                 text = text.Substring(7).Trim();
//             if (text.EndsWith("```"))
//                 text = text.Substring(0, text.Length - 3).Trim();

//             return text;
//         }

//         private string GetPrompt()
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
//     }
// }
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CameraAnalyzer.bl.Utils;
using Microsoft.Extensions.Configuration;

namespace CameraAnalyzer.bl.APIs
{
    public class GeminiAPI
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string ModelName = "gemini-2.0-flash";

        public GeminiAPI(HttpClient httpClient, IConfiguration config)
        {
            _apiKey = config["GeminiAPI:ApiKey"]
                ?? throw new InvalidOperationException("Gemini API key missing.");

            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ===============================================================
        // TEXT-ONLY
        // ===============================================================
        public async Task<string?> AskGeminiAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            return await SendRequestAsync(payload);
        }

        // ===============================================================
        // IMAGE + PROMPT (base64)
        // ===============================================================
        public async Task<string?> AnalyzeImageAsync(
            string base64ImageData, string prompt, string mimeType = "image/jpeg")
        {
            if (string.IsNullOrWhiteSpace(base64ImageData))
                throw new ArgumentException("Image data cannot be empty.");

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType,
                                    data = base64ImageData
                                }
                            }
                        }
                    }
                }
            };

            return await SendRequestAsync(payload);
        }

        // ===============================================================
        // IMAGE FILE → BASE64 → PROMPT
        // ===============================================================
        public async Task<string?> AnalyzeImageFromStorageAsync(
            string imagePath, string prompt, string mimeType = "image/jpeg")
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image not found.", imagePath);

            string base64 = await ImagesProcessing.ConvertImageToBase64(imagePath);

            string? geminiResponse =  await AnalyzeImageAsync(base64, prompt, mimeType);
            if (geminiResponse == null)
            {
                Logger.LogError("Gemini API returned no response.");
                return null;
            }
            return geminiResponse;
        }

        // ===============================================================
        // CORE HTTP CALL
        // ===============================================================
        private async Task<string?> SendRequestAsync(object payload)
        {
            string url = $"{BaseUrl}/{ModelName}:generateContent?key={_apiKey}";

            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);

                var rawResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ Gemini API Error:");
                    Console.WriteLine(rawResponse);
                    return null;
                }

                return ParseResponse(rawResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error calling Gemini API: {ex}");
                return null;
            }
        }

        // ===============================================================
        // PARSE RESPONSE
        // ===============================================================
        private string? ParseResponse(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);

                if (!doc.RootElement.TryGetProperty("candidates", out var candidates))
                    return null;

                if (candidates.GetArrayLength() == 0)
                    return null;

                var parts = candidates[0].GetProperty("content").GetProperty("parts");

                string? resultText = parts[0].GetProperty("text").GetString();

                if (string.IsNullOrWhiteSpace(resultText))
                    return null;

                // Remove Markdown wrappers if model wraps JSON in ```json ```
                string text = resultText.Trim();

                if (text.StartsWith("```json"))
                    text = text.Substring(7).Trim();
                if (text.EndsWith("```"))
                    text = text.Substring(0, text.Length - 3).Trim();

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Failed to parse Gemini JSON: " + ex.Message);
                return null;
            }
        }

        // ===============================================================
        // Example prompt (unused here)
        // ===============================================================
        private string GetPrompt()
        {
            string[] lines =
            {
                "Analyze this shipping label image.",
                "Extract 'ship to' and 'ship from' details as JSON.",
                "If multiple labels exist, return an array.",
                "Missing values should be null.",
                "Return JSON only."
            };
            return string.Join("\n", lines);
        }
    }
}
