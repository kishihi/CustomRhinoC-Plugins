using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;

namespace MyRhinoSelectTools.Commands.SelectSimilarCurve
{
    public class SelectSimilarCurve : Command
    {

        public override string EnglishName => "SelectSimilarCurve";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            //1

            var go = new GetObject();
            go.SetCommandPrompt("Select base curve");
            go.GeometryFilter = ObjectType.Curve;

            // 添加选项
            double initSimiliarThreshold = 0.9;
            bool initUseProjection = false;

            OptionDouble opthreshold = new OptionDouble(initSimiliarThreshold);
            OptionToggle opuseProjection = new OptionToggle(initUseProjection, "No", "Yes");

            int optThresholdindex = go.AddOptionDouble("SimilarityThreshold", ref opthreshold);
            int optProjectionindex = go.AddOptionToggle("UseProjection", ref opuseProjection);

            double similiarThreshold = initSimiliarThreshold;
            bool useProjection = initUseProjection;

            while (true)
            {
                var res = go.Get();
                if (res == GetResult.Option)
                {
                    similiarThreshold = opthreshold.CurrentValue;
                    useProjection = opuseProjection.CurrentValue;
                    continue; // 修改选项后继续获取
                }
                if (res != GetResult.Object)
                    return Result.Cancel;
                break;
            }

            //var objRef = go.Object(0);
            //var baseCurve = objRef?.Curve();\
            var baseCurveRef = go.Object(0);

            RhinoApp.WriteLine($"阈值: {similiarThreshold}");

            RhinoApp.WriteLine($"是否计算三侧投影方向: {useProjection}");

            //2 
            var curves = RhinoDoc.ActiveDoc.Objects.FindByObjectType(ObjectType.Curve);

            //3  

            //List<Guid> curveIds = curves.Select(o => o.Id).ToList();

            var selectedIds = new List<Guid>();

            foreach (var curve in curves)
            {
                //Point3d centerpt = MyRhinoSelectTools.Commands.SelectAboveSurface.SelectAboveSurface.GetCentroidFast(curve.Geometry);
                //Point3d centerptbase = MyRhinoSelectTools.Commands.SelectAboveSurface.SelectAboveSurface.GetCentroidFast(baseCurveRef.Geometry());
                if (curve.Id == baseCurveRef.ObjectId)
                {
                    continue;
                }
                Double similiarcc = CurveCompute.CurveSimilarity(curve.Geometry as Curve, baseCurveRef.Geometry() as Curve);
                if (similiarcc >= similiarThreshold)

                {
                    selectedIds.Add(curve.Id);
                }
                MyRhinoSelectTools.Commands.SelectIntersect.GeometryHelper.AddTextDotInCenter(curve.Geometry, $"{similiarcc:F3}");
            }

            if (selectedIds.Count > 0)
            {
                RhinoDoc.ActiveDoc.Objects.Select(selectedIds);
                RhinoApp.WriteLine($"已选择 {selectedIds.Count} 个相似曲线。");
                RhinoDoc.ActiveDoc.Views.Redraw();
            }
            else
            {
                RhinoApp.WriteLine("未找到相似曲线。");
            }



            return Result.Success;
        }


    }
}