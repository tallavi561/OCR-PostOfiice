using CameraAnalyzer.bl.Utils;
using Microsoft.Extensions.Options;

namespace CameraAnalyzer.bl.Services.CompanyName
{
      public class LabelsCompanyOptions
      {
            public string BaseUrl { get; set; } = string.Empty;
      }
      public interface ICompanyNameService
      {
            Task<string> GetCompanyNameAsync();
      }

      public class CompanyNameService : ICompanyNameService
      {
            private readonly HttpClient _httpClient;

            public CompanyNameService(HttpClient httpClient, IOptions<LabelsCompanyOptions> options)
            {
                  // הגדרת כתובת הבסיס מתוך הקונפיגורציה
                  _httpClient = httpClient;
                  _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
                  Logger.LogInfo($"CompanyNameService initialized with BaseUrl: {options.Value.BaseUrl}");
            }

            public async Task<string> GetCompanyNameAsync()
            {
                  try
                  {
                        return await _httpClient.GetStringAsync("getCompanyName");
                  }
                  catch (HttpRequestException ex)
                  {
                        Logger.LogError($"HTTP Request Error: {ex.Message}");
                        return $"Error: {ex.Message}";
                  }
            }
      }

}