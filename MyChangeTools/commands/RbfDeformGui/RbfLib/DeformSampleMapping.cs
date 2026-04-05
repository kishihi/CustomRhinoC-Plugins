using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui.RBFLib
{
    internal partial class Deform
    {
        static bool ComputeDeltaByNormal(Point3d sourcePoint, Curve targetCurve, out Vector3d delta)
        {
            delta = Vector3d.Unset;
            if (targetCurve.ClosestPoint(sourcePoint, out double t, 0))
            {
                var dstpoint = targetCurve.PointAt(t);
                delta = dstpoint - sourcePoint;
                return true;
            }

            return false;
        }

        // curve mapping by cloestpoing
        bool AddCurveMapByClosestPoint(Curve baseCurve, Curve targetCurve, int count, out int successCount)
        {
            successCount = 0;
            NurbsCurve nurbsBaseCurve = baseCurve.ToNurbsCurve();
            nurbsBaseCurve.Domain = new Interval(0, count);
            var srcPoints = Enumerable.Range(0, count + 1).Select(t => nurbsBaseCurve.PointAt(t));
            var successSrcPoints = new List<Point3d>();
            var successDeltas = new List<Vector3d>();
            foreach (Point3d srcpoint in srcPoints)
            {

                if (ComputeDeltaByNormal(srcpoint, targetCurve, out Vector3d delta))
                {
                    successDeltas.Add(delta);
                    successSrcPoints.Add(srcpoint);
                }

            }
            _srcPoints.AddRange(successSrcPoints);
            _deltas.AddRange(successDeltas);
            if (successSrcPoints.Count > 0)
            {
                successCount = successSrcPoints.Count;

                return true;
            }
            else
            {
                return false;
            }

        }

        //curve mapping by parameter
        bool AddCurveMapByParameter(Curve baseCurve, Curve targetCurve, int count, out int successCount)
        {
            successCount = 0;

            NurbsCurve nurbsBase = baseCurve.ToNurbsCurve();
            NurbsCurve nurbsTarget = targetCurve.ToNurbsCurve();

            double baseLength = nurbsBase.GetLength();
            double targetLength = nurbsTarget.GetLength();

            // 采样参数列表
            var srcPoints = new List<Point3d>();
            var dstPoints = new List<Point3d>();

            for (int i = 0; i <= count; i++)
            {
                if (nurbsBase.LengthParameter(baseLength * i / count, out double tBase) &&
                nurbsTarget.LengthParameter(targetLength * i / count, out double tTarget))
                {

                    srcPoints.Add(nurbsBase.PointAt(tBase));
                    dstPoints.Add(nurbsTarget.PointAt(tTarget));
                }
            }
            var deltas = srcPoints.Zip(dstPoints, (src, dst) => dst - src).ToList();

            _srcPoints.AddRange(srcPoints);
            _deltas.AddRange(deltas);

            if (srcPoints.Count > 0)
            {
                successCount = srcPoints.Count;
                return true;
            }
            else
            {
                return false;
            }
        }

        // mesh maping by vertexorder
        bool AddMeshMapByVertexOrder(Mesh baseMesh, Mesh targetMesh, out int successCount)
        {
            successCount = 0;
            if (baseMesh.Vertices.Count != targetMesh.Vertices.Count) { return false; }
            ;
            var srcPts = baseMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));
            var dstPts = targetMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));
            var deltas = srcPts.Zip(dstPts, (src, dst) => dst - src);
            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);
            successCount = srcPts.Count();
            return successCount > 0;
        }

        //mesh mapping by coordiante
        bool AddMeshMapByCoordinate(Mesh baseMesh, Mesh targetMesh, out int successCount)
        {
            successCount = 0;
            if (baseMesh.Vertices.Count != targetMesh.Vertices.Count) { return false; }

            var srcPts = baseMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));

            var baseMps = srcPts.Select(t => baseMesh.ClosestMeshPoint(t, 0.0));

            var dstPts = baseMps.Select(bmp => targetMesh.PointAt(bmp));

            var deltas = srcPts.Zip(dstPts, (src, dst) => dst - src);
            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);
            successCount = srcPts.Count();
            return successCount > 0;
        }

        //point mapping
        bool AddPtMap(IEnumerable<Point3d> basePts, IEnumerable<Point3d> targetPts, out int successCount)
        {
            successCount = 0;
            if (basePts.Count() != targetPts.Count()) return false;
            var deltas = basePts.Zip(targetPts, (src, dst) => dst - src);
            _srcPoints.AddRange(basePts);
            _deltas.AddRange(deltas);
            successCount = basePts.Count();
            return successCount > 0;
        }

        //surface maping by uv correspond
        // 对基础曲面和目标曲面采集相同uv数量的点，计算位移增量
        bool AddSurfaceMapByUVCorrespond(
    NurbsSurface baseSrf,
    NurbsSurface targetSrf,
    out int successCount)
        {
            successCount = 0;
            if (baseSrf == null || targetSrf == null)
                return false;
            if (baseSrf.Points.CountU != targetSrf.Points.CountU || baseSrf.Points.CountV != targetSrf.Points.CountV)
                return false;

            int countU = baseSrf.Points.CountU;
            int countV = baseSrf.Points.CountV;

            var uDomainBase = baseSrf.Domain(0);
            var vDomainBase = baseSrf.Domain(1);

            var uDomainTarget = targetSrf.Domain(0);
            var vDomainTarget = targetSrf.Domain(1);

            var srcPts = new List<Point3d>();
            var dstPts = new List<Point3d>();

            for (int i = 0; i < countU; i++)
            {
                double tU = (countU == 1) ? 0.5 : (double)i / (countU - 1);

                double uBase = uDomainBase.ParameterAt(tU);
                double uTarget = uDomainTarget.ParameterAt(tU);

                for (int j = 0; j < countV; j++)
                {
                    double tV = (countV == 1) ? 0.5 : (double)j / (countV - 1);

                    double vBase = vDomainBase.ParameterAt(tV);
                    double vTarget = vDomainTarget.ParameterAt(tV);

                    var pBase = baseSrf.PointAt(uBase, vBase);
                    var pTarget = targetSrf.PointAt(uTarget, vTarget);

                    srcPts.Add(pBase);
                    dstPts.Add(pTarget);
                }
            }

            var deltas = srcPts.Zip(dstPts, (src, dst) => dst - src);

            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);

            successCount = srcPts.Count;
            return successCount > 0;
        }
        // surface maping by NormalDirection
        // 对基础曲面采集uvcount数量的点，作一条法线方向两端无限延伸的射线，求与目标曲面的交点，计算位移增量
        bool AddSurfaceMapByNormalDirection(
    NurbsSurface baseSrf,
    NurbsSurface targetSrf,
    out int successCount)
        {
            successCount = 0;
            if (baseSrf == null || targetSrf == null) return false;

            int countU = baseSrf.Points.CountU;
            int countV = baseSrf.Points.CountV;

            var uDomain = baseSrf.Domain(0);
            var vDomain = baseSrf.Domain(1);

            var srcPts = new List<Point3d>();
            var deltas = new List<Vector3d>();

            var targets = new List<Surface> { targetSrf };

            for (int i = 0; i < countU; i++)
            {
                double tU = (countU == 1) ? 0.5 : (double)i / (countU - 1);
                double u = uDomain.ParameterAt(tU);

                for (int j = 0; j < countV; j++)
                {
                    double tV = (countV == 1) ? 0.5 : (double)j / (countV - 1);
                    double v = vDomain.ParameterAt(tV);

                    Point3d p = baseSrf.PointAt(u, v);

                    // 关键：求法线
                    Vector3d normal;
                    if (!baseSrf.NormalAt(u, v).Unitize())
                        continue;

                    normal = baseSrf.NormalAt(u, v);
                    normal.Unitize();

                    // 双向射线（正 + 负）
                    var ray1 = new Ray3d(p, normal);
                    var ray2 = new Ray3d(p, -normal);

                    Point3d[] hits1 = Rhino.Geometry.Intersect.Intersection.RayShoot(ray1, targets, 1);
                    Point3d[] hits2 = Rhino.Geometry.Intersect.Intersection.RayShoot(ray2, targets, 1);

                    Point3d? bestHit = null;
                    double minDist = double.MaxValue;

                    // 找最近交点
                    Action<Point3d[]> checkHits = (hits) =>
                    {
                        if (hits == null) return;
                        foreach (var h in hits)
                        {
                            double d = p.DistanceTo(h);
                            if (d < minDist)
                            {
                                minDist = d;
                                bestHit = h;
                            }
                        }
                    };

                    checkHits(hits1);
                    checkHits(hits2);

                    if (bestHit.HasValue)
                    {
                        srcPts.Add(p);
                        deltas.Add(bestHit.Value - p);
                        successCount++;
                    }
                }
            }

            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);

            return successCount > 0;
        }
        //surface maping by Vector
        // 对基础曲面采集uvcount数量的点，按照一个方向两端无限延伸的射线，求与目标曲面的交点，计算位移增量
        bool AddSurfaceMapByDirection(
    NurbsSurface baseSrf,
    NurbsSurface targetSrf,
    Vector3d direction,
    out int successCount)
        {
            successCount = 0;
            if (baseSrf == null || targetSrf == null) return false;
            if (!direction.Unitize()) return false;

            int countU = baseSrf.Points.CountU;
            int countV = baseSrf.Points.CountV;

            var uDomain = baseSrf.Domain(0);
            var vDomain = baseSrf.Domain(1);

            var srcPts = new List<Point3d>();
            var deltas = new List<Vector3d>();

            var targets = new List<Surface> { targetSrf };

            for (int i = 0; i < countU; i++)
            {
                double tU = (countU == 1) ? 0.5 : (double)i / (countU - 1);
                double u = uDomain.ParameterAt(tU);

                for (int j = 0; j < countV; j++)
                {
                    double tV = (countV == 1) ? 0.5 : (double)j / (countV - 1);
                    double v = vDomain.ParameterAt(tV);

                    Point3d p = baseSrf.PointAt(u, v);

                    var ray1 = new Ray3d(p, direction);
                    var ray2 = new Ray3d(p, -direction);

                    Point3d[] hits1 = Rhino.Geometry.Intersect.Intersection.RayShoot(ray1, targets, 1);
                    Point3d[] hits2 = Rhino.Geometry.Intersect.Intersection.RayShoot(ray2, targets, 1);

                    Point3d? bestHit = null;
                    double minDist = double.MaxValue;

                    Action<Point3d[]> checkHits = (hits) =>
                    {
                        if (hits == null) return;
                        foreach (var h in hits)
                        {
                            double d = p.DistanceTo(h);
                            if (d < minDist)
                            {
                                minDist = d;
                                bestHit = h;
                            }
                        }
                    };

                    checkHits(hits1);
                    checkHits(hits2);

                    if (bestHit.HasValue)
                    {
                        srcPts.Add(p);
                        deltas.Add(bestHit.Value - p);
                        successCount++;
                    }
                }
            }

            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);

            return successCount > 0;
        }

    }
}