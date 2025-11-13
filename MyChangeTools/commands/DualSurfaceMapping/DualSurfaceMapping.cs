using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;

namespace MyChangeTools.commands.DualSurfaceMapping
{
    public class DualSurfaceMapping : Command
    {

        public override string EnglishName => "DualSurfaceMapping";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var rc = Selection.SelectGeometries(doc, "Select objs to flow", ObjectType.AnyObject, out ObjRef[] processObjRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectSurface(doc, "Select 面 A1", out Brep BrepA1);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectSurface(doc, "Select 面 A2", out Brep BrepA2);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectSurface(doc, "Select 面 B1", out Brep BrepB1);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectSurface(doc, "Select 面 B2", out Brep BrepB2);
            if (rc != Result.Success) return Result.Failure;


            rc = Selection.GetProjectVector(out Vector3d projectVector);
            if (rc != Result.Success) return Result.Failure;

            var processor = new GeometryProcessor(doc, processObjRefs, BrepA1, BrepA2, BrepB1, BrepB2, projectVector, Selection.ProcessOption);
            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;


            doc.Views.Redraw();
            return Result.Success;
        }
    }
}