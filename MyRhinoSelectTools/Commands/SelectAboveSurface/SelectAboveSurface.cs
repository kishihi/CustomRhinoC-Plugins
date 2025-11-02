using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyRhinoSelectTools.Commands.SelectAboveSurface
{
    public class SelectAboveSurface : Command
    {
        public override string EnglishName => "SelectAboveSurface";

        //}
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1. 选择基准曲面
            Result rc1 = RhinoGet.GetOneObject("选择基准曲面", false, ObjectType.Brep, out ObjRef baseBrepRef);
            if (rc1 != Result.Success || baseBrepRef == null)
                return rc1;

            // 2. 获取方向选项
            GetString gs = new GetString();
            gs.SetCommandPrompt("选择质心在基准面的哪个位置的物体(按Enter默认上方)");
            gs.AcceptNothing(false);
            gs.AddOption("上方");
            gs.AddOption("下方");
            gs.AddOption("在面上");
            gs.Get();
            String directionStr = gs.Option()?.EnglishName ?? "上方";
            int direction = directionStr == "上方" ? 1 : (directionStr == "下方" ? -1 : 0);

            // 3. 选择要检查的对象

            ObjRef[] objRefs;

            using (var eschandler = new CustomQuickClass.QuickGetObject.EscapeKeyEventHandler("ESC退出命令"))
            {
                //清除选择集
                doc.Objects.UnselectAll();
                Result rc2 = RhinoGet.GetMultipleObjects("选择要检查的对象(Enter选择全部,ESC取消命令)", false, ObjectType.AnyObject, out objRefs);

                if (eschandler.EscapeKeyPressed)
                {
                    RhinoApp.WriteLine("用户取消命令");
                    return Result.Failure;
                }

                else
                {
                    if (objRefs != null && objRefs.Length > 0)
                    {
                        RhinoApp.WriteLine($"用户选择了{objRefs.Length}个对象进行检查");
                    }
                    else
                    {
                        // 把所有对象都包装成 ObjRef 并生成数组
                        objRefs = doc.Objects
                            .Where(o => o != null)
                            .Select(o => new ObjRef(o))
                            .ToArray();

                        RhinoApp.WriteLine($"未手动选择，自动选择全部对象，共 {objRefs.Length} 个。");
                    }
                }
            }

            // 4. 遍历对象
            var sc = SurfaceCompute.ComputeDirection(
                doc, baseBrepRef, objRefs, out ConcurrentBag<Guid> abovedObjIds, out ConcurrentBag<Guid> belowObjIds, out ConcurrentBag<Guid> onObjids, out ConcurrentBag<(Point3d pt, string text)> textDots);
            
            if(sc!=Result.Success)
            {
                RhinoApp.WriteLine("计算方向失败");
                return sc;
            }

            // 在主线程中添加 TextDot
            foreach (var (pt, text) in textDots)
            {
                CustomQuickClass.QuickGeometry.AddTextDotInCenter(new Rhino.Geometry.Point(pt), text);
            }

            // 5. 根据用户选择选中对象
            List<Guid> resultObjIds = (direction == 1 ? abovedObjIds : (direction == -1 ? belowObjIds : onObjids)).ToList();
            if (resultObjIds.Count > 0)
            {
                doc.Objects.UnselectAll();
                foreach (var id in resultObjIds)
                    doc.Objects.Select(id);
                doc.Views.Redraw();
                List<RhinoObject> objs = resultObjIds
                            .Select(id => doc.Objects.Find(id))
                            .Where(obj => obj != null)
                            .ToList();

                CustomQuickClass.QuickGeometry.PrintObjsSummary(objs);

            }
            else
            {
                RhinoApp.WriteLine("未找到符合条件的物体。");
            }

            return Result.Success;
        }



    }
}
