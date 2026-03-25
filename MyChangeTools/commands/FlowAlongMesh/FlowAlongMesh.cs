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

            if (baseMesh.Vertices.Count != targetMesh.Vertices.Count || baseMesh.Faces.Count != targetMesh.Faces.Count)
            {
                RhinoApp.WriteLine("基准网格和目标网格的顶点和面数需要相同,否则造成未预料的变形结果. 请重新选择");
                return Result.Failure;
            }

            var processor = new GeometryProcessor(doc, objRefs, baseMesh, targetMesh, NormalVector, Selection.ProcessOption);
            System.Threading.Tasks.Task.Run(() =>
            {
                return processor.Process();
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Eto.Forms.MessageBox.Show($"错误：{task.Exception?.InnerException?.Message}");
                    return;
                }
                else
                {
                    processor.ApplyResultToDoc(task.Result);
                }

            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            if (rc != Result.Success) return Result.Failure;
            doc.Views.Redraw();
            return Result.Success;
        }


    }
}