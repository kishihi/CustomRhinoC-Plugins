using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MyChangeTools.commands.FlowAlongMesh2
{
    public class FlowAlongMesh : Command
    {
        public FlowAlongMesh()
        {
            Instance = this;
        }

        public static FlowAlongMesh Instance { get; private set; }

        public override string EnglishName => "FlowAlongMesh2";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.

            var rc = Selection.SelectGeometries(doc, "Select geoms to flow", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectOneMesh(doc, "Select base  mesh", out ObjRef baseMeshRef);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectOneMesh(doc, "Select target mesh", out ObjRef targetMeshRef);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.GetNormalVector(out Vector3d NormalVector);
            if (rc != Result.Success) return Result.Failure;

            if (baseMeshRef.Mesh().Vertices.Count != targetMeshRef.Mesh().Vertices.Count 
                ||
                baseMeshRef.Mesh().Faces.Count != targetMeshRef.Mesh().Faces.Count)
            {
                RhinoApp.WriteLine("基准网格和目标网格的顶点和面数需要相同,否则造成未预料的变形结果. 请重新选择");
                return Result.Failure;
            }

            var processor = new GeometryProcessor(doc, objRefs, baseMeshRef, targetMeshRef, NormalVector, Selection.ProcessOption);
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