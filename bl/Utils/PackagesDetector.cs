// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using Microsoft.ML.OnnxRuntime;
// using Microsoft.ML.OnnxRuntime.Tensors;
// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.PixelFormats;
// using SixLabors.ImageSharp.Processing; // נדרש ל-Resize
// using CameraAnalyzer.bl.Models;

// namespace CameraAnalyzer.bl.Utils
// {
//     public static class PackagesDetector
//     {
//         private static readonly object _lock = new();
//         private static InferenceSession? _session;

//         /// <summary>
//         /// Detect packages using YOLOv5n (ONNX Runtime). Works on Linux/Windows/macOS.
//         /// </summary>
//         public static List<BoundingBox> Detect(string imagePath, float confidenceThreshold = 0.02f)
//         {
//             if (!File.Exists(imagePath))
//                 throw new FileNotFoundException($"Image not found: {imagePath}");

//             // --- 1. Load the model once ---
//             lock (_lock)
//             {
//                 _session ??= new InferenceSession("models/yolov5n.onnx");
//             }

//             // --- 2. Load image ---
//             using var image = Image.Load<Rgba32>(imagePath);
//             int originalWidth = image.Width;
//             int originalHeight = image.Height;

//             // --- 3. Convert image to tensor ---
//             const int targetSize = 640;
//             var tensor = ImageToTensor(image, targetSize, targetSize);

//             // --- 4. Run model inference ---
//             var inputs = new List<NamedOnnxValue>
//             {
//                 NamedOnnxValue.CreateFromTensor("images", tensor)
//             };

//             using var results = _session.Run(inputs);
//             var result = results.First(r => r.Name == "output");
//             var outputTensor = result.AsTensor<float>();

//             Console.WriteLine($"Tensor shape: {string.Join('x', outputTensor.Dimensions.ToArray())}");

//             // --- 5. Parse YOLO output ---
//             var boxes = ParseYoloOutput(outputTensor, originalWidth, originalHeight, confidenceThreshold);

//             // --- 6. Apply Non-Maximum Suppression ---
//             var finalBoxes = ApplyNms(boxes, 0.45f);

//             Logger.LogInfo($"Detected {finalBoxes.Count} packages (threshold={confidenceThreshold}).");
//             return finalBoxes;
//         }

//         /// <summary>
//         /// Convert ImageSharp image to normalized float tensor (1x3x640x640)
//         /// </summary>
//         private static DenseTensor<float> ImageToTensor(Image<Rgba32> image, int width, int height)
//         {
//             var resized = image.Clone(x => x.Resize(width, height));
//             var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });

//             resized.ProcessPixelRows(accessor =>
//             {
//                 for (int y = 0; y < height; y++)
//                 {
//                     var row = accessor.GetRowSpan(y);
//                     for (int x = 0; x < width; x++)
//                     {
//                         // נורמליזציה מדויקת ל-YOLO (טווח -1 עד 1)
//                         tensor[0, 0, y, x] = (row[x].R / 255f - 0.5f) / 0.5f;
//                         tensor[0, 1, y, x] = (row[x].G / 255f - 0.5f) / 0.5f;
//                         tensor[0, 2, y, x] = (row[x].B / 255f - 0.5f) / 0.5f;
//                     }
//                 }
//             });

//             return tensor;
//         }

//         /// <summary>
//         /// Parse YOLOv5 output tensor into bounding boxes.
//         /// </summary>
//         private static List<BoundingBox> ParseYoloOutput(
//             Tensor<float> outputTensor,
//             int originalWidth,
//             int originalHeight,
//             float confidenceThreshold)
//         {
//             var dims = outputTensor.Dimensions; // [1, 25200, 85]
//             int numBoxes = dims[1];
//             int attributes = dims[2];
//             var boxes = new List<BoundingBox>();

//             float scaleX = originalWidth / 640f;
//             float scaleY = originalHeight / 640f;

//             for (int i = 0; i < numBoxes; i++)
//             {
//                 float x = outputTensor[0, i, 0];
//                 float y = outputTensor[0, i, 1];
//                 float w = outputTensor[0, i, 2];
//                 float h = outputTensor[0, i, 3];
//                 float objectness = outputTensor[0, i, 4];

//                 // Find best class score
//                 float maxClassScore = 0f;
//                 for (int c = 5; c < attributes; c++)
//                     if (outputTensor[0, i, c] > maxClassScore)
//                         maxClassScore = outputTensor[0, i, c];

//                 float confidence = objectness * maxClassScore;
//                 if (confidence < confidenceThreshold)
//                     continue;

//                 int X1 = (int)((x - w / 2) * scaleX);
//                 int Y1 = (int)((y - h / 2) * scaleY);
//                 int X2 = (int)((x + w / 2) * scaleX);
//                 int Y2 = (int)((y + h / 2) * scaleY);

//                 boxes.Add(new BoundingBox
//                 {
//                     X1 = Math.Max(0, X1),
//                     Y1 = Math.Max(0, Y1),
//                     X2 = Math.Min(originalWidth, X2),
//                     Y2 = Math.Min(originalHeight, Y2),
//                     Confidence = confidence
//                 });

//                 Console.WriteLine($"Box {i}: conf={confidence:F2} ({X1},{Y1})-({X2},{Y2})");
//             }

//             return boxes;
//         }

//         /// <summary>
//         /// Non-Maximum Suppression (NMS) to remove overlapping boxes.
//         /// </summary>
//         private static List<BoundingBox> ApplyNms(List<BoundingBox> boxes, float iouThreshold)
//         {
//             var finalBoxes = new List<BoundingBox>();
//             var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();

//             while (sorted.Count > 0)
//             {
//                 var current = sorted[0];
//                 finalBoxes.Add(current);
//                 sorted.RemoveAt(0);

//                 sorted = sorted.Where(box => IoU(current, box) < iouThreshold).ToList();
//             }

//             return finalBoxes;
//         }

//         /// <summary>
//         /// Compute Intersection over Union (IoU)
//         /// </summary>
//         private static float IoU(BoundingBox a, BoundingBox b)
//         {
//             int X1 = Math.Max(a.X1, b.X1);
//             int Y1 = Math.Max(a.Y1, b.Y1);
//             int X2 = Math.Min(a.X2, b.X2);
//             int Y2 = Math.Min(a.Y2, b.Y2);

//             int interArea = Math.Max(0, X2 - X1) * Math.Max(0, Y2 - Y1);
//             int boxAArea = Math.Max(0, a.X2 - a.X1) * Math.Max(0, a.Y2 - a.Y1);
//             int boxBArea = Math.Max(0, b.X2 - b.X1) * Math.Max(0, b.Y2 - b.Y1);
//             int unionArea = boxAArea + boxBArea - interArea;

//             return unionArea == 0 ? 0 : (float)interArea / unionArea;
//         }
//     }
// }
