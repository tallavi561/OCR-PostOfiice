using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly string _projectId;
        private readonly string _location = "europe-west1";
        private readonly string _apiUrl;
        private readonly GoogleCredential _credential;
        private const string ModelName = "gemini-2.0-flash";

        public GeminiAPI(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _projectId = config["GoogleCloud:ProjectId"] ?? "aol-services";

            // בניית ה-URL פעם אחת ב-Constructor
            _apiUrl = $"https://{_location}-aiplatform.googleapis.com/v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{ModelName}:streamGenerateContent";

            // יצירת ה-Credential פעם אחת בלבד
            var jsonSection = config.GetSection("GoogleCloud:ServiceAccountJson");
            var keyValues = jsonSection.GetChildren().ToDictionary(c => c.Key, c => c.Value);
            string jsonContent = JsonSerializer.Serialize(keyValues);

            _credential = GoogleCredential.FromJson(jsonContent)
                                         .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        }

        /// <summary>
        /// מקבל Access Token מ-Google Credentials.
        /// הספרייה מטמנת את ה-Token ומחדשת אותו אוטומטית במידת הצורך.
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            return await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        }

        /// <summary>
        /// מנתח תמונה מ-Base64 עם Prompt
        /// </summary>
        private async Task<List<PackageDetails>?> InnerAnalyzeImageAsync(
            string base64ImageData, string prompt, string mimeType = "image/jpeg")
        {
            if (string.IsNullOrWhiteSpace(base64ImageData))
                throw new ArgumentException("Image data cannot be empty.", nameof(base64ImageData));

            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

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

        /// <summary>
        /// מנתח תמונה מ-Bytes עם Prompt
        /// </summary>
        public async Task<List<PackageDetails>?> AnalyzeImageFromBytesAsync(
            byte[] imageToAnalyze, string prompt, string mimeType = "image/jpeg")
        {
            if (imageToAnalyze == null || imageToAnalyze.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty.", nameof(imageToAnalyze));

            var base64 = ImagesProcessing.ConvertImageToBase64(imageToAnalyze);

            List<PackageDetails>? geminiResponse = await InnerAnalyzeImageAsync(base64, prompt, mimeType);
            
            if (geminiResponse == null)
            {
                Logger.LogWarning("Gemini API returned no response.");
                return null;
            }

            return geminiResponse;
        }

        /// <summary>
        /// שולח בקשה ל-Gemini API - Thread-Safe למקביליות
        /// </summary>
        private async Task<List<PackageDetails>?> SendRequestAsync(object payload)
        {
            Logger.LogInfo("Building Gemini API request...");

            try
            {
                // קבלת Access Token
                string accessToken = await GetAccessTokenAsync();

                // הכנת ה-Payload
                string jsonPayload = JsonSerializer.Serialize(payload);

                // יצירת Request עם כל המידע - Thread-Safe
                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                {
                    Headers = 
                    { 
                        Authorization = new AuthenticationHeaderValue("Bearer", accessToken) 
                    },
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                Logger.LogInfo("Sending request to Gemini API...");

                // שליחת הבקשה
                var response = await _httpClient.SendAsync(request);
                var rawResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError($"❌ Gemini API Error ({response.StatusCode}):");
                    Logger.LogError(rawResponse);
                    return null;
                }

                Logger.LogInfo("Received response from Gemini API. Parsing...");
                return ParseResponse(rawResponse);
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError($"❌ HTTP Error calling Gemini API: {httpEx.Message}");
                return null;
            }
            catch (TaskCanceledException timeoutEx)
            {
                Logger.LogError($"❌ Timeout calling Gemini API: {timeoutEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ Unexpected error calling Gemini API: {ex}");
                return null;
            }
        }

        /// <summary>
        /// מפרסר את התגובה מ-Gemini API
        /// </summary>
        private List<PackageDetails>? ParseResponse(string rawJson)
        {
            Logger.LogInfo("Parsing Gemini API response...");

            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                StringBuilder fullText = new StringBuilder();

                // איסוף כל הטקסט מהתגובה
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textProperty))
                            {
                                fullText.Append(textProperty.GetString());
                            }
                        }
                    }
                }

                string text = fullText.ToString().Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.LogWarning("Gemini response contained no text content.");
                    return null;
                }

                // ניקוי Markdown wrappers
                text = CleanMarkdownWrappers(text);

                // Deserialize ישירות למערך של PackageDetails
                var result = JsonSerializer.Deserialize<List<PackageDetails>>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Logger.LogInfo($"✅ Parsed {result?.Count ?? 0} packages from Gemini response.");
                return result;
            }
            catch (JsonException jsonEx)
            {
                Logger.LogError($"❌ Failed to parse Gemini JSON: {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ Unexpected error parsing Gemini response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// מנקה Markdown code blocks מסביב ל-JSON
        /// </summary>
        private string CleanMarkdownWrappers(string text)
        {
            // הסרת ```json...```
            if (text.Contains("```json"))
            {
                var parts = text.Split(new[] { "```json" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    text = parts[1].Split(new[] { "```" }, StringSplitOptions.None)[0];
                }
            }
            // הסרת ```...```
            else if (text.Contains("```"))
            {
                var parts = text.Split(new[] { "```" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    text = parts[1].Split(new[] { "```" }, StringSplitOptions.None)[0];
                }
            }

            return text.Trim();
        }
    }
}