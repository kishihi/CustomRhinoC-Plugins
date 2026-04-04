using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;

namespace MyChangeTools.commands.MyFlowAlongNurbsSurface
{
    public class MyFlowAlongNurbsSurface : Command
    {
        public MyFlowAlongNurbsSurface()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static Command Instance { get; private set; }

        public override string EnglishName => "MyFlowAlongNurbsSurface";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var rc = Selection.SelectGeometries(doc, "Select geoms to flow", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectOneSurface(doc, "Select base surf", out Surface baseSurf);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectOneSurface(doc, "Select target surf", out Surface targetSurf);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.GetNormalVector(out Vector3d NormalVector);
            if (rc != Result.Success) return Result.Failure;

            var processor = new GeometryProcessor(doc, objRefs, baseSurf, targetSurf, NormalVector, Selection.ProcessOption);
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