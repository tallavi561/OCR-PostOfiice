using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace CameraAnalyzer.bl.Utils
{
    public static class ImagesProcessing
    {
        public static void CropAndSaveImage(int X1, int Y1, int X2, int Y2, string originalFilePath, string newFilePath, float confidence = 1.0f)
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
                    // Logger.LogInfo($"Cropped image saved successfully: {newFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"CropAndSaveImage failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load image and convert to Base64 string.
        /// </summary>
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
    }
}
