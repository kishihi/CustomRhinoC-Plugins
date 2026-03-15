using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace MyChangeTools.commands.RbfDeform
{
    public class RbfDeform : Command
    {
        public RbfDeform()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RbfDeform Instance { get; private set; }

        public override string EnglishName => "RbfDeform";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var rc = Selection.SelectGeometries(doc, "Select geoms to flow", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectGeometries(doc, "Select baseCurves", ObjectType.Curve, out ObjRef[] baseCurveRfs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectGeometries(doc, "Select targetCurves, the order and amount must correspond with baseCurves", ObjectType.Curve, out ObjRef[] targetCurvesRfs);
            if (rc != Result.Success) return Result.Failure;

            var limitedtypes = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point;

            rc = Selection.SelectGeometries(doc, "Select limmited Curves , Meshs, Points, Enter means no limmited", limitedtypes, out ObjRef[] limitedObjRfs);
            if (rc != Result.Success)
                limitedObjRfs = new ObjRef[0];

            rc = Selection.GetVector(out List<Vector3d> MoveVectors);
            if (rc != Result.Success) return Result.Failure;

            Curve[] baseCurves = baseCurveRfs.Where(x => x != null).Select(t => t.Curve()).ToArray();
            Curve[] targetCurves = targetCurvesRfs.Where(x => x != null).Select(t => t.Curve()).ToArray();

            var processor = new GeometryProcessor(doc, objRefs, baseCurves, targetCurves, limitedObjRfs, MoveVectors, Selection.ProcessOption);
            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;
            doc.Views.Redraw();
            return Result.Success;
        }
    }
}