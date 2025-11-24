using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CameraAnalyzer.bl.APIs
{
    public class YoloAPI
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;

        public YoloAPI(string modelPath)
        {
            _session = new InferenceSession(modelPath);
            _inputName = _session.InputMetadata.Keys.First();

            Logger.LogInfo($"YOLO → Loaded model: {modelPath}");
        }

        // --------------------------------------------------------
        public List<BoundingBox> Detect(string imagePath, float confThreshold = 0.25f)
        {
            Logger.LogInfo("Starting detect " + imagePath);
            using var img = Image.Load<Rgb24>(imagePath);

            int origW = img.Width;
            int origH = img.Height;

            var (tensor, ratio, padX, padY) = Preprocess(img);

            var inputs = new[]
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor)
            };

            var outputs = _session.Run(inputs);
            var out0 = outputs.First().AsTensor<float>();

            var boxes = ParseOutput(out0, confThreshold, ratio, padX, padY, origW, origH);
            // Logger.LogInfo(boxe)
            return NMS(boxes, 0.5f);
        }

        // --------------------------------------------------------
        private List<BoundingBox> ParseOutput(
            Tensor<float> output,
            float threshold,
            float ratio,
            float padX,
            float padY,
            int origW,
            int origH)
        {
            var results = new List<BoundingBox>();
            int count = output.Dimensions[1];

            for (int i = 0; i < count; i++)
            {
                float x1 = output[0, i, 0];
                float y1 = output[0, i, 1];
                float x2 = output[0, i, 2];
                float y2 = output[0, i, 3];
                float conf = output[0, i, 4];

                if (conf < threshold)
                    continue;

                // Reverse letterbox
                x1 = (x1 - padX) / ratio;
                y1 = (y1 - padY) / ratio;
                x2 = (x2 - padX) / ratio;
                y2 = (y2 - padY) / ratio;

                // Clamp
                x1 = Math.Clamp(x1, 0, origW - 1);
                y1 = Math.Clamp(y1, 0, origH - 1);
                x2 = Math.Clamp(x2, 0, origW - 1);
                y2 = Math.Clamp(y2, 0, origH - 1);

                results.Add(new BoundingBox
                {
                    Confidence = conf,
                    X1 = (int)x1,
                    Y1 = (int)y1,
                    X2 = (int)x2,
                    Y2 = (int)y2
                });
            }

            return results;
        }

        // --------------------------------------------------------
        private (DenseTensor<float>, float, float, float) Preprocess(Image<Rgb24> img)
        {
            const int size = 640;

            float ratio = Math.Min((float)size / img.Width, (float)size / img.Height);

            int newW = (int)(img.Width * ratio);
            int newH = (int)(img.Height * ratio);

            int padX = (size - newW) / 2;
            int padY = (size - newH) / 2;

            var resized = img.Clone(ctx => ctx.Resize(newW, newH));

            var canvas = new Image<Rgb24>(size, size);
            canvas.Mutate(x => x.BackgroundColor(new Rgb24(114, 114, 114)));
            canvas.Mutate(x => x.DrawImage(resized, new Point(padX, padY), 1f));

            var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });

            canvas.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < size; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < size; x++)
                    {
                        tensor[0, 0, y, x] = row[x].R / 255f;
                        tensor[0, 1, y, x] = row[x].G / 255f;
                        tensor[0, 2, y, x] = row[x].B / 255f;
                    }
                }
            });

            return (tensor, ratio, padX, padY);
        }

        // --------------------------------------------------------
        private List<BoundingBox> NMS(List<BoundingBox> boxes, float iouThreshold)
        {
            var result = new List<BoundingBox>();
            var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                result.Add(best);
                sorted.RemoveAt(0);

                sorted = sorted
                    .Where(b => IoU(best, b) < iouThreshold)
                    .ToList();
            }

            return result;
        }

        private float IoU(BoundingBox a, BoundingBox b)
        {
            int x1 = Math.Max(a.X1, b.X1);
            int y1 = Math.Max(a.Y1, b.Y1);
            int x2 = Math.Min(a.X2, b.X2);
            int y2 = Math.Min(a.Y2, b.Y2);

            int interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            int areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            int areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);

            return interArea / (float)(areaA + areaB - interArea);
        }
    }
}
