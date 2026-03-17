using System;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;

namespace MyChangeTools.commands.Mvc2DOutlineReplace
{
    public class Mvc2DOutlineReplace : Command
    {
        public Mvc2DOutlineReplace()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static Mvc2DOutlineReplace Instance { get; private set; }

        public override string EnglishName => "Mvc2DOutlineReplace";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            var rc = Selection.SelectGeometries(doc, "Select geoms to replace outline", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectOneGeom(doc, "Select old outline curve", out ObjRef oldOutlineRf, ObjectType.Curve);

            if (rc != Result.Success) return Result.Failure;

            rc = Selection.SelectOneGeom(doc, "Select new outline curve", out ObjRef newOutlineRf, ObjectType.Curve);

            if (rc != Result.Success) return Result.Failure;

            rc = Selection.GetVector();

            if (rc != Result.Success) return Result.Failure;

            var processor = new GeometryProcessor(doc, objRefs, oldOutlineRf.Curve(), newOutlineRf.Curve(), Selection.ProcessOption);

            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;

            doc.Views.Redraw();

            // TODO: complete command.
            return Result.Success;
        }
    }
}