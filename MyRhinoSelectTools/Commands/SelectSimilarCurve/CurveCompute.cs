using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyRhinoSelectTools.Commands.SelectSimilarCurve
{
    public class CurveCompute
    {
        // 改进的核心相似度函数（无关大小、位置、旋转）
        public static double CurveSimilarity(Curve c1, Curve c2, int sampleCount = 100, bool handleRotation = true)
        {
            if (c1 == null || c2 == null || !c1.IsValid || !c2.IsValid)
                return 0;

            // 步骤1: 归一化曲线（平移、缩放）
            Curve normC1 = NormalizeCurve(c1);
            Curve normC2 = NormalizeCurve(c2);

            // 步骤2: 处理旋转（可选，对闭合曲线有效）
            if (handleRotation && normC1.IsClosed && normC2.IsClosed)
            {
                normC2 = AlignRotation(normC1, normC2, sampleCount);
            }

            // 步骤3: 采样点
            List<Point3d> points1 = SamplePoints(normC1, sampleCount);
            List<Point3d> points2 = SamplePoints(normC2, sampleCount);

            // 步骤4: 计算 Hausdorff 距离
            double hausdorffDist = ComputeHausdorffDistance(points1, points2);

            // 步骤5: 转换为相似度（越小距离越相似）
            return 1.0 / (1.0 + hausdorffDist);
        }

        // 归一化曲线：平移到原点，缩放到单位边界框
        private static Curve NormalizeCurve(Curve curve)
        {
            Curve dup = curve.DuplicateCurve();
            BoundingBox bb = dup.GetBoundingBox(true);
            if (!bb.IsValid) return dup;

            // 平移到原点
            Vector3d tocenter = MyRhinoSelectTools.CustomQuickClass.QuickGeometry.GetCentroidFast(dup) - Point3d.Origin;
            Transform translate = Transform.Translation(-tocenter);
            dup.Transform(translate);

            // 缩放到单位大小
            Vector3d diag = bb.Max - bb.Min;
            double maxSide = Math.Max(diag.X, Math.Max(diag.Y, diag.Z));
            if (maxSide > 0)
            {
                Transform scale = Transform.Scale(Point3d.Origin, 1.0 / maxSide);
                dup.Transform(scale);
            }

            return dup;
        }

        // 旋转对齐：使用起点偏移类似方法对齐闭合曲线
        private static Curve AlignRotation(Curve refCurve, Curve targetCurve, int sampleCount)
        {
            double bestDist = double.MaxValue;
            Curve bestAligned = targetCurve.DuplicateCurve();

            int shiftSteps = 36; // 旋转步数（10度一步）
            for (int shift = 0; shift < shiftSteps; shift++)
            {
                double angle = (shift / (double)shiftSteps) * Math.PI * 2;
                Transform rotate = Transform.Rotation(angle, Vector3d.ZAxis, Point3d.Origin); // 假设Z轴旋转，视曲线平面调整
                Curve rotated = targetCurve.DuplicateCurve();
                rotated.Transform(rotate);

                double dist = ComputeHausdorffDistance(SamplePoints(refCurve, sampleCount), SamplePoints(rotated, sampleCount));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestAligned = rotated;
                }
            }

            return bestAligned;
        }

        // 均匀采样点
        private static List<Point3d> SamplePoints(Curve curve, int count)
        {
            List<Point3d> points = new List<Point3d>();
            curve.Domain = new Interval(0, 1);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)(count - 1);
                points.Add(curve.PointAt(t));
            }
            return points;
        }

        // 计算 Hausdorff 距离（双向最大最小距离）
        private static double ComputeHausdorffDistance(List<Point3d> points1, List<Point3d> points2)
        {
            double maxDist1 = points1.Max(p1 => points2.Min(p2 => p1.DistanceTo(p2)));
            double maxDist2 = points2.Max(p2 => points1.Min(p1 => p2.DistanceTo(p1)));
            return Math.Max(maxDist1, maxDist2);
        }
    }
}