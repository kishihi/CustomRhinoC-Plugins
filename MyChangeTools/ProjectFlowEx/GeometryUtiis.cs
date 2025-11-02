using Rhino.Geometry;

namespace MyChangeTools.ProjectFlowEx
{
    public static class GeometryUtils
    {

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
