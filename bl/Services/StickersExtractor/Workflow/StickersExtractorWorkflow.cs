using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CameraAnalyzer.bl.Services.StickersExtractor.MiddleServices;
using CameraAnalyzer.bl.Utils;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace CameraAnalyzer.bl.Services.StickersExtractor.Workflow
{
    public class DetectionResponse
    {
        public string LabelName { get; set; }
        public double Confidence { get; set; }
        public string ImageBase64 { get; set; } // התמונה הגזורה בלבד
    }

    public class StickersExtractorService
    {
        private readonly StickersDetector _stickerDetector;

        public StickersExtractorService(StickersDetector stickerDetector)
        {
            _stickerDetector = stickerDetector ?? throw new ArgumentNullException(nameof(stickerDetector));
        
            
        }

        /// <summary>
        /// מזהה מדבקות בתמונה
        /// </summary>
        /// <param name="imageBytes">רשימת בתים של התמונה</param>
        /// <param name="labelName">שם התווית לזיהוי</param>
        /// <returns>רשימת זיהויים</returns>
        public async Task<List<DetectionResponse>> DetectStickerAsync(byte[] imageBytes, string labelName)
        {
            var startTime = DateTime.Now;
            Logger.LogInfo($"[INFO] Received detection request for label: {labelName}");

            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Image bytes are missing or empty", nameof(imageBytes));
            }

            if (string.IsNullOrEmpty(labelName))
            {
                throw new ArgumentException("labelName is required", nameof(labelName));
            }

            Logger.LogInfo($"[INFO] Image size: {imageBytes.Length} bytes");

            try
            {
                using var inputImage = new Mat();
                // המרת List<byte> ל-array
                byte[] imageArray = [.. imageBytes];
                
                // שימוש ב-Unchanged עבור תמונות MONO/Grayscale
                CvInvoke.Imdecode(imageArray, ImreadModes.Unchanged, inputImage);

                if (inputImage.IsEmpty)
                {
                    throw new InvalidOperationException("Invalid image format");
                }
                // time for detection
                var detectionStartTime = DateTime.Now;
                var detections = _stickerDetector.Detect(inputImage, labelName);
                var detectionTime = DateTime.Now;
                Logger.LogInfo($"[INFO] Detection time: {(detectionTime - startTime).TotalMilliseconds} ms");

                if (detections == null || detections.Count == 0)
                {
                    Logger.LogInfo($"[INFO] No detections found for label: {labelName}");
                    return new List<DetectionResponse>();
                }

                var responseList = new List<DetectionResponse>();

                foreach (var detection in detections)
                {
                    try
                    {
                        // חיתוך ויישור
                        using var alignedLabel = ImageRotator.ExtractAndAlignLabel(inputImage, detection.Corners);
                        
                        // קידוד ל-JPG (שומר על ערוץ אחד ב-MONO)
                        byte[] croppedBytes = CvInvoke.Imencode(".jpg", alignedLabel);

                        responseList.Add(new DetectionResponse
                        {
                            LabelName = detection.LabelName,
                            Confidence = detection.Confidence,
                            ImageBase64 = Convert.ToBase64String(croppedBytes)
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInfo($"[WARNING] Failed to crop detection: {ex.Message}");
                    }
                }

                Logger.LogInfo($"[INFO] Detection completed. Found {responseList.Count} instances of label: {labelName}");
                var endTime = DateTime.Now;
                Logger.LogInfo($"[INFO] Processing time: {(endTime - startTime).TotalMilliseconds} ms");

                return responseList;
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"[ERROR] Detection failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// גרסה סינכרונית של הזיהוי
        /// </summary>
        public List<DetectionResponse> DetectSticker(byte[] imageBytes, string labelName)
        {
            return DetectStickerAsync(imageBytes, labelName).GetAwaiter().GetResult();
        }
    }
}