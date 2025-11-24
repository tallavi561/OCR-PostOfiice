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

        public AiDetectorAPI()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5000")
            };
        }


        public async Task<List<BoundingBox>> Detect(string imagePath)
        {
            Logger.LogInfo("Converting image to Base64...");
            string base64Image = await ImagesProcessing.ConvertImageToBase64(imagePath);
            var requestJson = new
            {
                image = base64Image
            };

            Logger.LogInfo("Sending image to AI Detector API url" + _http.BaseAddress + "/api/v1/detectBase64Image");
            var content = new StringContent(
                JsonSerializer.Serialize(requestJson),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.PostAsync("/api/v1/detectBase64Image", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Server returned error: {response.StatusCode}");
            }

            using var stream = await response.Content.ReadAsStreamAsync();

            var serverResponse = await JsonSerializer.DeserializeAsync<DetectResponse>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

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
