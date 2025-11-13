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

namespace MyChangeTools.commands.DualSurfaceMapping
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
                        Min = -1e6,
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
        public bool PreserveStructure { get; set; } = false;

        [Option("快速预览")]
        public bool QuickPreview { get; set; } = true;
        [Option("ShowLogObj")]
        public bool ShowLogObj { get; set; } = false;

        [Option("整体处理Brep")]
        public bool IsProcessBrepTogeTher { get; set; } = false;

        [Option("复制对象")]
        public bool IsCopy { get; set; } = false;
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

            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            objRefs = go.Objects().ToArray();

            objRefs = objRefs
                .Where(o => o != null && o.Object() != null)
                .ToArray();

            if (objRefs.Length == 0)
                return Result.Failure;

            doc.Objects.UnselectAll();

            return Result.Success;
        }


        public static Result SelectSurface(RhinoDoc doc, string prompt, out Brep brep)
        {
            brep = null;
            var rc = RhinoGet.GetOneObject(prompt, false, ObjectType.Brep, out ObjRef objRef);
            if (rc != Result.Success) return rc;

            brep = objRef.Brep();

            if (brep == null) return Result.Failure;

            doc.Objects.UnselectAll();
            return Result.Success;
        }



        public static Result GetProjectVector(
    out Vector3d projectVector
    )
        {
            projectVector = Vector3d.Unset;

            var go = new GetOption();
            go.SetCommandPrompt("选择投影方向 (输入选项或通过两点定义方向, 默认 Z 轴)");

            int optX = go.AddOption("X轴");
            int optY = go.AddOption("Y轴");
            int optZ = go.AddOption("Z轴");
            int optPick = go.AddOption("两点定义");

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
                        //默认使用 Z 轴方向
                        projectVector = Vector3d.ZAxis;
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
                            projectVector = Vector3d.XAxis;
                            RhinoApp.WriteLine($"XAxis作为投影方向");
                            break;
                        }

                        else if (optindex == optY)
                        {
                            projectVector = Vector3d.YAxis;
                            RhinoApp.WriteLine($"YAxis作为投影方向");
                            break;
                        }
                        else if (optindex == optZ)
                        {
                            projectVector = Vector3d.ZAxis;
                            RhinoApp.WriteLine($"ZAxis作为投影方向");
                            break;
                        }
                        else if (optindex == optPick)
                        {
                            if (RhinoGet.GetPoint("选择第一个点", false, out Point3d p1) != Result.Success)
                                return Result.Cancel;
                            
                            if (RhinoGet.GetPoint("选择第二个点", false, out Point3d p2) != Result.Success)
                                return Result.Cancel;

                            projectVector = p2 - p1;
                            if (!projectVector.Unitize())
                            {
                                RhinoApp.WriteLine("两点重合，方向无效。请重新选择方向。");
                                continue; // 继续循环，重新获取选项
                            }
                            break; // 成功获取方向，退出
                        }
                    }
                    else
                    {
                        RhinoApp.WriteLine($"{res}");
                        break; // 非 Option 或 Cancel，退出循环
                    }

                }

            }

            if (projectVector == Vector3d.Unset)
            {
            }
            else if (!projectVector.Unitize())
            {
                return Result.Failure; // 理论上不会发生，除非是零向量
            }
            return Result.Success;
        }




    }
}
