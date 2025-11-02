using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;

namespace MyChangeTools.ExtendCurves
{
    public class ExtendCurves : Command
    {

        public override string EnglishName => "ExtendCurves";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.
            //var sc = new Selection();
            var rc = Selection.GetCurves(out ObjRef[] curves);
            if (rc != Result.Success)
            {
                return Result.Failure;
            }

            rc = CurveProcess.Extend(doc, curves, Selection.extendOptions);

            if (rc != Result.Success)
            {
                return Result.Failure;
            }


            return Result.Success;
        }
    }
}