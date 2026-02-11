using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using CameraAnalyzer.bl.Models.DetectorModles.Labels;
using CameraAnalyzer.bl.Models.DetectorModles.Shapes;
using CameraAnalyzer.bl.Utils;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;


namespace CameraAnalyzer.bl.Services.StickersExtractor.MiddleServices
{
    public sealed class StickersDetector : IDisposable
    {
        private readonly SIFT _sift;
        private readonly Dictionary<string, LabelModel> _labels;
        private bool _disposed;

        public StickersDetector(List<InputLabelDefinition> labelDefinitions)
        {
            if (labelDefinitions == null || labelDefinitions.Count == 0)
                throw new ArgumentException("Label definitions list is empty", nameof(labelDefinitions));

            // Constructor timing is not a priority. 
            // Lowering nFeatures to 800 significantly speeds up the 'Detect' phase.
            _sift = new SIFT(nFeatures: 1400, nOctaveLayers: 3, contrastThreshold: 0.04, edgeThreshold: 10, sigma: 1.6);
            _labels = new Dictionary<string, LabelModel>();


            foreach (var def in labelDefinitions)
            {
                using var templateGray = CvInvoke.Imread(def.ImagePath, ImreadModes.Grayscale);
                if (templateGray.IsEmpty) continue;

                var keypoints = new VectorOfKeyPoint();
                var descriptors = new Mat();
                _sift.DetectAndCompute(templateGray, null, keypoints, descriptors, false);

                _labels.Add(def.Name, new LabelModel(def.Name, templateGray.Clone(), keypoints, descriptors));
            }
        }

        public List<LabelDetectionResult> Detect(
                  Mat image,
                  string labelName,
                  int minMatches = 15,
                  double ratioThreshold = 0.7,
                  double ransacThreshold = 5.0,
                  int maxProcessingDimension = 1000)
        {
            var results = new List<LabelDetectionResult>();
            if (image == null || image.IsEmpty) return results;
            if (!_labels.TryGetValue(labelName, out var label)) return results;

            Stopwatch sw = Stopwatch.StartNew();
            Stopwatch totalSw = Stopwatch.StartNew();

            // --- 1. Dynamic Resize ---
            double scale = CalculateScale(image.Size, maxProcessingDimension);
            using var workImage = new Mat();
            CvInvoke.Resize(image, workImage, new Size(), scale, scale, Inter.Area);

            Logger.LogDebug($"[Timer] Resize: {sw.ElapsedMilliseconds}ms");
            sw.Restart();

            // --- 2. SIFT Extraction (The Main Bottleneck) ---
            using var sceneKeypoints = new VectorOfKeyPoint();
            using var sceneDescriptors = new Mat();
            _sift.DetectAndCompute(workImage, null, sceneKeypoints, sceneDescriptors, false);

            Logger.LogDebug($"[Timer] SIFT DetectAndCompute: {sw.ElapsedMilliseconds}ms (Found {sceneKeypoints.Size} keypoints)");
            sw.Restart();

            if (sceneDescriptors.IsEmpty || sceneKeypoints.Size < minMatches)
            {
                Logger.LogDebug("[Timer] Detection aborted: Not enough keypoints.");
                return results;
            }

            // --- 3. Matching (BFMatcher) ---
            using var matcher = new BFMatcher(DistanceType.L2);
            using var knnMatches = new VectorOfVectorOfDMatch();
            matcher.KnnMatch(label.Descriptors, sceneDescriptors, knnMatches, 2);

            Logger.LogDebug($"[Timer] KNN Matching: {sw.ElapsedMilliseconds}ms");
            sw.Restart();

            // --- 4. Ratio Test ---
            var remainingMatches = new List<MDMatch>();
            for (int i = 0; i < knnMatches.Size; i++)
            {
                if (knnMatches[i].Size < 2) continue;
                if (knnMatches[i][0].Distance < ratioThreshold * knnMatches[i][1].Distance)
                    remainingMatches.Add(knnMatches[i][0]);
            }

            Logger.LogDebug($"[Timer] Ratio Test: {sw.ElapsedMilliseconds}ms (Matches remaining: {remainingMatches.Count})");
            sw.Restart();

            // --- 5. Multi-instance Loop (Homography & RANSAC) ---
            int iteration = 0;
            while (remainingMatches.Count >= minMatches)
            {
                iteration++;
                Stopwatch iterationSw = Stopwatch.StartNew();

                var srcPoints = new PointF[remainingMatches.Count];
                var dstPoints = new PointF[remainingMatches.Count];

                for (int i = 0; i < remainingMatches.Count; i++)
                {
                    srcPoints[i] = label.Keypoints[remainingMatches[i].QueryIdx].Point;
                    var p = sceneKeypoints[remainingMatches[i].TrainIdx].Point;
                    dstPoints[i] = new PointF((float)(p.X / scale), (float)(p.Y / scale));
                }

                using var srcVec = new VectorOfPointF(srcPoints);
                using var dstVec = new VectorOfPointF(dstPoints);
                using var inlierMask = new Mat();
                using var homography = CvInvoke.FindHomography(srcVec, dstVec, RobustEstimationAlgorithm.Ransac, ransacThreshold, inlierMask);

                if (homography == null || homography.IsEmpty) break;

                int inliersCount = CountInliers(inlierMask);
                if (inliersCount < minMatches) break;

                var tplCorners = new[] {
                    new PointF(0, 0),
                    new PointF(label.TemplateGray.Width, 0),
                    new PointF(label.TemplateGray.Width, label.TemplateGray.Height),
                    new PointF(0, label.TemplateGray.Height)
                };

                using var tplVec = new VectorOfPointF(tplCorners);
                using var imgVec = new VectorOfPointF();
                CvInvoke.PerspectiveTransform(tplVec, imgVec, homography);

                if (CvInvoke.IsContourConvex(imgVec) && CvInvoke.ContourArea(imgVec) > 100)
                {
                    var cornersArray = imgVec.ToArray();
                    results.Add(new LabelDetectionResult(
                        labelName: label.Name,
                        corners: cornersArray.Select(p => new Point2D((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToList().AsReadOnly(),
                        confidence: (double)inliersCount / remainingMatches.Count,
                        inliers: inliersCount,
                        matches: remainingMatches.Count
                    ));
                }
                else
                {
                    break;
                }

                var nextIterationMatches = new List<MDMatch>();
                byte[] maskBytes = new byte[inlierMask.Rows];
                inlierMask.CopyTo(maskBytes);

                for (int i = 0; i < remainingMatches.Count; i++)
                {
                    if (maskBytes[i] == 0)
                    {
                        nextIterationMatches.Add(remainingMatches[i]);
                    }
                }

                if (nextIterationMatches.Count == remainingMatches.Count) break;
                remainingMatches = nextIterationMatches;

                Logger.LogDebug($"[Timer] Loop Iteration {iteration}: {iterationSw.ElapsedMilliseconds}ms");
            }

            Logger.LogDebug($"[Timer] Total Multi-instance Loop: {sw.ElapsedMilliseconds}ms");
            Logger.LogDebug($"[Timer] === TOTAL DETECTION TIME: {totalSw.ElapsedMilliseconds}ms ===");

            return results;
        }

        // --- PRIVATE HELPERS (Fixed CS0103) ---

        private double CalculateScale(Size size, int maxDim)
        {
            if (size.Width <= maxDim && size.Height <= maxDim) return 1.0;
            return (double)maxDim / Math.Max(size.Width, size.Height);
        }

        private int CountInliers(Mat mask)
        {
            if (mask == null || mask.IsEmpty) return 0;
            var maskBytes = new byte[mask.Rows];
            mask.CopyTo(maskBytes);
            return maskBytes.Count(b => b != 0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (var label in _labels.Values) label.Dispose();
            _sift.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}