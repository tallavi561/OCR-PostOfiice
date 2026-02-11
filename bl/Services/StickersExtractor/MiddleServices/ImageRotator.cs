using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CameraAnalyzer.bl.Models.DetectorModles.Shapes;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;

namespace CameraAnalyzer.bl.Services.StickersExtractor.MiddleServices
{
    public static class ImageRotator
    {
        public static Mat ExtractAndAlignLabel(Mat image, IReadOnlyList<Point2D> corners)
        {
            if (image == null || image.IsEmpty)
                throw new ArgumentException("Image is null or empty");

            if (corners == null || corners.Count != 4)
                throw new ArgumentException("Must provide exactly 4 corners");

            // 1. המרת הפינות לפורמט PointF ש-OpenCV מכיר
            var srcPoints = corners.Select(p => new PointF(p.X, p.Y)).ToArray();

            // 2. חישוב מימדי המדבקה (רוחב וגובה) לפי המרחקים בין הפינות
            float width = GetDistance(srcPoints[0], srcPoints[1]);
            float height = GetDistance(srcPoints[0], srcPoints[3]);

            // 3. הגדרת נקודות היעד - מלבן ישר שמתחיל ב-(0,0)
            var dstPoints = new PointF[]
            {
                new PointF(0, 0),
                new PointF(width, 0),
                new PointF(width, height),
                new PointF(0, height)
            };

            // 4. יצירת מטריצת הטרנספורמציה וביצוע החיתוך
            using var srcVec = new VectorOfPointF(srcPoints);
            using var dstVec = new VectorOfPointF(dstPoints);
            using var matrix = CvInvoke.GetPerspectiveTransform(srcVec, dstVec);

            var result = new Mat();
            CvInvoke.WarpPerspective(image, result, matrix, new Size((int)width, (int)height));

            return result;
        }

        private static float GetDistance(PointF p1, PointF p2)
        {
            return (float)Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }
    }
}