using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;
using System.Text.Json.Serialization;
using CameraAnalyzer.bl.Services.FtpPolling;


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



        public async Task<List<DetectionResult>> DetectStickers(ImageFromFtp imagesFromFTP, string labelName)
        {
            using var form = new MultipartFormDataContent();
            var fileBytes = imagesFromFTP.ImageBytes;
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

            form.Add(fileContent, "image", Path.GetFileName(imagesFromFTP.ImageName));
            form.Add(new StringContent(labelName), "labelName");

            var response = await _http.PostAsync("api/detectSticker/v1", form);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Error: {error}");
            }

            // קריאת רשימת התוצאות
            var results = await response.Content.ReadFromJsonAsync<List<DetectionResult>>();
            return results ?? new List<DetectionResult>();
        }
    }
}


