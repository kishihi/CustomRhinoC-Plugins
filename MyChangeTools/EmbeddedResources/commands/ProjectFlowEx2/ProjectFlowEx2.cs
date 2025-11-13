using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MyChangeTools.commands.ProjectFlowEx2
{
    public class ProjectFlowEx2 : Command
    {
        public override string EnglishName => "ProjectFlowEx2";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var rc = Selection.SelectGeometries(doc, "Select curve or surface to flow", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectSurface(doc, "Select base surface", out Brep baseBrep);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectSurface(doc, "Select target surface", out Brep targetBrep);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.GetProjectVector(out Vector3d projectVector);
            if (rc != Result.Success) return Result.Failure;

            var processor = new GeometryProcessor(doc, objRefs, baseBrep, targetBrep, projectVector, Selection.ProcessOption);
            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;


            doc.Views.Redraw();
            return Result.Success;
        }
    }
}