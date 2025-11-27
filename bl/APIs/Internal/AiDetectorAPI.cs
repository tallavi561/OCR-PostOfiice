using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.APIs
{

    public class AiDetectorAPI
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        // HttpClient is injected by IHttpClientFactory
        public AiDetectorAPI(HttpClient http, IConfiguration config)
        {
            _http = http;

            _baseUrl = config["AiDetectorAPI:BaseUrl"]
                ?? throw new InvalidOperationException("AiDetectorAPI BaseUrl missing.");

            _http.BaseAddress = new Uri(_baseUrl);
        }

        public async Task<List<BoundingBox>> Detect(string imagePath)
        {
            Logger.LogInfo("Converting image to Base64...");
            string base64Image = await ImagesProcessing.ConvertImageToBase64(imagePath);

            var payload = new { image = base64Image };

            Logger.LogInfo("Sending image to AI Detector API: " +
                           _http.BaseAddress + "api/v1/detectBase64Image");

            var response = await _http.PostAsJsonAsync(
                "/api/v1/detectBase64Image", payload
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Server returned error: {response.StatusCode}");
            }

            var serverResponse = await response.Content.ReadFromJsonAsync<DetectResponse>();

            if (serverResponse?.Detections == null)
                return new List<BoundingBox>();

            Logger.LogInfo($"Received {serverResponse.Detections.Count} detections from AI Detector API.");

            return serverResponse.Detections;
        }
    }

    public class DetectResponse
    {
        public List<BoundingBox> Detections { get; set; }
        public string Message { get; set; }
    }
}
