using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices
{
    public class CroppingService
    {
        public List<string> CropAll(string originalImage, List<BoundingBox> boxes)
        {
            var cropped = new List<string>();

            foreach (var box in boxes)
            {
                string newFile = $"./cropped_outputs/crop_{Guid.NewGuid()}.png";
                ImagesProcessing.CropAndSaveImage(box.X1, box.Y1, box.X2, box.Y2, originalImage, newFile, box.Confidence);
                cropped.Add(newFile);
            }

            return cropped;
        }
    }
}
