using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices
{
    public class DetectionService
    {
        private readonly AiDetectorAPI _aiDetector;
        private readonly bool _isDebug;
        private readonly string _debugFolder = "DebugDetections"; // שם התיקייה לשמירה

        // הוספנו פרמטר isDebug לקונסטרקטור
        public DetectionService(AiDetectorAPI aiDetector, bool isDebug = true)
        {
            _aiDetector = aiDetector;
            _isDebug = isDebug;

            if (_isDebug && !Directory.Exists(_debugFolder))
            {
                Directory.CreateDirectory(_debugFolder);
            }
        }

        public async Task<List<byte[]?>> DetectPackagesAsync(string imagePath, string labelsCompany)
        {
            Logger.LogInfo("Detecting packages...");

            List<DetectionResult> detectionResults = await _aiDetector.DetectStickers(imagePath, labelsCompany);

            if (detectionResults == null || !detectionResults.Any())
            {
                Logger.LogInfo("No bounding boxes found.");
                return null;
            }

            // חילוץ מערכי הבייטים
            List<byte[]> packageBytes = detectionResults
                .Select(res => res.GetImageBytes())
                .Where(b => b != null)
                .ToList()!;

            // אם מצב דיבאג פעיל - נשמור את התמונות לדיסק
            if (_isDebug)
            {
                await SaveImagesToDiskAsync(packageBytes);
            }

            Logger.LogInfo($"Successfully extracted {packageBytes.Count} images as byte arrays.");

            return packageBytes;
        }

        private async Task SaveImagesToDiskAsync(List<byte[]> images)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            for (int i = 0; i < images.Count; i++)
            {
                // יצירת נתיב תואם Linux/Windows
                string fileName = $"detection_{timestamp}_{i}.jpg";
                string filePath = Path.Combine(_debugFolder, fileName);

                try
                {
                    await File.WriteAllBytesAsync(filePath, images[i]);
                    Logger.LogInfo($"[DEBUG] Saved image to: {filePath}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[DEBUG] Failed to save image: {ex.Message}");
                }
            }
        }
    }
}