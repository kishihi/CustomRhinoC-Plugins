using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System.Linq;

namespace MyChangeTools.ProjectFlowEx2
{
    public class SelectionOptions
    {
        public bool IsNormalvectorAsProjectVector { get; set; } = false;
        public bool IsFlowOnTargetBaseNormalVector { get; set; } = true;
        public int ControlPointMagnification { get; set; } = 1;
        public bool PreserveStructure { get; set; } = false;

        public bool QuickPreview { get; set; } = true; // 开启快速变形

        public bool IsProcessBrepTogeTher { get; set; } = true;//整体处理brep

        public bool IsCopy { get; set; } = false;//复制对象

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
            int optNormal = go.AddOption("最近点法向");


            var toggleIsFlowOnTargetBaseNormalVector = new OptionToggle(ProcessOption.IsFlowOnTargetBaseNormalVector, "No", "Yes");
            int optFlowOnNormal = go.AddOptionToggle("在目标曲面法向方向排列", ref toggleIsFlowOnTargetBaseNormalVector);

            var opIntCp = new OptionInteger(ProcessOption.ControlPointMagnification);
            int opControlPointMagnification = go.AddOptionInteger("控制点放大倍数", ref opIntCp);


            var togglePreserveStructure = new OptionToggle(ProcessOption.PreserveStructure, "No", "Yes");
            int optPreserveStructure = go.AddOptionToggle("保持结构", ref togglePreserveStructure);


            // 新增两个 toggle：QuickPreview 和 IsProcessBrepTogeTher
            var toggleQuickPreview = new OptionToggle(ProcessOption.QuickPreview, "No", "Yes");
            int optQuickPreview = go.AddOptionToggle("快速预览模式", ref toggleQuickPreview);

            var toggleProcessTogether = new OptionToggle(ProcessOption.IsProcessBrepTogeTher, "No", "Yes");
            int optProcessTogether = go.AddOptionToggle("整体处理Brep", ref toggleProcessTogether);

            var toggleIsCopy = new OptionToggle(ProcessOption.IsCopy, "No", "Yes");
            int optIsCopy = go.AddOptionToggle("复制", ref toggleIsCopy);






            using (var escHandler = new MyChangeTools.Mylib.CommandHandler.EscapeKeyEventHandler("（按 ESC 取消）"))

            {
                while (true)
                {
                    var res = go.Get();
                    if (res == GetResult.Option)
                    {
                        int index = go.OptionIndex();
                        if (index == optX)
                            projectVector = Vector3d.XAxis;
                        else if (index == optFlowOnNormal)
                        {
                            ProcessOption.IsFlowOnTargetBaseNormalVector = toggleIsFlowOnTargetBaseNormalVector.CurrentValue;
                            RhinoApp.WriteLine($"在目标曲面法向方向排列:{ProcessOption.IsFlowOnTargetBaseNormalVector}");
                            continue;
                        }
                        else if (index == optIsCopy)
                        {
                            ProcessOption.IsCopy = toggleIsCopy.CurrentValue;
                            RhinoApp.WriteLine($"复制对象:{ProcessOption.IsCopy}");
                            continue;
                        }
                        else if (index == optPreserveStructure)
                        {
                            ProcessOption.PreserveStructure = togglePreserveStructure.CurrentValue;
                            RhinoApp.WriteLine($"保持结构:{ProcessOption.PreserveStructure}");
                            continue;
                        }

                        else if (index == optQuickPreview)
                        {
                            ProcessOption.QuickPreview = toggleQuickPreview.CurrentValue;
                            RhinoApp.WriteLine($"快速预览模式: {ProcessOption.QuickPreview}");
                            continue;
                        }
                        else if (index == optProcessTogether)
                        {
                            ProcessOption.IsProcessBrepTogeTher = toggleProcessTogether.CurrentValue;
                            RhinoApp.WriteLine($"整体处理Brep: {ProcessOption.IsProcessBrepTogeTher}");
                            continue;
                        }

                        else if (index == opControlPointMagnification)
                        {
                            ProcessOption.ControlPointMagnification = opIntCp.CurrentValue;
                            RhinoApp.WriteLine($"控制点放大倍数:{ProcessOption.ControlPointMagnification}");
                            continue;

                        }

                        else if (index == optY)
                            projectVector = Vector3d.YAxis;
                        else if (index == optZ)
                            projectVector = Vector3d.ZAxis;
                        else if (index == optNormal)
                        {
                            projectVector = Vector3d.Unset;
                            ProcessOption.IsNormalvectorAsProjectVector = true;
                            RhinoApp.WriteLine($"基准面上最近点法向作为投影方向:{ProcessOption.IsNormalvectorAsProjectVector}");
                            return Result.Success;
                        }
                        else if (index == optPick)
                        {
                            // 通过两点定义方向
                            if (RhinoGet.GetPoint("选择第一个点", false, out Point3d p1) != Result.Success)
                                return Result.Cancel;
                            if (RhinoGet.GetPoint("选择第二个点", false, out Point3d p2) != Result.Success)
                                return Result.Cancel;

                            projectVector = p2 - p1;
                            if (!projectVector.Unitize())
                            {
                                RhinoApp.WriteLine("两点重合，方向无效。请重新选择方向。");
                                continue; // 重新选择
                            }
                        }
                        ProcessOption.IsFlowOnTargetBaseNormalVector = toggleIsFlowOnTargetBaseNormalVector.CurrentValue;
                        break;
                    }
                    else if (escHandler.EscapeKeyPressed)
                    {
                        RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
                        return Result.Nothing;
                    }
                    else if (res == GetResult.Cancel)
                    {
                        //默认使用 Z 轴方向
                        projectVector = Vector3d.ZAxis;
                        ProcessOption.IsFlowOnTargetBaseNormalVector = toggleIsFlowOnTargetBaseNormalVector.CurrentValue;
                        return Result.Success;
                    }
                    else
                    {
                        RhinoApp.WriteLine("请通过选项选择方向或使用两点定义。");
                    }

                }
            }

            projectVector.Unitize();

            return Result.Success;
        }


    }
}
