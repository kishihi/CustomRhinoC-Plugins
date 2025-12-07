using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace MyRhinoSelectTools.Commands.SelectAboveSurface2
{
    public class PopulateGeom
    {
        //
        public static List<Point3d> PopulateGeom3D(GeometryBase geom, int count)
        {
            var pts = new List<Point3d>();
            if (geom == null || count <= 0) //count只对曲线有效
                return pts;

            List<Point3d> candidates = new List<Point3d>();

            // --- 按类型分别处理 ---
            switch (geom)
            {
                case Point pt:
                    pts.Add(pt.Location);
                    return pts;

                case Curve crv:
                    {
                        double step = 1.0 / (count - 1);
                        for (int i = 0; i < count; i++)
                            pts.Add(crv.PointAtNormalizedLength(i * step));
                        return pts;
                    }

                case Brep brep:
                    {
                        var meshes = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
                        if (meshes != null)
                            candidates.AddRange(meshes.SelectMany(m => m.Vertices.Select(v => (Point3d)v)));
                        break;
                    }

                case Surface srf:
                    {
                        var mesh = Mesh.CreateFromSurface(srf, MeshingParameters.FastRenderMesh);
                        if (mesh != null)
                            candidates.AddRange(mesh.Vertices.Select(v => (Point3d)v));
                        break;
                    }

                case SubD subd:
                    {
                        var mesh = Mesh.CreateFromSubD(subd, 1);
                        if (mesh != null)
                            candidates.AddRange(mesh.Vertices.Select(v => (Point3d)v));
                        break;
                    }

                case Mesh mesh:
                    {
                        candidates.AddRange(mesh.Vertices.Select(v => (Point3d)v));
                        break;
                    }

                default:
                    {
                        var bbox = geom.GetBoundingBox(true);
                        pts.Add(bbox.Center);
                        return pts;
                    }
            }

            pts = candidates;//为了精确判断


            return pts;
        }

    }

}