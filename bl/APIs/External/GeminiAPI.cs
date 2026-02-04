using Google.Apis.Auth.OAuth2;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CameraAnalyzer.bl.Utils;
using Microsoft.Extensions.Configuration;
using CameraAnalyzer.bl.Models;

namespace CameraAnalyzer.bl.APIs
{
    public class GeminiAPI
    {
        private readonly HttpClient _httpClient;
        private readonly string _jsonContent;
        private readonly string _projectId;
        private readonly string _location = "europe-west1";
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string ModelName = "gemini-2.0-flash";

        public GeminiAPI(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;

            // שליפת ה-ID של הפרויקט
            _projectId = config["GoogleCloud:ProjectId"] ?? "aol-services";

            // שליפת הנתונים מתוך הסקשן ובניית מילון שטוח
            var jsonSection = config.GetSection("GoogleCloud:ServiceAccountJson");
            var keyValues = new Dictionary<string, string>();

            foreach (var child in jsonSection.GetChildren())
            {
                if (child.Value != null)
                {
                    keyValues[child.Key] = child.Value;
                }
            }

            // יצירת מחרוזת JSON תקנית עבור GoogleCredential
            _jsonContent = JsonSerializer.Serialize(keyValues);
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // יצירת ה-Credential ישירות מהמחרוזת (במקום קובץ)
            var credential = GoogleCredential.FromJson(_jsonContent)
                                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            return token;
        }

        // ===============================================================
        // TEXT-ONLY
        // ===============================================================
        // public async Task<string?> AskGeminiAsync(string prompt)
        // {
        //     if (string.IsNullOrWhiteSpace(prompt))
        //         throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        //     var payload = new
        //     {
        //         contents = new[]
        //         {
        //             new
        //             {
        //                 role = "user",
        //                 parts = new[]
        //                 {
        //                     new { text = prompt }
        //                 }
        //             }
        //         }
        //     };

        //     return await SendRequestAsync(payload);
        // }

        // ===============================================================
        // IMAGE + PROMPT (base64)
        // ===============================================================
        public async Task<List<PackageDetails>?> AnalyzeImageAsync(
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
                        role = "user",
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
        public async Task<List<PackageDetails>?> AnalyzeImageFromBytesAsync(
            byte[] imageToAnalyze, string prompt, string mimeType = "image/jpeg")
        {


            var base64 = ImagesProcessing.ConvertImageToBase64(imageToAnalyze);

            List<PackageDetails>? geminiResponse = await AnalyzeImageAsync(base64, prompt, mimeType);
            if (geminiResponse == null)
            {
                Logger.LogWarning("Gemini API returned no response.");
                return null;
            }
            return geminiResponse;
        }

        // ===============================================================
        // CORE HTTP CALL
        // ===============================================================
        private async Task<List<PackageDetails>?> SendRequestAsync(object payload)
        {
            // 1. הפקת ה-Token מה-JSON (השתמש בפונקציה שכבר כתבת)
            string accessToken = await GetAccessTokenAsync();

            // 2. בניית ה-URL עבור Vertex AI (שים לב לפורמט השונה)
            // הערה: וודא שה-Location מתאים למה שהגדרת (למשל us-central1)
            string url = $"https://{_location}-aiplatform.googleapis.com/v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{ModelName}:streamGenerateContent";

            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // 3. הוספת האימות ל-Header (במקום ה-Key ב-URL)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var rawResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError("❌ Gemini API Error:");
                    Logger.LogError(rawResponse);
                    return null;
                }

                return ParseResponse(rawResponse);
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ Error calling Gemini API: {ex}");
                return null;
            }
        }

        // ===============================================================
        // PARSE RESPONSE
        // ===============================================================
        private List<PackageDetails>? ParseResponse(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                StringBuilder fullText = new StringBuilder();

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        foreach (var part in parts.EnumerateArray())
                        {
                            fullText.Append(part.GetProperty("text").GetString());
                        }
                    }
                }

                string text = fullText.ToString().Trim();

                // ניקוי Markdown wrappers
                if (text.Contains("```json"))
                {
                    text = text.Split("```json")[1].Split("```")[0];
                }
                else if (text.Contains("```"))
                {
                    text = text.Split("```")[1].Split("```")[0];
                }

                text = text.Trim();

                // ✅ Deserialize ישירות למערך של PackageDetails
                var result = JsonSerializer.Deserialize<List<PackageDetails>>(text);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ Failed to parse Gemini JSON: {ex.Message}");
                return null;
            }
        }


    }
}
