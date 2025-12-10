using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
// using SixLabors.ImageSharp.Drawing;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;
using static System.Net.Mime.MediaTypeNames;

namespace CameraAnalyzer.bl.Utils
{
    public static class ImagesProcessing
    {
        // Crop image given coordinates and save to new file.
        public static void CropAndSaveImage(
            int X1, int Y1, int X2, int Y2,
            string originalFilePath, string newFilePath,
            bool saveMarkedImage = false)
        {
            try
            {
                if (!File.Exists(originalFilePath))
                {
                    Logger.LogError($"Original image not found: {originalFilePath}");
                    return;
                }

                using (Image<Rgba32> image = Image.Load<Rgba32>(originalFilePath))
                {
                    int imgW = image.Width;
                    int imgH = image.Height;

                    // Clamp coordinates safely
                    X1 = Math.Clamp(X1, 0, imgW - 1);
                    Y1 = Math.Clamp(Y1, 0, imgH - 1);
                    X2 = Math.Clamp(X2, 0, imgW);
                    Y2 = Math.Clamp(Y2, 0, imgH);

                    // Ensure X1 < X2, Y1 < Y2
                    if (X2 <= X1 || Y2 <= Y1)
                    {
                        Logger.LogError($"Invalid crop box after clamping ({X1},{Y1},{X2},{Y2}).");
                        return;
                    }

                    int width = X2 - X1;
                    int height = Y2 - Y1;

                    var cropRectangle = new Rectangle(X1, Y1, width, height);
                    image.Mutate(ctx => ctx.Crop(cropRectangle));

                    string? directory = Path.GetDirectoryName(newFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    image.Save(newFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CropAndSaveImage failed: {ex.Message}");
            }
        }

        /// Load image and convert to Base64 string.
        public static async Task<string> ConvertImageToBase64(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            await using var fileStream = File.OpenRead(imagePath);
            await using var memoryStream = new MemoryStream();

            await fileStream.CopyToAsync(memoryStream);
            string base64Image = Convert.ToBase64String(memoryStream.ToArray());

            return base64Image;
        }

        // ------------------------- NEW FUNCTION ---------------------------

        // <summary>
        // Draw bounding boxes on an image and save to output path.
        // </summary>
        public static void DrawBoundingBoxes(
            List<BoundingBox> boxes,
            string originalFilePath,
            string outputFilePath)
        {
            try
            {
                if (!File.Exists(originalFilePath))
                {
                    Logger.LogError($"Original image not found: {originalFilePath}");
                    return;
                }

                using (Image<Rgba32> image = Image.Load<Rgba32>(originalFilePath))
                {
                    int imgW = image.Width;
                    int imgH = image.Height;

                    foreach (var box in boxes)
                    {
                        // Clamp coordinates safely
                        int x1 = Math.Clamp(box.X1, 0, imgW - 1);
                        int y1 = Math.Clamp(box.Y1, 0, imgH - 1);
                        int x2 = Math.Clamp(box.X2, 0, imgW - 1);
                        int y2 = Math.Clamp(box.Y2, 0, imgH - 1);

                        int width = x2 - x1;
                        int height = y2 - y1;

                        if (width <= 0 || height <= 0)
                            continue;

                        var rect = new Rectangle(x1, y1, width, height);

                        // Draw with thickness = 4px
                        image.Mutate(ctx =>
                        {
                            ctx.Draw(SixLabors.ImageSharp.Color.Red, 4, rect);
                        });
                    }

                    string? directory = Path.GetDirectoryName(outputFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    image.Save(outputFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"DrawBoundingBoxes failed: {ex.Message}");
            }
        }
        // public static void DrawBoundingBoxes(
        //     List<BoundingBox> boxes,
        //     string originalFilePath,
        //     string outputFilePath,
        //     string fontPath = "fonts/OpenSans-Regular.ttf") // נניח שהנתיב הזה נכון מתיקיית הפרויקט
        // {
        //     try
        //     {
        //         if (!File.Exists(originalFilePath))
        //         {
        //             Logger.LogError($"Original image not found: {originalFilePath}");
        //             return;
        //         }

        //         // 1. טעינת הגופן
        //         FontFamily fontFamily;
        //         if (File.Exists(fontPath))
        //         {
        //             // טוען את הגופן מנתיב ספציפי
        //             fontFamily = SixLabors.Fonts.SystemFonts.Get  // נשתמש ב-SystemFonts.Collection.Add למקרה שאין SystemFonts.Find
        //                  .Add(fontPath);
        //         }
        //         else
        //         {
        //             Logger.LogWarning($"Font file not found: {fontPath}. Falling back to default.");
        //             // מנסה למצוא גופן ברירת מחדל אם הקובץ לא נמצא
        //             if (SixLabors.Fonts.SystemFonts.TryFind("Arial", out FontFamily arialFamily))
        //             {
        //                 fontFamily = arialFamily;
        //             }
        //             else
        //             {
        //                 Logger.LogError("No suitable font found. Cannot draw text labels.");
        //                 // אם אין גופן, אנו עדיין יכולים לצייר את הריבועים
        //                 fontFamily = null;
        //             }
        //         }

        //         Font font = fontFamily?.CreateFont(12, SixLabors.Fonts.FontStyle.Regular);

        //         // הגדרת מברשות וצבעים
        //         var boxColor = Color.Red;
        //         var textColor = Color.White;
        //         var textBackgroundColor = Color.Red; // רקע טקסט שחור לנוחות קריאה
        //         var textBrush = Brushes.Solid(textColor);
        //         var textBackgroundBrush = Brushes.Solid(textBackgroundColor);
        //         var pen = Pens.Solid(boxColor, 2); // עובי הקו של הריבוע

        //         using (Image<Rgba32> image = Image.Load<Rgba32>(originalFilePath))
        //         {
        //             int imgW = image.Width;
        //             int imgH = image.Height;

        //             foreach (var box in boxes)
        //             {
        //                 // ... (Clamping and dimensions check remains the same)
        //                 int x1 = Math.Clamp(box.X1, 0, imgW - 1);
        //                 int y1 = Math.Clamp(box.Y1, 0, imgH - 1);
        //                 int x2 = Math.Clamp(box.X2, 0, imgW - 1);
        //                 int y2 = Math.Clamp(box.Y2, 0, imgH - 1);

        //                 int width = x2 - x1;
        //                 int height = y2 - y1;

        //                 if (width <= 0 || height <= 0)
        //                     continue;

        //                 var rect = new Rectangle(x1, y1, width, height);

        //                 image.Mutate(ctx =>
        //                 {
        //                     // 2. ציור הריבוע (Bounding Box)
        //                     ctx.Draw(pen, rect);

        //                     // 3. ציור שם המחלקה (Class Name)
        //                     if (font != null && !string.IsNullOrEmpty(box.ClassName))
        //                     {
        //                         string text = box.ClassName;
        //                         // מיקום הטקסט - מעט מעל הריבוע
        //                         var textLocation = new PointF(x1, y1 - 15);

        //                         // מדידת הטקסט עבור ציור רקע
        //                         FontRectangle size = TextMeasurer.Measure(text, new TextOptions(font));

        //                         // ציור מלבן רקע לטקסט (כדי שיבלוט מעל התמונה)
        //                         ctx.Fill(textBackgroundBrush, new RectangleF(
        //                             textLocation.X,
        //                             textLocation.Y,
        //                             size.Width + 4, // פדינג קטן
        //                             size.Height + 2));

        //                         // ציור הטקסט
        //                         ctx.DrawText(text, font, textBrush, textLocation);
        //                     }
        //                 });
        //             }

        //             // ... (Directory check and save remains the same)
        //             string? directory = Path.GetDirectoryName(outputFilePath);
        //             if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        //                 Directory.CreateDirectory(directory);

        //             image.Save(outputFilePath);
        //             Logger.LogInfo($"Bounding boxes and labels drawn to: {outputFilePath}");
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.LogError($"DrawBoundingBoxes failed: {ex.Message}");
        //     }
        // }
    }
}
