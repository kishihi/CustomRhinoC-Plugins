using Rhino;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;

namespace MyRhinoSelectTools.Commands.SelectIntersect
{
    public static class GeometryUtils
    {
        public static HashSet<Guid> ComputeIntersections(
            RhinoDoc doc, List<RhinoObject> baseObjs, List<RhinoObject> computeObjs)
        {
            var result = new HashSet<Guid>();
            double tol = doc.ModelAbsoluteTolerance;
            double overlapTol = tol;

            foreach (var b in baseObjs)
            {
                foreach (var c in computeObjs)
                {
                    if (c.Id == b.Id) continue;

                    var g1 = b.Geometry;
                    var g2 = c.Geometry;
                    if (g1 == null || g2 == null) continue;

                    if (GeometryHelper.Intersects(g1, g2, tol, overlapTol))
                        result.Add(c.Id);
                }
            }
            return result;
        }
    }
}
