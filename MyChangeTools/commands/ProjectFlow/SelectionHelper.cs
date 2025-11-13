using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Geometry;
using Rhino.Input.Custom;
using System.Linq;

namespace MyChangeTools.commands.ProjectFlow
{
    public class SelectionHelper
    {
        //public static Result SelectGeometry(RhinoDoc doc, string prompt, ObjectType filter, out GeometryBase geometry)
        //{
        //    geometry = null;
        //    var rc = RhinoGet.GetOneObject(prompt, false, filter, out ObjRef objRef);
        //    if (rc != Result.Success) return rc;

        //    geometry = objRef.Geometry();
        //    if (geometry == null) return Result.Failure;

        //    doc.Objects.UnselectAll();
        //    return Result.Success;
        //}

        /// <summary>
        /// 从 Rhino 界面中选择几何对象，返回 ObjRef 数组。
        /// </summary>
        //public static Result SelectGeometries(
        //    RhinoDoc doc,
        //    string prompt,
        //    ObjectType filter,
        //    out ObjRef[] objRefs)
        //{
        //    objRefs = null;
        //    var rc = RhinoGet.GetMultipleObjects(prompt, false, filter, out objRefs);
        //    if (rc != Result.Success || objRefs == null || objRefs.Length == 0)
        //        return Result.Cancel; // 用户取消或无选择

        //    // 清除选择状态，避免影响后续命令
        //    doc.Objects.UnselectAll();
        //    // 检查是否存在无效引用
        //    objRefs = objRefs.Where(o => o != null && o.Object() != null).ToArray();
        //    if (objRefs.Length == 0)
        //        return Result.Failure;

        //    return Result.Success;
        //}
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

            go.GroupSelect=true;

            go.GeometryFilter = filter;

            go.GetMultiple(1, 0); // 1 = 最少1个对象, 0 = 无限个

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


        /// <summary>
        /// 让用户选择投影方向，可以通过两点确定，也可以选择 X/Y/Z 轴
        /// </summary>
        public static Result GetProjectVector(
    out Vector3d projectVector,
    out bool isNormalVector,
    out bool isFlowOnNormalVector,
    out bool isTransformToMeshToAppylyTransForm,
    out int controlPointMagnification
    )
        {
            isNormalVector = false;
            isFlowOnNormalVector = true;
            isTransformToMeshToAppylyTransForm = false;
            projectVector = Vector3d.Unset;
            controlPointMagnification = 4;

            var go = new GetOption();
            go.SetCommandPrompt("选择投影方向 (输入选项或通过两点定义方向, 默认 Z 轴)");

            int optX = go.AddOption("X轴");
            int optY = go.AddOption("Y轴");
            int optZ = go.AddOption("Z轴");
            int optPick = go.AddOption("两点定义");
            int optNormal = go.AddOption("最近点法向");

            var toggle0 = new OptionToggle(false, "No", "Yes");
            int opIsTransformToMeshToAppylyTransForm = go.AddOptionToggle("isTransformToMeshToAppylyTransForm", ref toggle0);

            var toggle = new OptionToggle(true, "No", "Yes");
            int optFlowOnNormal = go.AddOptionToggle("在目标曲面法向方向排列", ref toggle);

            var opIntCp = new OptionInteger(4);

            int opControlPointMagnification = go.AddOptionInteger("ControlPointMagnification", ref opIntCp);

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
                        isFlowOnNormalVector = toggle.CurrentValue;
                        RhinoApp.WriteLine($"设置在目标曲面法向方向排列:{isFlowOnNormalVector}");
                        continue;
                    }
                    else if (index == opIsTransformToMeshToAppylyTransForm)
                    {
                        isTransformToMeshToAppylyTransForm = toggle0.CurrentValue;
                        RhinoApp.WriteLine($"isTransformToMeshToAppylyTransForm:{isTransformToMeshToAppylyTransForm}");
                        continue;
                    }
                    else if (index == opControlPointMagnification)
                    {
                        controlPointMagnification = opIntCp.CurrentValue;
                        RhinoApp.WriteLine($"controlPointMagnification:{controlPointMagnification}");
                        continue;

                    }
                    else if (index == optY)
                        projectVector = Vector3d.YAxis;
                    else if (index == optZ)
                        projectVector = Vector3d.ZAxis;
                    else if (index == optNormal)
                    {
                        projectVector = Vector3d.Unset;
                        isNormalVector = true;
                        isFlowOnNormalVector = toggle.CurrentValue;
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
                    isFlowOnNormalVector = toggle.CurrentValue;
                    break;
                }
                else if (res == GetResult.Cancel)
                {
                    // 用户取消时默认使用 Z 轴方向
                    projectVector = Vector3d.ZAxis;
                    isFlowOnNormalVector = toggle.CurrentValue;
                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("请通过选项选择方向或使用两点定义。");
                }
            }

            // 归一化方向
            //if (!projectVector.IsTiny())
            projectVector.Unitize();

            return Result.Success;
        }


    }
}