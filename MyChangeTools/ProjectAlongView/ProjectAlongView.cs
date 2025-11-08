using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyChangeTools.ProjectAlongView
{

    internal class SelectionOptions
    {
        public bool DeleteInput { get; set; } = true;
    }

    internal class Selection
    {
        //store option
        public static SelectionOptions ProcessOption = new SelectionOptions();

        public static Result SelectGeometries(
    RhinoDoc doc,
    string prompt,
    bool enablePreSelect,
    ObjectType filter,
    out ObjRef[] objRefs)
        {
            objRefs = null;
            var go = new GetObject();

            go.SetCommandPrompt(string.IsNullOrEmpty(prompt) ? "选择几何体" : prompt);

            go.EnablePreSelect(enablePreSelect, true);

            go.GroupSelect = true;

            go.GeometryFilter = filter;
            //go.EnableClearObjectsOnEntry(false);

            OptionToggle toggleDeleteInput = new OptionToggle(ProcessOption.DeleteInput, "No", "Yes");
            int opIndexDeleteInput = go.AddOptionToggle("删除输入", ref toggleDeleteInput);

            var picked = new Dictionary<Guid, ObjRef>();
            using (var escHandler = new MyChangeTools.Mylib.CommandHandler.EscapeKeyEventHandler("（按 ESC 取消）"))
            {
                while (true)
                {
                    var res = go.GetMultiple(1, 0);
                    if (escHandler.EscapeKeyPressed)
                    {
                        RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
                        return Result.Cancel;
                    }
                    if (res == Rhino.Input.GetResult.Object)
                    {
                        for (int i = 0; i < go.ObjectCount; i++)
                        {
                            var objRef = go.Object(i);
                            var id = objRef.ObjectId;

                            if (!picked.ContainsKey(id))   // 去重
                                picked[id] = objRef;

                        }
                    }
                    else if (res == Rhino.Input.GetResult.Option)
                    {
                        var chosen = go.Option();
                        int opindex = chosen.Index;
                        string name = chosen.EnglishName;
                        //chosen.
                        if (opindex == opIndexDeleteInput)
                        {
                            ProcessOption.DeleteInput = toggleDeleteInput.CurrentValue;
                            RhinoApp.WriteLine($"{name} : {ProcessOption.DeleteInput}");

                        }
                        continue;
                    }
                    break;
                    
                    
                }
            }


            objRefs = new List<ObjRef>(picked.Values).ToArray();

            RhinoApp.WriteLine($"用户选取了{objRefs.Length}个对象");


            return Result.Success;
        }
    }


    public class ProjectAlongView : Command
    {
        public override string EnglishName => "ProjectAlongViewZ";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1. 选择多条曲线
            var rc = Selection.SelectGeometries(doc, "选择多条曲线", true, ObjectType.Curve, out ObjRef[] curveobjrefs);
            if (rc != Result.Success) return rc;

            List<Curve> curves = curveobjrefs.Select(c => c.Curve()).Where(c => c != null).ToList();

            if (curves.Count == 0)
            {
                RhinoApp.WriteLine("No valid curves selected.");
                return Result.Failure;
            }

            // 2. 选择多个 Brep
            rc = Selection.SelectGeometries(doc, "选择目标曲面", false, ObjectType.Brep, out ObjRef[] brepobjrefs);
            if (rc != Result.Success) return rc; ;
            List<Brep> breps = brepobjrefs.Select(c => c.Brep()).Where(c => c != null).ToList();

            if (breps.Count == 0)
            {
                RhinoApp.WriteLine("No valid Breps selected.");
                return Result.Failure;
            }

            // 3. 获取当前视口的投影方向
            var vp = doc.Views.ActiveView?.ActiveViewport;
            if (vp == null)
            {
                RhinoApp.WriteLine("No active view.");
                return Result.Failure;
            }

            Vector3d projDir = vp.CameraDirection;
            projDir.Unitize();
            projDir = -projDir; // 从屏幕 → 模型

            double tol = doc.ModelAbsoluteTolerance;

            // 4. 使用集合投影重载
            int[] curveIndices;
            int[] brepIndices;

            var results = Curve.ProjectToBrep(
                curves,
                breps,
                projDir,
                tol,
                out curveIndices,
                out brepIndices
            );

            // 5. 处理结果
            if (results == null || results.Length == 0)
            {
                RhinoApp.WriteLine("Projection failed: no projected curves generated.");
                return Result.Failure;
            }

            //List<Guid> resutId;

            for (int i = 0; i < results.Length; i++)
            {
                Curve projected = results[i];
                int sourceCurveIndex = curveIndices[i];
                int hitBrepIndex = brepIndices[i];

                // 添加到文档
                var id = doc.Objects.AddCurve(projected);
                //resutId.Add(id);
                doc.Objects.Select(id);

                // 输出映射关系
                RhinoApp.WriteLine($"Projected curve {i}: from curve[{sourceCurveIndex}] onto brep[{hitBrepIndex}]");
            }

            //doc.Views.Redraw();
            RhinoApp.WriteLine($"Projection completed: {results.Length} curve(s) created.");
            if (Selection.ProcessOption.DeleteInput)
            {
                foreach (var o in curveobjrefs)
                    doc.Objects.Delete(o, true);
            }
            doc.Views.Redraw();
            return Result.Success;
        }
    }
}