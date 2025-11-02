using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyRhinoSelectTools.Commands.SelectIntersect
{
    public static class SelectionHelper
    {

        private static ObjectType _lastTargetType = ObjectType.Brep;

        public static List<RhinoObject> GetBaseObjects()
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select base object(s)");
            go.GroupSelect = true;
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success) return null;

            var doc = RhinoDoc.ActiveDoc;
            var baseobjsresult = new List<RhinoObject>();
            for (int i = 0; i < go.ObjectCount; i++)
            {
                var robj = doc.Objects.FindId(go.Object(i).ObjectId);
                if (robj != null) baseobjsresult.Add(robj);
            }
            MyRhinoSelectTools.CustomQuickClass.QuickGeometry.PrintObjsSummary(baseobjsresult);
            return baseobjsresult;
        }

        public static bool GetTargetObjectTypeFromUser(RhinoDoc doc, out ObjectType type)
        {
            var opts = new GetOption();
            opts.SetCommandPrompt("Select target type");
            int brepopindex = opts.AddOption("Brep");
            int curveopindex = opts.AddOption("Curve");
            int meshopindex = opts.AddOption("Mesh");
            int pointopindex = opts.AddOption("Point");
            int subdopindex = opts.AddOption("SubD");
            int anyopindex = opts.AddOption("Any");
            opts.Get();
            if (opts.CommandResult() != Result.Success)
            {
                type = _lastTargetType;
                return false;
            }
            int index = opts.OptionIndex();

            switch (index)
            {
                case int i when i == brepopindex:
                    type = ObjectType.Brep;
                    break;

                case int i when i == curveopindex:
                    type = ObjectType.Curve;
                    break;

                case int i when i == meshopindex:
                    type = ObjectType.Mesh;
                    break;

                case int i when i == pointopindex:
                    type = ObjectType.Point;
                    break;

                case int i when i == subdopindex:
                    type = ObjectType.SubD;
                    break;

                case int i when i == anyopindex:
                    type = ObjectType.AnyObject;
                    break;

                default:
                    type = _lastTargetType;
                    break;
            }
            _lastTargetType = type;

            //RhinoApp.WriteLine($"当前目标物体类型是{type}");
            return true;
        }

        public static List<RhinoObject> GetComputeObjects(RhinoDoc doc, ObjectType type, List<RhinoObject> exclude)
        {
            var objs = new List<RhinoObject>();

            // 创建一个Esc检测器（自动释放）
            using (var escHandler = new MyRhinoSelectTools.CustomQuickClass.QuickGetObject.EscapeKeyEventHandler("选择要计算的对象（按 ESC 取消）"))
            {
                // 清空当前选择集
                doc.Objects.UnselectAll();

                var go = new GetObject();
                go.SetCommandPrompt($"Select objects to test ({type}) or press Enter for all");
                go.GeometryFilter = type;
                go.GetMultiple(1, 0);

                // 检查是否按了 ESC
                if (escHandler.EscapeKeyPressed)
                {
                    RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
                    return objs; // 空列表
                }

                // 用户选择了对象
                if (go.CommandResult() == Result.Success && go.ObjectCount > 0)
                {
                    for (int i = 0; i < go.ObjectCount; i++)
                    {
                        var robj = doc.Objects.FindId(go.Object(i).ObjectId);
                        if (robj != null && !exclude.Any(e => e.Id == robj.Id))
                            objs.Add(robj);
                    }
                }
                // 用户直接按 Enter 或 空格
                else
                {
                    foreach (var robj in doc.Objects.FindByObjectType(type))
                    {
                        if (!exclude.Any(e => e.Id == robj.Id))
                            objs.Add(robj);
                    }
                }

                MyRhinoSelectTools.CustomQuickClass.QuickGeometry.PrintObjsSummary(objs);
            }

            return objs;
        }

        public static void HighlightAndReport(RhinoDoc doc, HashSet<Guid> ids)
        {
            if (ids.Count == 0)
            {
                RhinoApp.WriteLine("No intersections found.");
                return;
            }

            doc.Objects.UnselectAll();
            foreach (var id in ids)
                doc.Objects.Select(id);
            doc.Views.Redraw();
            RhinoApp.WriteLine($"Found {ids.Count} intersected objects.");
        }
    }
}

