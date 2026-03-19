using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MyChangeTools.commands.RbfDeform
{

    [AttributeUsage(AttributeTargets.Property)]
    public class OptionAttribute : Attribute
    {
        public string DisplayName { get; }

        public OptionAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }

    public static class OptionRegistry
    {
        public abstract class OptionInfo
        {
            public string Name;
            public PropertyInfo Property;
        }

        public class BoolOption : OptionInfo { }
        public class IntOption : OptionInfo { public int Min; public int Max; }
        public class DoubleOption : OptionInfo { public double Min; public double Max; }

        public static List<OptionInfo> GetOptions()
        {
            var result = new List<OptionInfo>();

            foreach (var prop in typeof(SelectionOptions).GetProperties())
            {
                var attr = prop.GetCustomAttribute<OptionAttribute>();
                if (attr == null)
                    continue;

                if (prop.PropertyType == typeof(bool))
                {
                    result.Add(new BoolOption
                    {
                        Name = attr.DisplayName,
                        Property = prop
                    });
                }
                else if (prop.PropertyType == typeof(int))
                {
                    result.Add(new IntOption
                    {
                        Name = attr.DisplayName,
                        Property = prop,
                        Min = 0,
                        Max = 9999
                    });
                }
                else if (prop.PropertyType == typeof(double))
                {
                    result.Add(new DoubleOption
                    {
                        Name = attr.DisplayName,
                        Property = prop,
                        Min = 0,
                        Max = 1e6
                    });
                }
            }
            return result;
        }
    }

    public class SelectionOptions
    {

        [Option("保持结构")]
        public bool PreserveStructure { get; set; } = true;

        [Option("快速预览")]
        public bool QuickPreview { get; set; } = false;

        [Option("复制对象")]
        public bool IsCopy { get; set; } = false;

        [Option("线性系统")]
        public bool RBFAddLinearSystem { get; set; } = false;

        [Option("基准和目标曲线采样点以参数匹配")]
        public bool CurveSampleByParameter { get; set; } = false;

        [Option("网格点以网格坐标系匹配")]
        public bool MatchMeshByCoordinate { get; set; } = true;

        [Option("采样点影响半径")]
        public double InfectRadius { get; set; } = 0; // 0 代表不限制影响范围 

        [Option("非严格影响半径")]
        public bool IsSoftInfectRadius { get; set; } = false; //


        [Option("在曲线上的采样点距离")]
        public double CurveSampleDistance { get; set; } = 1.0;

        [Option("Tolerance")]
        public double Tolerance { get; set; } = 0.01;

        //UseMyGeomMorph

        [Option("UseMyGeomMorph")]
        public bool UseCustomMorph { get; set; } = false;

        [Option("MyGeomMorphShrinkSurfaceToEdge")]
        public bool ShrinkSurfaceToEdge { get; set; } = false;

        [Option("MyGeomMorphRebuildFU")]
        public int RebuildFaceUCount { get; set; } = 0;

        [Option("MyGeomMorphRebuildFV")]
        public int RebuildFaceVCount { get; set; } = 0;

        [Option("MyGeomMorphRebuildCT")]
        public int RebuildCurveCount { get; set; } = 0;

    }
    public class Selection
    {

        public static SelectionOptions ProcessOption = new SelectionOptions();


        public static Result SelectGeometries(
    RhinoDoc doc,
    string prompt,
    ObjectType filter,
    out ObjRef[] objRefs)
        {
            objRefs = null;

            var go = new GetObject();
            go.SetCommandPrompt(string.IsNullOrEmpty(prompt) ? "选择几何体" : prompt);

            go.EnablePreSelect(true, true);

            go.GroupSelect = true;

            go.GeometryFilter = filter;

            //关闭选择子物件
            go.SubObjectSelect = false;

            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            objRefs = go.Objects().ToArray();

            objRefs = objRefs
                .Where(o => o != null && o.Object() != null && o.Object().IsValid)
                .ToArray();
            
            var typeCounts = objRefs.GroupBy(o => o.Object().ObjectType).Select(g => new { Type = g.Key, Count = g.Count() });
            foreach (var typeCount in typeCounts)            {
                RhinoApp.WriteLine($"You Selected {typeCount.Count} {typeCount.Type.ToString()}");
            }

            if (objRefs.Length == 0)
                return Result.Failure;

            doc.Objects.UnselectAll();

            return Result.Success;
        }


        public static Result SelectOneGeom(RhinoDoc doc, string prompt, out ObjRef objRef, ObjectType type)
        {
            objRef = null;
            var rc = RhinoGet.GetOneObject(prompt, false, type, out objRef);
            if (rc != Result.Success) return rc;
            doc.Objects.UnselectAll();
            return Result.Success;
        }


        public static Result GetVector(
        out List<Vector3d> projectVectors
        )
        {
            projectVectors = new List<Vector3d>();

            var go = new GetOption();
            go.SetCommandPrompt("选择最终移动的限制方向 (Enter 默认 Unset)");
            int optX = go.AddOption("X轴");
            int optY = go.AddOption("Y轴");
            int optZ = go.AddOption("Z轴");
            int optPick = go.AddOption("两点定义");
            int optXY = go.AddOption("XY底面");
            int optYZ = go.AddOption("YZ侧面");
            int optXZ = go.AddOption("XZ前面");



            var boolMap = new Dictionary<string, OptionToggle>();
            var intMap = new Dictionary<string, OptionInteger>();
            var doubleMap = new Dictionary<string, OptionDouble>();
            var otherOptList = OptionRegistry.GetOptions();

            foreach (var opt in otherOptList)
            {
                switch (opt)
                {
                    case OptionRegistry.BoolOption b:
                        {
                            bool cur = (bool)opt.Property.GetValue(ProcessOption);
                            var toggle = new OptionToggle(cur, "No", "Yes");
                            int optIndeex = go.AddOptionToggle(opt.Name, ref toggle);
                            boolMap[opt.Name] = toggle;
                        }
                        break;

                    case OptionRegistry.IntOption i:
                        {
                            int cur = (int)opt.Property.GetValue(ProcessOption);
                            var oh = new OptionInteger(cur, i.Min, i.Max);
                            int optIndeex = go.AddOptionInteger(opt.Name, ref oh);
                            intMap[opt.Name] = oh;
                        }
                        break;

                    case OptionRegistry.DoubleOption d:
                        {
                            double cur = (double)opt.Property.GetValue(ProcessOption);
                            var oh = new OptionDouble(cur, d.Min, d.Max);
                            int optIndeex = go.AddOptionDouble(opt.Name, ref oh);
                            doubleMap[opt.Name] = oh;
                        }
                        break;
                }
            }

            using (var escHandler = new Mylib.CommandHandler.EscapeKeyEventHandler("（按 ESC 取消）"))

            {
                while (true)
                {
                    var res = go.Get();
                    if (escHandler.EscapeKeyPressed)
                    {
                        RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
                        return Result.Nothing;
                    }

                    if (res == GetResult.Cancel)
                    {
                        //默认使用none , use mesh normal for per point
                        RhinoApp.WriteLine("Vector Unset");
                        break;
                    }

                    if (res == GetResult.Option)
                    {
                        var chosen = go.Option();
                        string name = chosen.EnglishName;
                        int optindex = chosen.Index;

                        // 处理自定义选项
                        foreach (var opt in otherOptList)
                        {
                            if (opt.Name != name) continue;

                            switch (opt)
                            {
                                case OptionRegistry.BoolOption b:
                                    opt.Property.SetValue(ProcessOption, boolMap[name].CurrentValue);
                                    RhinoApp.WriteLine($"{name}: {boolMap[name].CurrentValue}");
                                    break;
                                case OptionRegistry.IntOption i:
                                    opt.Property.SetValue(ProcessOption, intMap[name].CurrentValue);
                                    RhinoApp.WriteLine($"{name}: {intMap[name].CurrentValue}");
                                    break;
                                case OptionRegistry.DoubleOption d:
                                    opt.Property.SetValue(ProcessOption, doubleMap[name].CurrentValue);
                                    RhinoApp.WriteLine($"{name}: {doubleMap[name].CurrentValue}");
                                    break;
                            }
                        }

                        // 处理内置方向选项
                        if (optindex == optX)
                        {
                            //projectVector = Vector3d.XAxis;
                            projectVectors.Add(Vector3d.XAxis);
                            RhinoApp.WriteLine($"XAxis作为方向");
                            break;
                        }

                        else if (optindex == optY)
                        {
                            projectVectors.Add(Vector3d.YAxis);
                            RhinoApp.WriteLine($"YAxis作为方向");
                            break;
                        }
                        else if (optindex == optZ)
                        {
                            projectVectors.Add(Vector3d.ZAxis);
                            RhinoApp.WriteLine($"ZAxis作为方向");
                            break;
                        }
                        else if (optindex == optPick)
                        {
                            if (RhinoGet.GetPoint("选择第一个点", false, out Point3d p1) != Result.Success)
                                return Result.Cancel;

                            if (RhinoGet.GetPoint("选择第二个点", false, out Point3d p2) != Result.Success)
                                return Result.Cancel;

                            var projectVector = p2 - p1;
                            if (!projectVector.Unitize())
                            {
                                RhinoApp.WriteLine("两点重合，方向无效。请重新选择方向。");
                                continue; // 继续循环，重新获取选项
                            }
                            projectVectors.Add(projectVector);
                            break; // 成功获取方向，退出
                        }
                        else if (optindex == optXY)
                        {
                            projectVectors.Add(Vector3d.XAxis);
                            projectVectors.Add(Vector3d.YAxis);
                            RhinoApp.WriteLine($"move on xy  plane");
                            break;
                        }

                        else if (optindex == optYZ)
                        {

                            projectVectors.Add(Vector3d.YAxis);
                            projectVectors.Add(Vector3d.ZAxis);
                            RhinoApp.WriteLine($"move on yz side plane");
                            break;
                        }
                        else if (optindex == optXZ)
                        {

                            projectVectors.Add(Vector3d.XAxis);
                            projectVectors.Add(Vector3d.ZAxis);
                            RhinoApp.WriteLine($"move on xz front plane");
                            break;
                        }
                    }
                    else
                    {
                        RhinoApp.WriteLine($"{res}");
                        break; // 非 Option 或 Cancel，退出循环
                    }

                }

            }
            foreach (var projectVector in projectVectors)
            {
                {
                    if (projectVector == Vector3d.Unset)
                    {
                        RhinoApp.WriteLine("方向无效。请重新选择方向。");
                        return Result.Failure; // 理论上不会发生

                    }
                    else if (!projectVector.Unitize())
                    {
                        RhinoApp.WriteLine("方向无效。请重新选择方向。");
                        return Result.Failure; // 理论上不会发生，除非是零向量
                    }
                }
                
            }
            return Result.Success;

        }

    }

}
