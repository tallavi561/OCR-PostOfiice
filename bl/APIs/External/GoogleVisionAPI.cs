using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CameraAnalyzer.bl.Utils;
using Microsoft.Extensions.Configuration;

namespace CameraAnalyzer.bl.APIs
{
    public class GoogleVisionAPI
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string Endpoint = "https://vision.googleapis.com/v1/images:annotate";

        public GoogleVisionAPI(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleVisionAPI:ApiKey"];

            if (string.IsNullOrEmpty(_apiKey))
                Logger.LogWarning("Google Vision API key missing!");
            else
                Logger.LogInfo("Google Vision API initialized successfully.");
        }

        /// <summary>
        /// Sends an image to Google Vision API for OBJECT_LOCALIZATION and returns raw JSON.
        /// </summary>
        public async Task<string> AnalyzeImageAsync(string imagePath, string prompt)
        {
            try
            {
                // 1. Validate image path
                if (!File.Exists(imagePath))
                {
                    string msg = $"Image file not found: {imagePath}";
                    Logger.LogError(msg);
                    return JsonError(msg);
                }

                // 2. Convert image to Base64
                string base64Image = await ImagesProcessing.ConvertImageToBase64(imagePath);

                Logger.LogInfo($"Sending '{Path.GetFileName(imagePath)}' to Google Vision API...");
                Logger.LogInfo("Prompt (ignored by Vision API): " + prompt);

                // 3. Build request body EXACTLY in the format Google Vision expects
                var requestBody = new
                {
                    requests = new[]
                    {
                        new
                        {
                            image = new { content = base64Image },
                            features = new[]
                            {
                                new { type = "OBJECT_LOCALIZATION", maxResults = 50 }
                            }
                        }
                    }
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                // 4. Send request
                Logger.LogInfo("Calling Google Vision API...");
                var response = await _httpClient.PostAsync($"{Endpoint}?key={_apiKey}", httpContent);

                string rawJson = await response.Content.ReadAsStringAsync();

                // 5. Handle error responses
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError("Google Vision API ERROR:");
                    Logger.LogError(rawJson);
                    return rawJson;
                }

                // 6. Success
                Logger.LogInfo("Google Vision API response received successfully.");
                Logger.LogInfo("RAW RESPONSE: " + rawJson);

                return rawJson;
            }
            catch (Exception ex)
            {
                Logger.LogError("Unexpected error while calling Google Vision API: " + ex);
                return JsonError(ex.Message);
            }
        }

        private string JsonError(string message)
        {
            return $"{{\"error\":\"{message.Replace("\"", "")}\"}}";
        }
    }
}
