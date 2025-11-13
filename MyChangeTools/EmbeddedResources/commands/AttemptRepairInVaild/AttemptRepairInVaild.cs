//using Rhino;
//using Rhino.Commands;
//using Rhino.Geometry;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace MyChangeTools.AttemptRepairInVaild
//{
//    public class AttemptRepairInVaild : Command
//    {

//        public override string EnglishName => "AttemptRepairInVaild";

//        private List<Brep> SplitAndValidate(Brep baseBrep, List<Curve> pulledCurves, List<Point3d> mappedPts, double _ModelTolerance, double _TransFormedPointOnTransFromedFaceTolerance)
//        {
//            var newBreps = new List<Brep>();

//            var splitResult = baseBrep.Split(pulledCurves.ToArray(), _ModelTolerance);
//            if (splitResult == null || splitResult.Length == 0)
//                return newBreps;

//            foreach (var splitBrep in splitResult)
//            {
//                foreach (var face in splitBrep.Faces)
//                {
//                    var pulled = face.PullPointsToFace(mappedPts, _ModelTolerance);
//                    if (pulled == null || pulled.Length != mappedPts.Count)
//                        continue;

//                    bool ok = true;
//                    for (int i = 0; i < mappedPts.Count; i++)
//                    {
//                        if (mappedPts[i].DistanceTo(pulled[i]) > _TransFormedPointOnTransFromedFaceTolerance)
//                        {
//                            ok = false;
//                            break;
//                        }
//                    }

//                    if (ok)
//                    {
//                        var keep = face.DuplicateFace(false);
//                        if (keep != null)
//                            newBreps.Add(keep);
//                    }
//                }
//            }

//            return newBreps;
//        }

//        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//        {
//            // 1. 获取选中的 Brep
//            var go = new Rhino.Input.Custom.GetObject();
//            go.SetCommandPrompt("选择需要修复的 Brep");
//            go.GeometryFilter = Rhino.DocObjects.ObjectType.Brep;
//            go.GetMultiple(1, 0);

//            if (go.CommandResult() != Result.Success)
//                return go.CommandResult();

//            var selectedBreps = go.Objects().Select(o => o.Brep()).Where(b => b != null).ToList();
//            if (selectedBreps.Count == 0)
//            {
//                RhinoApp.WriteLine("未选择有效 Brep");
//                return Result.Failure;
//            }

//            // 2. 遍历每个 Brep
//            foreach (var brep in selectedBreps)
//            {
//                if (brep == null) continue;

//                var repairedBreps = new List<Brep>();

//                foreach (var face in brep.Faces)
//                {
//                    try
//                    {
//                        // 获取采样点和 trimmed edges
//                        var samplePts = MyChangeTools.ProjectFlowEx2.GeometryUtils.SamplePointsOnBrepFace(face, doc.ModelAbsoluteTolerance);


//                        var trimmedCurves = MyChangeTools.ProjectFlowEx2.GeometryUtils.GetFaceTrimCurves3D(face);

//                        doc.Objects.AddPoints(samplePts);

//                        trimmedCurves
//                         .Where(c => c != null)
//                         .ToList()
//                         .ForEach(c => doc.Objects.AddCurve(c));




//                        if (samplePts == null || samplePts.Count == 0)
//                        {
//                            repairedBreps.Add(Brep.CreateFromSurface(face.UnderlyingSurface()));
//                            continue;
//                        }

//                        // 构造 untrimmed surface
//                        var s = face.UnderlyingSurface();
//                        var ns = s as NurbsSurface ?? face.ToNurbsSurface();

//                        // 可选：加密控制点
//                        // ns = GeometryUtils.DensifyNurbsSurface(ns, magnification);

//                        var untrimmedBrep = Brep.CreateFromSurface(ns);
//                        if (untrimmedBrep == null)
//                            continue;

//                        //// 拉伸 / 映射 trimmed edges
//                        //var pulledCurves = MyChangeTools.ProjectFlowEx2.GeometryUtils.ExtendAndPullCurves(trimmedCurves, untrimmedBrep.Faces[0], doc.ModelAbsoluteTolerance);
//                        //if (pulledCurves.Count == 0)
//                        //{
//                        //    repairedBreps.Add(untrimmedBrep);
//                        //    continue;
//                        //}

//                        // 分割并筛选
//                        var pieces = SplitAndValidate(untrimmedBrep, trimmedCurves, samplePts, doc.ModelAbsoluteTolerance, doc.ModelAbsoluteTolerance * 2);
//                        repairedBreps.AddRange(pieces.Where(b => b != null));
//                    }
//                    catch (Exception ex)
//                    {
//                        RhinoApp.WriteLine($"修复 Face 时出错: {ex.Message}");
//                    }
//                }

//                // 3. 添加到文档
//                foreach (var r in repairedBreps)
//                {
//                    if (r != null)
//                        doc.Objects.AddBrep(r);
//                }
//            }


//            doc.Views.Redraw();
//            RhinoApp.WriteLine("修复完成");
//            return Result.Success;
//        }
//    }
//}
