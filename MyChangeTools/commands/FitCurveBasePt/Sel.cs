using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.UI;
using System;
using System.Collections.Generic;

namespace MyChangeTools.commands.FitCurveBasePt
{
    internal class SelOption
    {
        public bool OnZDirection { get; set; } = false;
        public bool SnapMode { get; set; } = false;

        public int FitMode { get; set; } = 2;

    }
    internal class Sel
    {
        public static SelOption selOp = new SelOption();
        public static Result GetBaseCurve(out Rhino.DocObjects.ObjRef curverf)
        {
            curverf = null;
            var gc = new Rhino.Input.Custom.GetObject();
            gc.SetCommandPrompt("选择基准曲线");
            gc.EnablePreSelect(true, true);
            gc.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;
            gc.GroupSelect = true;
            gc.SubObjectSelect = true;

            var OnZDirectionTg = new OptionToggle(selOp.OnZDirection, "No", "Yes");
            var OnZDirectionTgId = gc.AddOptionToggle("OnZDirection", ref OnZDirectionTg);


            var SnapModeTg = new OptionToggle(selOp.SnapMode, "No", "Yes");
            var SnapModeTgId = gc.AddOptionToggle("SnapMode", ref SnapModeTg);

            var FitModeOption = new OptionInteger(selOp.FitMode, 1, 3);

            var FitModeOptionId = gc.AddOptionInteger(new LocalizeStringPair("FitMode", "逼近模式"), ref FitModeOption,"1命令单轨水平断面,2Brep单轨垂直断面,3命令单轨垂直断面");



            using (var escHandler = new Mylib.CommandHandler.EscapeKeyEventHandler("（按 ESC 取消）"))
            {
                while (true)
                {
                    var res = gc.Get();
                    if (res == Rhino.Input.GetResult.Object)

                        break;
                    else if (res == Rhino.Input.GetResult.Option)
                    {
                        var opid = gc.Option().Index;
                        if (opid == OnZDirectionTgId)
                        {
                            selOp.OnZDirection = OnZDirectionTg.CurrentValue;
                            Rhino.RhinoApp.WriteLine($"OnZDirection {selOp.OnZDirection}");
                        }
                        else if (opid == SnapModeTgId)
                        {
                            selOp.SnapMode = SnapModeTg.CurrentValue;
                            Rhino.RhinoApp.WriteLine($"SnapMode {selOp.SnapMode}");
                        }

                        else if (opid == FitModeOptionId)
                        {
                            selOp.FitMode = FitModeOption.CurrentValue;
                            Rhino.RhinoApp.WriteLine($"FitMode {selOp.FitMode}");
                        }

                    }
                    else if (escHandler.EscapeKeyPressed)
                    {
                        return Result.Cancel;
                    }

                }

            }
            curverf = gc.Object(0);
            return Result.Success;

        }

        public static Result GetBasePoint(out List<Point3d> pts)
        {
            pts = new List<Point3d>();
            
            if (selOp.SnapMode)
            {
                List<Guid> tempPts = new List<Guid>();
                using (var escHandler = new Mylib.CommandHandler.EscapeKeyEventHandler("（按 ESC 取消）"))
                {
                    while (true)
                    {
                        var gp = new Rhino.Input.Custom.GetPoint();
                        gp.SetCommandPrompt("选择基准点");
                        gp.PermitObjectSnap(true);
                        gp.PermitOrthoSnap(true);
                        var res = gp.Get();
                        if (res == Rhino.Input.GetResult.Point)
                        {
                            var pt = gp.Point();
                            Guid tempPtid = Rhino.RhinoDoc.ActiveDoc.Objects.AddPoint(pt);
                            tempPts.Add(tempPtid);
                            pts.Add(pt);
                        }
                        else if (escHandler.EscapeKeyPressed)
                        {
                            Rhino.RhinoApp.WriteLine("用户取消了点的选择");
                            tempPts.ForEach(guid => Rhino.RhinoDoc.ActiveDoc.Objects.Delete(guid, true));
                            return Result.Cancel;
                        }
                        else if (res == Rhino.Input.GetResult.Cancel)
                        {
                            break;
                        }
                    }
                }
                tempPts.ForEach(guid => Rhino.RhinoDoc.ActiveDoc.Objects.Delete(guid, true));
            }
            else
            {
                var gp = new Rhino.Input.Custom.GetObject();
                gp.SetCommandPrompt("选择基准点");
                gp.GeometryFilter = Rhino.DocObjects.ObjectType.Point;
                gp.SubObjectSelect = true;
                gp.EnablePreSelect(true, true);
                gp.GroupSelect = true;
                var res = gp.GetMultiple(1, 0);
                if (res == Rhino.Input.GetResult.Object)
                {
                    for (int i = 0; i < gp.ObjectCount; i++)
                    {
                        var objref = gp.Object(i);
                        var pt = objref.Point().Location;
                        pts.Add(pt);
                    }
                }
                else if (res == Rhino.Input.GetResult.Cancel)
                {
                    Rhino.RhinoApp.WriteLine("用户取消了点的选择");
                    return Result.Cancel;
                }
            }

            Rhino.RhinoApp.WriteLine($"用户选择了{pts.Count}个点");

            if (pts.Count > 0)
                return Result.Success;
            else
                return Result.Cancel
        ;
        }
    }
}
