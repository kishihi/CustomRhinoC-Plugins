using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyRhinoSelectTools.CustomQuickClass
{


    

    public class QuickGeometry
    {
        public static void AddTextDotInCenter(GeometryBase gemo, string text)
        {
            BoundingBox bbox1 = gemo.GetBoundingBox(false);
            TextDot t = new TextDot(text, bbox1.Center);
            Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(t);
        }


        public static Point3d GetCentroidFast(GeometryBase geom)
        {
            if (geom == null)
                return Point3d.Unset;

            // 点
            if (geom is Point point)
                return point.Location;

            // 曲线
            if (geom is Curve curve)
            {
                if (curve.IsClosed)
                {
                    var areaProps = AreaMassProperties.Compute(curve);
                    if (areaProps != null)
                        return areaProps.Centroid;
                }
                else
                {
                    return curve.PointAtNormalizedLength(0.5);
                }
            }

            // 其他几何：使用外包盒中心
            var bbox = geom.GetBoundingBox(true);
            return bbox.Center;
        }

        public static void PrintObjsSummary(List<RhinoObject> objs)
        {
            if (objs == null || objs.Count == 0)
            {
                RhinoApp.WriteLine("Empty obj sequence");
            }
            else
            {
                var typeCount = new Dictionary<ObjectType, int>();

                foreach (var obj in objs)
                {
                    if (typeCount.TryGetValue(obj.ObjectType, out int value))
                        typeCount[obj.ObjectType] = ++value;
                    else
                        typeCount[obj.ObjectType] = 1;
                }

                // 构建一行输出字符串
                string summary = $"Total: {objs.Count} | ";
                foreach (var kvp in typeCount)
                {
                    summary += $"{kvp.Key}: {kvp.Value} ";
                }

                RhinoApp.WriteLine(summary);
            }
        }

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


    }
}
