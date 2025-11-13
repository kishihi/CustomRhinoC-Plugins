using Rhino;
using Rhino.Geometry;
using System.Collections.Generic;

namespace MyChangeTools.commands.ProjectFlow
{
    public static class GeometryUtils
    {

        public static List<Curve> GetFaceBorderCurves(BrepFace face)
        {
            var borders = new List<Curve>();
            foreach (var loop in face.Loops)
            {
                borders.Add(loop.To3dCurve());
            }
            return borders;
        }

        public static List<Point3d> SamplePointsOnBrepFace(BrepFace face, int u_count = 5, int v_count = 5, double border_dist = 0.1)
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

        /// <summary>
        /// Densify a NurbsCurve by increasing its control points.
        /// </summary>
        /// <param name="nc">The input NurbsCurve.</param>
        /// <param name="magnification">Number of times to increase control points.</param>
        /// <returns>The densified NurbsCurve.</returns>
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

        /// <summary>
        /// Densify a NurbsSurface by increasing its control points in U and V directions.
        /// </summary>
        /// <param name="ns">The input NurbsSurface.</param>
        /// <param name="magnification">Number of times to increase control points in each direction.</param>
        /// <returns>The densified NurbsSurface.</returns>
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



        public static Point3d IntersectSurfaceAlongVector(Brep surface, Point3d fromPt, Vector3d dir)
        {
            //dir.IsTiny()
            if (surface == null || !fromPt.IsValid || !dir.IsValid) return Point3d.Unset;

            Brep brep = ToBrepSafe(surface);
            if (brep == null || !brep.IsValid) return Point3d.Unset;

            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            Line line = new Line(fromPt, dir, 1e6);
            Curve lineCurve = new LineCurve(line);

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

            Line revLine = new Line(fromPt, -dir, 1e6);
            Curve revCurve = new LineCurve(revLine);
            if (Rhino.Geometry.Intersect.Intersection.CurveBrep(revCurve, brep, tol, out _, out Point3d[] revPoints))
            {
                if (revPoints != null && revPoints.Length > 0)
                {
                    foreach (var p in revPoints)
                    {
                        if (p != Point3d.Unset) return p;
                    }
                }
            }

            return Point3d.Unset;
        }

        public static Point3d TransformPointAlongDirection(Point3d A, Point3d B1, Point3d B2, Vector3d vectorA)
        {
            Vector3d move = B2 - B1;
            if (!vectorA.Unitize()) return Point3d.Unset;

            double moveLen = move * vectorA;
            Vector3d projectedMove = vectorA * moveLen;
            return A + projectedMove;
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