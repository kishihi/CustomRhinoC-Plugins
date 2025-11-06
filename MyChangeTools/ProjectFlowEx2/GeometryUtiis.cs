using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyChangeTools.ProjectFlowEx2
{
    public static class GeometryUtils
    {
        public static List<Curve> GetFaceTrimCurves3D(BrepFace face)
        {
            List<Curve> trimCurves3D = new List<Curve>();
            Brep brep = face.Brep;

            // 遍历 BrepFace 中的所有 BrepLoop (外环和内环/孔)
            foreach (BrepLoop loop in face.Loops)
            {
                // 遍历每个 Loop 中的所有 BrepTrim
                foreach (BrepTrim trim in loop.Trims)
                {
                    // BrepTrim 自身是 2D 的，它引用了一个 3D 的 BrepEdge
                    // 使用 trim.Edge 获取关联的 BrepEdge 对象
                    BrepEdge edge = trim.Edge;

                    if (edge != null)
                    {
                        // 从 BrepEdge 获取实际的三维曲线。
                        // 注意：BrepEdge 可能会反转引用的曲线方向。
                        // edge.EdgeCurve 属性返回原始的 3D 曲线，但可能需要根据 trim.IsReversed 调整方向。
                        // 更好的方法是使用 edge.ToNurbsCurve()，它通常能处理好方向和子域。

                        Curve edgeCurve3D = edge.ToNurbsCurve();

                        // 确保曲线方向与 trim 的方向一致
                        if (trim.ProxyCurveIsReversed)
                        {
                            edgeCurve3D.Reverse();
                        }

                        // trim 还定义了一个子域 (sub-domain)，确保只获取实际被修剪的部分
                        // 虽然 edge.ToNurbsCurve() 通常会处理子域，但可以再次使用 Trim 方法确保精确性
                        Interval domain = edge.Domain;
                        Curve trimmedCurve3D = edgeCurve3D.Trim(domain.T0, domain.T1);

                        if (trimmedCurve3D != null)
                        {
                            trimCurves3D.Add(trimmedCurve3D);
                        }
                        else
                        {
                            // 如果 Trim 失败，添加原始曲线（可能是因为曲线已经是正确的长度或域有问题）
                            trimCurves3D.Add(edgeCurve3D);
                        }
                    }
                }
            }

            return trimCurves3D;
        }
        public static List<Curve> GetFaceBorderCurves(BrepFace face)
        {
            var borders = new List<Curve>();
            foreach (var loop in face.Loops)
            {
                borders.Add(loop.To3dCurve());
            }
            return borders;
        }

        public static List<Curve> ExtendAndPullCurves(List<Curve> curves, BrepFace newFace, double tolerance)
        {
            var pulledCurves = new List<Curve>();
            double extendLen = tolerance * 1000;

            foreach (var crv in curves)
            {
                if (crv == null || !crv.IsValid)
                    continue;

                Curve extended = crv;
                try
                {
                    if (!crv.IsClosed)
                        extended = crv.Extend(CurveEnd.Both, extendLen, CurveExtensionStyle.Line);
                }
                catch
                {
                    extended = crv.DuplicateCurve();
                }

                var pulled = extended.PullToBrepFace(newFace, tolerance);
                if (pulled != null && pulled.Length > 0)
                    pulledCurves.AddRange(pulled.Where(c => c != null && c.IsValid));
            }

            return pulledCurves;
        }





        public static List<Point3d> SamplePointsOnBrepFace0(BrepFace face, int u_count = 5, int v_count = 5, double border_dist = 0.1)
        {
            var points = new List<Point3d>();
            if (face == null || u_count < 1 || v_count < 1)
                return points;

            var u_domain = face.Domain(0);
            var v_domain = face.Domain(1);
            double u_step = (u_domain.T1 - u_domain.T0) / (u_count - 1);
            double v_step = (v_domain.T1 - v_domain.T0) / (v_count - 1);

            var borders = new List<Curve>();
            foreach (var loop in face.Loops)
            {
                if (loop.LoopType == BrepLoopType.Outer)
                    borders.Add(loop.To3dCurve());
            }

            for (int i = 0; i < u_count; i++)
            {
                for (int j = 0; j < v_count; j++)
                {
                    double u = u_domain.T0 + i * u_step;
                    double v = v_domain.T0 + j * v_step;

                    if (face.IsPointOnFace(u, v) == PointFaceRelation.Interior)
                    {
                        var pt = face.PointAt(u, v);
                        double min_dist = double.MaxValue;

                        foreach (var crv in borders)
                        {
                            if (crv.ClosestPoint(pt, out double t))
                            {
                                double d = pt.DistanceTo(crv.PointAt(t));
                                if (d < min_dist) min_dist = d;
                            }
                        }

                        if (min_dist > border_dist)
                            points.Add(pt);
                    }
                }
            }
            return points;
        }


        public static List<Point3d> SamplePointsOnBrepFace(BrepFace face, double tol, int u_count = 5, int v_count = 5, double border_dist = 0.1)
        {
            var points = new List<Point3d>();
            if (face == null || u_count < 1 || v_count < 1)
                return points;

            // --- 准备域和步长 ---
            var u_domain = face.Domain(0);
            var v_domain = face.Domain(1);
            double u_step = (u_domain.T1 - u_domain.T0) / (u_count - 1);
            double v_step = (v_domain.T1 - v_domain.T0) / (v_count - 1);

            var borders = new List<Curve>();
            foreach (var loop in face.Loops)
            {
                borders.Add(loop.To3dCurve());
            }

            // --- 并行采样 ---
            var result = new ConcurrentBag<Point3d>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8)
            };

            Parallel.For(0, u_count, options, i =>
            {
                for (int j = 0; j < v_count; j++)
                {
                    double u = u_domain.T0 + i * u_step;
                    double v = v_domain.T0 + j * v_step;

                    // 快速检测参数合法性（避免内部Nurbs异常）
                    if (!face.IsPointOnFace(u, v).HasFlag(PointFaceRelation.Interior))
                        continue;

                    var pt = face.PointAt(u, v);

                    // --- 快速边界距离估算 ---
                    double minDist = double.MaxValue;
                    foreach (var crv in borders)
                    {
                        if (crv.ClosestPoint(pt, out double t))
                        {
                            double d = pt.DistanceTo(crv.PointAt(t));
                            if (d < minDist)
                            {
                                minDist = d;
                                if (minDist < border_dist) // 提前退出
                                    break;
                            }
                        }
                    }

                    if (minDist > border_dist + tol)
                        result.Add(pt);
                }
            });

            return result.ToList();
        }


        //把能转换的转为为brep统一处理
        public static Brep ToBrepSafe(GeometryBase geom)
        {
            if (geom == null) return null;
            if (geom is Brep brep) return brep;
            if (geom is Extrusion extrusion) return extrusion.ToBrep();
            if (geom is Surface surface) return surface.ToBrep();
            if (geom is SubD subD) return subD.ToBrep(new SubDToBrepOptions());
            if (geom is Mesh mesh) return Brep.CreateFromMesh(mesh, true);
            return null;

        }

        public static NurbsCurve DensifyNurbsCurve(NurbsCurve nc, int magnification)
        {
            if (nc == null || magnification < 1) return nc;

            // 获取参数范围
            double t0 = nc.Domain.T0;
            double t1 = nc.Domain.T1;

            // 插入新点
            int originalCount = nc.Points.Count;
            int newPointsCount = originalCount * magnification;

            for (int i = 1; i < newPointsCount; i++)
            {
                double t = t0 + (t1 - t0) * i / newPointsCount;
                nc.IncreaseDegree(nc.Degree); // 可选，确保有足够度数
                nc.Knots.InsertKnot(t, 1);
            }

            return nc;
        }

        public static NurbsSurface DensifyNurbsSurface(NurbsSurface ns, int magnification)
        {
            if (ns == null || magnification < 1) return ns;

            double u0 = ns.Domain(0).T0;
            double u1 = ns.Domain(0).T1;
            double v0 = ns.Domain(1).T0;
            double v1 = ns.Domain(1).T1;

            int uOriginal = ns.Points.CountU;
            int vOriginal = ns.Points.CountV;

            int uNew = uOriginal * magnification;
            int vNew = vOriginal * magnification;

            // 插入 U 方向
            for (int i = 1; i < uNew; i++)
            {
                double u = u0 + (u1 - u0) * i / uNew;
                ns.KnotsU.InsertKnot(u, 1);
            }

            // 插入 V 方向
            for (int j = 1; j < vNew; j++)
            {
                double v = v0 + (v1 - v0) * j / vNew;
                ns.KnotsV.InsertKnot(v, 1);
            }

            return ns;
        }


        public static Point3d IntersectMeshAlongVector(Mesh[] meshes, Point3d fromPt, Vector3d dir)
        {
            if (meshes == null || meshes.Length == 0 || !fromPt.IsValid || !dir.IsValid)
                return Point3d.Unset;

            dir.Unitize();
            double length = 1e6;

            // 创建双向中心线（往前后各延伸）
            Point3d p1 = fromPt - dir * length;
            Point3d p2 = fromPt + dir * length;
            Line centerLine = new Line(p1, p2);

            Point3d closest = Point3d.Unset;
            double minDist = double.MaxValue;

            foreach (var mesh in meshes)
            {
                if (mesh == null) continue;

                var hits = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, centerLine);
                if (hits == null || hits.Length == 0) continue;

                foreach (var hit in hits)
                {
                    double d = fromPt.DistanceTo(hit);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = hit;
                    }
                }
            }

            return closest;
        }

        public static Point3d IntersectSurfaceAlongVector(Brep surface, Point3d fromPt, Vector3d dir)
        {
            //dir.IsTiny()
            if (surface == null || !fromPt.IsValid || !dir.IsValid) return Point3d.Unset;

            Brep brep = ToBrepSafe(surface);
            if (brep == null || !brep.IsValid) return Point3d.Unset;

            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            //Line line = new Line(fromPt, dir, 1e6);
            dir.Unitize();
            double length = 1e6;

            // 创建双向中心线（往前后各延伸）
            Point3d p1 = fromPt - dir * length;
            Point3d p2 = fromPt + dir * length;
            Line centerLine = new Line(p1, p2);
            Curve lineCurve = new LineCurve(centerLine);

            if (Rhino.Geometry.Intersect.Intersection.CurveBrep(lineCurve, brep, tol, out _, out Point3d[] intersectionPoints))
            {
                if (intersectionPoints != null && intersectionPoints.Length > 0)
                {
                    foreach (var p in intersectionPoints)
                    {
                        if (p != Point3d.Unset) return p;
                    }
                }
            }

            return Point3d.Unset;
        }

        public static Point3d IntersectMeshAlongVector(Mesh mesh, Point3d fromPt, Vector3d dir)
        {
            if (mesh == null || !fromPt.IsValid || !dir.IsValid)
                return Point3d.Unset;

            dir.Unitize();
            double length = 1e6;

            // 创建双向中心线（往前后各延伸）
            Point3d p1 = fromPt - dir * length;
            Point3d p2 = fromPt + dir * length;
            Line centerLine = new Line(p1, p2);

            var hits = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, centerLine);

            if (hits == null || hits.Length == 0)
                return Point3d.Unset;

            // 找到距离 fromPt 最近的交点（无论正负方向）
            Point3d closest = Point3d.Unset;
            double minDist = double.MaxValue;

            foreach (var hit in hits)
            {
                double d = fromPt.DistanceTo(hit);
                if (d < minDist)
                {
                    minDist = d;
                    closest = hit;
                }
            }

            return closest;
        }


        public static Point3d TransformPointAlongDirection(Point3d A, Point3d B1, Point3d B2, Vector3d vectorA)
        {
            Vector3d move = B2 - B1;
            if (!vectorA.Unitize()) return Point3d.Unset;

            double moveLen = move * vectorA;
            Vector3d projectedMove = vectorA * moveLen;
            return A + projectedMove;
        }

        public static Point3d MovePointAlongVector(Point3d point, Vector3d direction, double distance)
        {
            var d = direction;
            if (!d.Unitize())
                return Point3d.Unset;
            return point + d * distance;
        }


        public static Point3d RotatePointToVector(Point3d a, Point3d basePoint, Vector3d targetVec, Vector3d baseVec)
        {
            if (!baseVec.Unitize() || !targetVec.Unitize()) return Point3d.Unset;

            Vector3d axis = Vector3d.CrossProduct(baseVec, targetVec);
            double angle = Vector3d.VectorAngle(baseVec, targetVec);
            Transform rotation = Transform.Rotation(angle, axis, basePoint);

            Point3d a2 = a;
            a2.Transform(rotation);
            return a2;
        }
    }
}
