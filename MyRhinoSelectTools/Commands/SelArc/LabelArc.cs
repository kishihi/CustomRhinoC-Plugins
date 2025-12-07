using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Generic;

namespace MyRhinoSelectTools.Commands.SelArc
{
    internal class LabelArc
    {
        static public TextDot AddRadiusDot(RhinoObject arcob)
        {
            var arc = arcob.Geometry as ArcCurve;
            if (arc == null) return null;
            var pt = arc.PointAtNormalizedLength(0.5);
            var r = arc.Radius;
            string text = $"{r:F2}";
            return new TextDot(text, pt);
        }

        static public Result RunLabelArc(RhinoDoc doc,List<Rhino.DocObjects.RhinoObject> arcs)
        {
            foreach (var item in arcs)
            {
                TextDot td = AddRadiusDot(item);
                if (td == null) continue;
                doc.Objects.AddTextDot(td);
            }
            return Result.Success;
        }
    }
}
