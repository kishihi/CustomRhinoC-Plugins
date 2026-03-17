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

            var limitedtypes = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;

            rc = Selection.SelectGeometries(doc, "Select base Curves, Meshs, Points, NurbsSurfaces, the order and type and amount must correspond with targetObjs",
                limitedtypes, out ObjRef[] baseObjRfs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectGeometries
                (doc, "Select target Curves, Meshs, Points, NurbsSurfaces, the order and type and amount must correspond with baseObjs",
                limitedtypes, out ObjRef[] targetObjRfs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectGeometries
                (doc, "Select limmited Curves , Meshs, Points, Enter means no limmited",
                limitedtypes, out ObjRef[] limitedObjRfs);
            if (rc != Result.Success)
                limitedObjRfs = new ObjRef[0];

            rc = Selection.GetVector(out List<Vector3d> MoveVectors);
            if (rc != Result.Success) return Result.Failure;

            var processor = new GeometryProcessor(doc, objRefs, baseObjRfs, targetObjRfs, limitedObjRfs, MoveVectors, Selection.ProcessOption);
            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;
            doc.Views.Redraw();
            return Result.Success;
        }
    }
}