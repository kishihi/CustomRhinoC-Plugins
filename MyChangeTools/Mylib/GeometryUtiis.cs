using MathNet.Numerics.Distributions;
using MathNet.Numerics.RootFinding;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyChangeTools.Mylib
{
    public static class GeometryUtils
    {
        public static List<double> FindKinks(NurbsCurve curve)
        {
            List<double> kinkParams = new List<double>();
            var knots = curve.Knots;
            int degree = curve.Degree;
            double domainMin = curve.Domain.Min;
            double domainMax = curve.Domain.Max;

            for (int i = 0; i < knots.Count;)
            {
                double currentKnot = knots[i];
                int multiplicity = 1;

                // 计算 multiplicity
                while
                (
                    i + multiplicity < knots.Count
                &&
                    Math.Abs(knots[i + multiplicity] - currentKnot) < Rhino.RhinoMath.ZeroTolerance
                )
                {
                    multiplicity++;
                }

                // 如果 multiplicity == degree 且是内部 knot，则是 kink
                if (multiplicity == degree && currentKnot > domainMin && currentKnot < domainMax)
                {
                    kinkParams.Add(currentKnot);
                }

                i += multiplicity;  // 跳到下一个独特 knot
            }

            return kinkParams;
        }

        public static List<Point3d> GetObjsOutMeshBoundaryPoints(
            Mesh mesh,
            GeometryBase[] objs, 
            double offsetDistance, 
            double intersectTolerance)
        {
            var bps = GetMeshBoundaryNormalSweepFace(mesh, offsetDistance);
            var pts = new List<Point3d>();
            foreach (var bp in bps) { 
                pts.AddRange(GetObjInsectWithBrepCurveKnotPoint(bp,objs,intersectTolerance));
            }
            return pts;
        }

        private static List<Brep> GetMeshBoundaryNormalSweepFace(Mesh mesh,double offsetDistance)
        {
            var outBreps = new List<Brep>();
            if (mesh == null || !mesh.IsValid)
                return outBreps;
            var meshoffset1 = mesh.Offset(offsetDistance);
            var meshoffset2 = mesh.Offset(-offsetDistance);
            var nakedEdges1 = meshoffset1.GetNakedEdges();
            var nakedEdges2 = meshoffset2.GetNakedEdges();
            if(nakedEdges1.Length != nakedEdges2.Length)
            {
                return outBreps;
            }
            for(int i = 0; i < nakedEdges1.Length; i++)
            {
                var rails = new NurbsCurve[] { nakedEdges1[i].ToNurbsCurve(), nakedEdges2[i].ToNurbsCurve() };
                var bps = Brep.CreateFromLoft(rails, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                outBreps.AddRange(bps);
            }
            return outBreps;
        }

        private static List<Point3d> GetObjInsectWithBrepCurveKnotPoint(
            Brep brep,
            GeometryBase[] objs,
            double tolerance)
        {
            var iccurves = new List<Curve>();
            var ickpts = new List<Point3d>();
            foreach(var go in objs)
            {
                var obtype = go.ObjectType;
                if(obtype == ObjectType.Curve)
                {
                    Intersection.CurveBrep(
                        go as Curve, 
                        brep, 
                        tolerance, 
                        out Curve[] overlapCurves,
                        out Point3d[] intersectionPoints);
                    iccurves.AddRange(overlapCurves);
                    ickpts.AddRange(intersectionPoints);
                }
                Brep tobrep = ToBrepSafe(go);
                if(tobrep != null)
                {
                    Intersection.BrepBrep(
                        brep,
                        tobrep,
                        tolerance,
                        out Curve[] intersectionCurves,
                        out Point3d[] intersectionPoints
                    );
                    iccurves.AddRange(intersectionCurves);
                    ickpts.AddRange(intersectionPoints);
                }
            }
            foreach(var curve in iccurves)
            {
                var nc = curve.ToNurbsCurve();
                var knots = nc.Knots;
                ickpts.AddRange(knots.Select(t => nc.PointAt(t)));
            }
            return ickpts;
        }


        public static List<Point3d> GetMeshBoundaryPoints(Mesh mesh)
        {
            var pts = new List<Point3d>();

            if (mesh == null || !mesh.IsValid)
                return pts;

            var nakedEdges = mesh.GetNakedEdges(); // Polyline[]

            for (int i = 0; i < nakedEdges.Length; i++)
            {
                var pl = nakedEdges[i];
                for (int j = 0; j < pl.Count; j++)
                {
                    pts.Add(pl[j]);
                }
            }

            return pts;
        }

        public static bool IsPointOutsideMesh(
            Mesh mesh,
            Point3d testpt,
            double outsideDistanceTol
            )
        {
            if (mesh == null || !mesh.IsValid)
                return true;
            int mp = mesh.ClosestPoint(testpt, out Point3d pointOnmesh, out Vector3d normalAtPoint, 0.0);
            if(mp < 0 ) 
                return true;
            normalAtPoint.Unitize();
            Vector3d rv = testpt - pointOnmesh;
            double rvProjectNormalLength = Math.Abs(rv * normalAtPoint);
            if (Math.Abs(rvProjectNormalLength - rv.Length) > outsideDistanceTol)
            {
                return true;
            }
            return false;
        }

        public static bool IsPointOutsideBrep(
    Brep brep,
    Point3d testpt,
    double outsideDistanceTol)
        {
            // return false;
            if (brep == null || !brep.IsValid)
                return true;

            // // 最近点
            if (!brep.ClosestPoint(
                    testpt,
                    out Point3d brepCloestPoint,
                    out ComponentIndex _,
                    out double _,
                    out double _,
                    0,
                    out Vector3d normal))
                return true;

            normal.Unitize();

            Vector3d rv = testpt - brepCloestPoint;

            double rvProjectNormalLength = Math.Abs(rv * normal);

            if (Math.Abs(rvProjectNormalLength - rv.Length) > outsideDistanceTol)
            {
                return true;
            }
            return false;

        }





        public static List<Point3d> SampleBoundingBoxSimple(BoundingBox box)
        {
            var pts = new List<Point3d>();

            if (!box.IsValid) return pts;

            // 8 corner points
            pts.Add(box.Corner(true, true, true));
            pts.Add(box.Corner(true, true, false));
            pts.Add(box.Corner(true, false, true));
            pts.Add(box.Corner(true, false, false));
            pts.Add(box.Corner(false, true, true));
            pts.Add(box.Corner(false, true, false));
            pts.Add(box.Corner(false, false, true));
            pts.Add(box.Corner(false, false, false));

            var min = box.Min;
            var max = box.Max;
            var c = box.Center;

            // 6 face centers
            pts.Add(new Point3d(min.X, c.Y, c.Z)); // -X face
            pts.Add(new Point3d(max.X, c.Y, c.Z)); // +X face

            pts.Add(new Point3d(c.X, min.Y, c.Z)); // -Y face
            pts.Add(new Point3d(c.X, max.Y, c.Z)); // +Y face

            pts.Add(new Point3d(c.X, c.Y, min.Z)); // -Z face
            pts.Add(new Point3d(c.X, c.Y, max.Z)); // +Z face

            return pts;
        }

        public static List<Point3d> SampleBoundingBox(BoundingBox box, double step)
        {
            var pts = new List<Point3d>();

            if (!box.IsValid) return pts;

            double dx = box.Max.X - box.Min.X;
            double dy = box.Max.Y - box.Min.Y;
            double dz = box.Max.Z - box.Min.Z;

            int nx = (int)Math.Ceiling(dx / step);
            int ny = (int)Math.Ceiling(dy / step);
            int nz = (int)Math.Ceiling(dz / step);

            double stepx = dx / nx;
            double stepy = dy / ny;
            double stepz = dz / nz;

            for (int i = 0; i <= nx; i++)
            {
                double x = box.Min.X + i * stepx;

                for (int j = 0; j <= ny; j++)
                {
                    double y = box.Min.Y + j * stepy;

                    for (int k = 0; k <= nz; k++)
                    {
                        double z = box.Min.Z + k * stepz;

                        pts.Add(new Point3d(x, y, z));
                    }
                }
            }

            return pts;
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

        public static BoundingBox GetAllBox(List<GeometryBase> objs)
        {
            BoundingBox allbox = objs.First().GetBoundingBox(true);
            for (int i = 0; i < objs.Count(); i++)
            {
                if (i == 0) continue;
                var box = objs[i].GetBoundingBox(true);
                allbox.Union(box);
            }
            return allbox;
        }

        public static bool GetCloestPointMaxSearchDistance(List<GeometryBase> objs, out double distance, out BoundingBox allbox)
        {
            distance = 0;
            allbox = new BoundingBox();
            if (objs.Count() < 1) return false;
            allbox = objs.First().GetBoundingBox(true);
            for (int i = 0; i < objs.Count(); i++)
            {
                if (i == 0) continue;
                var box = objs[i].GetBoundingBox(true);
                allbox.Union(box);
            }
            if (allbox.IsValid)
            {
                distance = allbox.Diagonal.Length;
                return true;
            }
            else
            {
                return false;
            }
        }

        //public static NurbsSurface DensifyNurbsSurface(NurbsSurface ns, int magnification)
        //{
        //    if (ns == null || magnification < 1) return ns;

        //    double u0 = ns.Domain(0).T0;
        //    double u1 = ns.Domain(0).T1;
        //    double v0 = ns.Domain(1).T0;
        //    double v1 = ns.Domain(1).T1;

        //    int uOriginal = ns.Points.CountU;
        //    int vOriginal = ns.Points.CountV;

        //    int uNew = uOriginal * magnification;
        //    int vNew = vOriginal * magnification;

        //    // 插入 U 方向
        //    for (int i = 1; i < uNew; i++)
        //    {
        //        double u = u0 + (u1 - u0) * i / uNew;
        //        ns.KnotsU.InsertKnot(u, 1);
        //    }

        //    // 插入 V 方向
        //    for (int j = 1; j < vNew; j++)
        //    {
        //        double v = v0 + (v1 - v0) * j / vNew;
        //        ns.KnotsV.InsertKnot(v, 1);
        //    }

        //    return ns;
        //}


        //public static Point3d IntersectMeshAlongVector(Mesh[] meshes, Point3d fromPt, Vector3d dir)
        //{
        //    if (meshes == null || meshes.Length == 0 || !fromPt.IsValid || !dir.IsValid)
        //        return Point3d.Unset;

        //    dir.Unitize();
        //    double length = 1e6;

        //    // 创建双向中心线（往前后各延伸）
        //    Point3d p1 = fromPt - dir * length;
        //    Point3d p2 = fromPt + dir * length;
        //    Line centerLine = new Line(p1, p2);

        //    Point3d closest = Point3d.Unset;
        //    double minDist = double.MaxValue;

        //    foreach (var mesh in meshes)
        //    {
        //        if (mesh == null) continue;

        //        var hits = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, centerLine);
        //        if (hits == null || hits.Length == 0) continue;

        //        foreach (var hit in hits)
        //        {
        //            double d = fromPt.DistanceTo(hit);
        //            if (d < minDist)
        //            {
        //                minDist = d;
        //                closest = hit;
        //            }
        //        }
        //    }

        //    return closest;
        //}

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

        //public static Point3d IntersectMeshAlongVector(Mesh mesh, Point3d fromPt, Vector3d dir)
        //{
        //    if (mesh == null || !fromPt.IsValid || !dir.IsValid)
        //        return Point3d.Unset;

        //    dir.Unitize();
        //    double length = 1e6;

        //    // 创建双向中心线（往前后各延伸）
        //    Point3d p1 = fromPt - dir * length;
        //    Point3d p2 = fromPt + dir * length;
        //    Line centerLine = new Line(p1, p2);

        //    var hits = Rhino.Geometry.Intersect.Intersection.MeshLine(mesh, centerLine);

        //    if (hits == null || hits.Length == 0)
        //        return Point3d.Unset;

        //    // 找到距离 fromPt 最近的交点（无论正负方向）
        //    Point3d closest = Point3d.Unset;
        //    double minDist = double.MaxValue;

        //    foreach (var hit in hits)
        //    {
        //        double d = fromPt.DistanceTo(hit);
        //        if (d < minDist)
        //        {
        //            minDist = d;
        //            closest = hit;
        //        }
        //    }

        //    return closest;
        //}


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


        public static Result SelectGeometries(
    RhinoDoc doc,
    string prompt,
    ObjectType filter,
    out ObjRef[] objRefs)
        {
            objRefs = null;

            var go = new GetObject();
            go.SetCommandPrompt(string.IsNullOrEmpty(prompt) ? "选择几何体" : prompt);

            go.EnablePreSelect(true, true);

            go.GroupSelect = true;

            go.GeometryFilter = filter;

            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            objRefs = go.Objects().ToArray();

            objRefs = objRefs
                .Where(o => o != null && o.Object() != null)
                .ToArray();

            if (objRefs.Length == 0)
                return Result.Failure;

            doc.Objects.UnselectAll();

            return Result.Success;
        }

    }
}
