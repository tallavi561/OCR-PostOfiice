using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using CameraAnalyzer.bl.Models;

namespace CameraAnalyzer.bl.Utils
{
    public static class ImagesProcessing
    {
        // ----------------------------------------------------
        // CROP IMAGE
        // ----------------------------------------------------
        public static void CropAndSaveImage(
            int X1, int Y1, int X2, int Y2,
            string originalFilePath,
            string newFilePath,
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

                    X1 = Math.Clamp(X1, 0, imgW - 1);
                    Y1 = Math.Clamp(Y1, 0, imgH - 1);
                    X2 = Math.Clamp(X2, 0, imgW - 1);
                    Y2 = Math.Clamp(Y2, 0, imgH - 1);

                    if (X2 <= X1 || Y2 <= Y1)
                    {
                        Logger.LogError($"Invalid crop box after clamping ({X1},{Y1},{X2},{Y2}).");
                        return;
                    }

                    var cropRect = new Rectangle(X1, Y1, X2 - X1, Y2 - Y1);

                    image.Mutate(ctx => ctx.Crop(cropRect));

                    string? dir = Path.GetDirectoryName(newFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    image.Save(newFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CropAndSaveImage failed: {ex.Message}");
            }
        }

        // ----------------------------------------------------
        // IMAGE TO BASE64
        // ----------------------------------------------------
        public static async Task<string> ConvertImageToBase64(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            await using var fs = File.OpenRead(imagePath);
            await using var ms = new MemoryStream();

            await fs.CopyToAsync(ms);

            return Convert.ToBase64String(ms.ToArray());
        }

        // ----------------------------------------------------
        // DRAW BOUNDING BOXES + CLASS NAME
        // ----------------------------------------------------
        public static void DrawBoundingBoxes(
            List<BoundingBox> boxes,
            string originalFilePath,
            string outputFilePath,
            string fontPath = "fonts/OpenSans-Regular.ttf")
        {
            try
            {
                if (!File.Exists(originalFilePath))
                {
                    Logger.LogError($"Original image not found: {originalFilePath}");
                    return;
                }

                // ----- LOAD FONT -----
                FontFamily fontFamily;
                var fontCollection = new FontCollection();

                if (File.Exists(fontPath))
                {
                    fontFamily = fontCollection.Add(fontPath);
                }
                else
                {
                    Logger.LogWarning($"Font not found: {fontPath}. Using first system font.");
                    fontFamily = SystemFonts.Families.First();
                }

                Font font = fontFamily.CreateFont(14, FontStyle.Regular);

                var pen = Pens.Solid(Color.Red, 2);
                var textBrush = Brushes.Solid(Color.White);
                var bgBrush = Brushes.Solid(Color.Red);

                using (Image<Rgba32> image = Image.Load<Rgba32>(originalFilePath))
                {
                    int imgW = image.Width;
                    int imgH = image.Height;

                    foreach (var box in boxes)
                    {
                        int x1 = Math.Clamp(box.X1, 0, imgW - 1);
                        int y1 = Math.Clamp(box.Y1, 0, imgH - 1);
                        int x2 = Math.Clamp(box.X2, 0, imgW - 1);
                        int y2 = Math.Clamp(box.Y2, 0, imgH - 1);

                        int width = x2 - x1;
                        int height = y2 - y1;

                        if (width <= 0 || height <= 0)
                            continue;

                        var rect = new Rectangle(x1, y1, width, height);

                        image.Mutate(ctx =>
                        {
                            // Draw bounding box
                            ctx.Draw(pen, rect);

                            if (!string.IsNullOrWhiteSpace(box.ClassName))
                            {
                                string text = box.ClassName;

                                // ------- TEXT SIZE (MeasureBounds works in all versions!) --------
                                var textOptions = new TextOptions(font);
                                FontRectangle textSize = TextMeasurer.MeasureBounds(text, textOptions);

                                var textLocation = new PointF(
                                    x1,
                                    Math.Max(0, y1 - textSize.Height - 4)
                                );

                                // Background for text
                                ctx.Fill(bgBrush, new RectangleF(
                                    textLocation.X,
                                    textLocation.Y,
                                    textSize.Width + 6,
                                    textSize.Height + 4));

                                // Draw text
                                ctx.DrawText(text, font, textBrush, textLocation);
                            }
                        });
                    }

                    string? dir = Path.GetDirectoryName(outputFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    image.Save(outputFilePath);

                    Logger.LogInfo($"Bounding boxes drawn: {outputFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"DrawBoundingBoxes failed: {ex.Message}");
            }
        }
    }
}
