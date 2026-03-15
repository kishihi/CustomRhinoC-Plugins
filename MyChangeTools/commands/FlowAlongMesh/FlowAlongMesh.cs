using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MyChangeTools.commands.FlowAlongMesh
{
    public class FlowAlongMesh : Command
    {
        public FlowAlongMesh()
        {
            Instance = this;
        }

        public static FlowAlongMesh Instance { get; private set; }

        public override string EnglishName => "FlowAlongMesh";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.

            var rc = Selection.SelectGeometries(doc, "Select geoms to flow", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectMesh(doc, "Select base  mesh", out Mesh baseMesh);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectMesh(doc, "Select target mesh", out Mesh targetMesh);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.GetNormalVector(out Vector3d NormalVector);
            if (rc != Result.Success) return Result.Failure;

            var processor = new GeometryProcessor(doc, objRefs, baseMesh, targetMesh, NormalVector, Selection.ProcessOption);
            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;
            doc.Views.Redraw();
            return Result.Success;
        }
    }
}