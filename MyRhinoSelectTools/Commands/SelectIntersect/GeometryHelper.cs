using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace MyRhinoSelectTools.Commands.SelectIntersect
{
    public static class GeometryHelper
    {
        public static Brep ToBrepSafe(GeometryBase geom)
        {
            if (geom == null)
                return null;

            if (geom is Brep)
                return geom as Brep;

            if (geom is Extrusion)
                return ((Extrusion)geom).ToBrep();

            if (geom is Surface)
                return ((Surface)geom).ToBrep();

            if (geom is SubD)
                return ((SubD)geom).ToBrep(new SubDToBrepOptions());

            if (geom is Mesh)
                return Brep.CreateFromMesh((Mesh)geom, true);

            return null;
            //return geom switch
            //{
            //    Brep b => b,
            //    Extrusion e => e.ToBrep(),
            //    Surface s => s.ToBrep(),
            //    SubD sd => sd.ToBrep(new SubDToBrepOptions()),
            //    Mesh m => Brep.CreateFromMesh(m, true),
            //    _ => null
            //};
        }

        public static bool Intersects(GeometryBase g1, GeometryBase g2, double tol, double overlapTol)
        {
            try
            {
                // store it if same type
                Curve c1 = null;
                Curve c2 = null;

                Brep b1 = null;
                Brep b2 = null;

                Point p1 = null;
                Point p2 = null;



                // mid data
                Point3d[] intersectPoints;
                Curve[] overlapCurves;
                CurveIntersections intersectCurvesEvents;

                //allocate gemos
                if (g1 is Curve curve1) c1 = curve1;
                if (g2 is Curve curve2) c2 = curve2;

                b1 = ToBrepSafe(g1);
                b2 = ToBrepSafe(g2);

                if (g1 is Point point1) p1 = point1;
                if (g2 is Point point2) p2 = point2;

                // store it if diff type

                Curve c = c1 ?? c2;
                Brep b = b1 ?? b2;
                Point p = p1 ?? p2;

                //same type intersect

                #region brep x brep
                if (b1 != null && b2 != null)
                {
                    Intersection.BrepBrep(b1, b2, tol, out overlapCurves, out intersectPoints);
                    return overlapCurves.Length > 0 || intersectPoints.Length > 0;
                }
                #endregion
                #region curve x curve
                else if (c1 != null && c2 != null)
                {
                    intersectCurvesEvents = Intersection.CurveCurve(c1, c2, tol, overlapTol);
                    return intersectCurvesEvents.Count > 0;
                }

                #endregion
                #region point x point
                else if (p1 != null && p2 != null)
                {
                    return p1.Location.DistanceTo(p2.Location) <= tol;
                }
                #endregion

                //diff type intersect

                #region curve x brep

                else if (c != null && b != null)
                {
                    Intersection.CurveBrep(c, b, tol, out overlapCurves, out intersectPoints);
                    return overlapCurves.Length > 0 || intersectPoints.Length > 0;
                }

                #endregion

                #region curve x point

                else if (c != null && p != null)
                {
                    c.ClosestPoint(p.Location, out double t);
                    return c.PointAt(t).DistanceTo(p.Location) <= tol;
                }

                #endregion

                #region point x brep

                else if (p != null && b != null)
                {
                    return b.ClosestPoint(p.Location).DistanceTo(p.Location) <= tol;

                }
                else
                {
                    //BoundingBox bbox1 = g1.GetBoundingBox(false);
                    //BoundingBox bbox2 = g2.GetBoundingBox(false);

                    //TextDot t1 = new TextDot($"不支持类型相交 {g1.ObjectType} x {g2.ObjectType}", bbox1.Center);
                    //TextDot t2 = new TextDot($"不支持类型相交 {g2.ObjectType} x {g1.ObjectType}", bbox2.Center);

                    //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(t1);
                    //Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(t2);

                }
                #endregion

            }
            catch { }
            return false;
        }

        public static void AddTextDotInCenter(GeometryBase gemo, string text)
        {
            BoundingBox bbox1 = gemo.GetBoundingBox(false);
            TextDot t = new TextDot(text, bbox1.Center);
            Rhino.RhinoDoc.ActiveDoc.Objects.AddTextDot(t);
        }
    }
}
