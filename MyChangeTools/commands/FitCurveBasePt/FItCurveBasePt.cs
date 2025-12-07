using Rhino;
using Rhino.Commands;

namespace MyChangeTools.commands.FitCurveBasePt
{
    public class FItCurveBasePt : Command
    {

        public override string EnglishName => "FItCurveBasePt";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            var rs = Sel.GetBaseCurve(out Rhino.DocObjects.ObjRef baseCurverf);
            if (rs != Result.Success)
                return rs;
            rs = Sel.GetBasePoint(out System.Collections.Generic.List<Rhino.Geometry.Point3d> FitPoints);
            if (rs != Result.Success)
            {
                return rs;

            }

            var baseCurve = baseCurverf.Curve();

            if (baseCurve == null)
            {
                Rhino.RhinoApp.WriteLine("基准曲线无效");
                return Result.Failure;
            }

            var process = new FitProcess(Sel.selOp, baseCurve, FitPoints, doc);

            rs = process.Process();

            if (rs != Result.Success)
            {
                return rs;
            }

            return Result.Success;
        }
    }
}