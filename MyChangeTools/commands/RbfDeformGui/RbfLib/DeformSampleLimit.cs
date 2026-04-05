using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui.RBFLib
{

    internal partial class Deform
    {
        //add limit curve
        bool AddLimitCurve(Curve limitedCurve, int count)
        {
            NurbsCurve nc = limitedCurve.ToNurbsCurve();
            nc.Domain = new Interval(0, count);
            var samplePoints = Enumerable.Range(0, count + 1).Select(t => nc.PointAt(t));
            var ZeroDeltas = Enumerable.Range(0, count + 1).Select(t => Vector3d.Zero);

            _srcPoints.AddRange(samplePoints);
            _deltas.AddRange(ZeroDeltas);

            return samplePoints.Count() > 0;
        }


        //use mesh vertex as limited point
        bool AddLimitMesh(Mesh limitedMesh, out int successCount)
        {
            successCount = 0;
            if (limitedMesh == null || limitedMesh.Vertices.Count == 0 || !limitedMesh.IsValid)
            {
                return false;
            }
            else
            {
                var srcPoints = limitedMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));
                var zeroDeltas = limitedMesh.Vertices.Select(v => Vector3d.Zero);
                _srcPoints.AddRange(srcPoints);
                _deltas.AddRange(zeroDeltas);
                successCount = limitedMesh.Vertices.Count;
            }
            return successCount > 0;

        }

        //use points as limited point
        bool AddLimitPoints(IEnumerable<Point3d> points, out int successCount)
        {
            successCount = 0;
            if (points == null || !points.Any())
            {
                return false;
            }
            else
            {
                var zeroDeltas = points.Select(p => Vector3d.Zero);
                _srcPoints.AddRange(points);
                _deltas.AddRange(zeroDeltas);
                successCount = points.Count();
            }
            return successCount > 0;
        }

        // 对一张曲面进行采样，得到点，计算位移增量为0，作为受限点
        bool AddLimitSurface(NurbsSurface limitedSrf, out int successCount)
        {
            successCount = 0;
            if (limitedSrf == null) return false;

            int countU = limitedSrf.Points.CountU;
            int countV = limitedSrf.Points.CountV;

            var uDomain = limitedSrf.Domain(0);
            var vDomain = limitedSrf.Domain(1);

            var srcPts = new List<Point3d>();

            for (int i = 0; i < countU; i++)
            {
                double u = (countU == 1)
                    ? uDomain.Mid
                    : uDomain.ParameterAt((double)i / (countU - 1));

                for (int j = 0; j < countV; j++)
                {
                    double v = (countV == 1)
                        ? vDomain.Mid
                        : vDomain.ParameterAt((double)j / (countV - 1));

                    srcPts.Add(limitedSrf.PointAt(u, v));
                }
            }

            var zeroDeltas = srcPts.Select(p => Vector3d.Zero);

            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(zeroDeltas);

            successCount = srcPts.Count;
            return successCount > 0;
        }
    }
}