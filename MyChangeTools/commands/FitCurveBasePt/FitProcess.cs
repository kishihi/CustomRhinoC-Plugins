using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyChangeTools.commands.FitCurveBasePt
{
    internal abstract class Fit
    {
        public List<Rhino.Geometry.Point3d> FitPoints { get; set; }

        public Curve Rail { get; set; }
        public string Cmd => Rail != null && Rail.IsClosed ? "_NoEcho _-Sweep1 _Style=_Freeform _Simplify=_Rebuild _RefitTolerance=0.001 _RebuildCount=5 _Closed=_Yes _ShapeBlending=_Local _Enter" : "_NoEcho _-Sweep1 _Style=_Freeform _Simplify=_Rebuild _RefitTolerance=0.001 _RebuildCount=5 _Closed=_No _ShapeBlending=_Local _Enter";
        public abstract List<Curve> GetResultFitCurve(RhinoDoc doc);

    }


    internal class FitMode1 : Fit
    {

        public FitMode1(Curve curve, List<Point3d> fitPoints)
        {
            Rail = curve;
            FitPoints = fitPoints;
        }

        public List<Line> CreateCopiedLines()
        {
            var result = new List<Line>();

            // 创建基准几何（不加进文档）
            var baseLine = new Line(-1, 0, 0, 1, 0, 0);

            foreach (var pt in FitPoints)
            {
                var line = baseLine;  // struct，复制
                line.Transform(Transform.Translation(pt - Point3d.Origin));

                result.Add(line);
            }

            return result;
        }



        public override List<Curve> GetResultFitCurve(RhinoDoc doc)
        {
            var resultCurves = new List<Curve>();
            if (FitPoints == null || FitPoints.Count == 0 || Rail == null)
                return resultCurves;

            // 1️⃣ 创建截面曲线
            var sectionLines = CreateCopiedLines();
            var sectionCurves = sectionLines.Select(l => l.ToNurbsCurve()).ToList();

            // 2️⃣ 将截面曲线加入文档（隐藏）
            var sectionIds = new List<Guid>();
            foreach (var curve in sectionCurves)
            {
                var id = doc.Objects.AddCurve(curve);
                if (id != Guid.Empty)
                    sectionIds.Add(id);
            }
            var railId = doc.Objects.AddCurve(Rail);

            doc.Views.Redraw();

            doc.Objects.UnselectAll();

            // 3️⃣ 选择轨迹和截面曲线
            doc.Objects.Select(railId);
            foreach (var id in sectionIds)
            {
                doc.Objects.Select(id);
            }

            // 4️⃣ 执行 Sweep 命令
            bool sweepOk = RhinoApp.RunScript(Cmd, true);

            if (!sweepOk)
            {
                RhinoApp.WriteLine("Sweep 命令执行失败");
                //doc.Objects.Delete(sectionIds, true);

                //doc.Objects.Delete(railId, true);
                return resultCurves;
            }

            // 5️⃣ 获取最后生成的曲面（Python rs.FirstObject()）
            var lastObj = doc.Objects.MostRecentObject();
            if (lastObj == null || !(lastObj.Geometry is Surface))
            {
                doc.Objects.Delete(sectionIds, true);
                return resultCurves;
            }

            var resultSurface = lastObj.Geometry as Surface;

            // 6️⃣ 提取等参线
            var u0v0 = resultSurface.ClosestPoint(FitPoints[0], out double u, out double v);

            var isoCurves = new List<Curve>();
            var isoU = resultSurface.IsoCurve(0, u); // U方向等参线
            var isoV = resultSurface.IsoCurve(1, v); // V方向等参线
            if (isoU != null) isoCurves.Add(isoU);
            if (isoV != null) isoCurves.Add(isoV);

            // 7️⃣ 清理截面曲线
            doc.Objects.Delete(sectionIds, true);
            doc.Objects.Delete(railId, true);

            // 8️⃣ 保留所有 FitPoints 都在曲线上的线
            foreach (var c in isoCurves)
            {
                bool allOnCurve = FitPoints.All(pt =>
                {
                    bool ok = c.ClosestPoint(pt, out double tIso);
                    return ok && pt.DistanceTo(c.PointAt(tIso)) < 0.001;
                });

                if (allOnCurve)
                    resultCurves.Add(c);
                else
                    c.Dispose(); // 不保留
            }

            // 9️⃣ 清理原曲面对象
            doc.Objects.Delete(lastObj.Id, true);

            // 10️⃣ 刷新视图
            doc.Views.Redraw();

            return resultCurves;
        }



    }

    internal class FitMode2 : Fit
    {

        private List<Curve> GetSortedSections()
        {
            var paramSectionPairs = new List<(double t, Curve section)>();


            foreach (Point3d pt in FitPoints)
            {
                if (!Rail.ClosestPoint(pt, out double t))
                {
                    RhinoApp.WriteLine("Failed to find closest point on rail for a section point.");
                    continue;
                }

                Point3d curvePt = Rail.PointAt(t);
                Line line = new Line(curvePt, pt);
                Curve section = line.ToNurbsCurve();

                paramSectionPairs.Add((t, section));
            }

            if (paramSectionPairs.Count == 0)
            {
                RhinoApp.WriteLine("No valid section lines were created.");
            }

            // =====================================================
            // 3. 按参数排序
            // =====================================================
            paramSectionPairs.Sort((a, b) => a.t.CompareTo(b.t));
            List<Curve> sortedSections = paramSectionPairs.Select(p => p.section).ToList();

            return sortedSections;
        }

        public override List<Curve> GetResultFitCurve(RhinoDoc doc)
        {
            var sortedSections = GetSortedSections();

            if (sortedSections.Count == 0)
            {
                RhinoApp.WriteLine("未能生成截面线");
                return new List<Curve>();
            }


            var sweepBreps = Brep.CreateFromSweep(
        rail: Rail,
        shapes: sortedSections,
        startPoint: Point3d.Unset,   // 不指定起点
        endPoint: Point3d.Unset,     // 不指定终点
        frameType: SweepFrame.Freeform, // _Style=_Freeform
        roadlikeNormal: Vector3d.Unset, // 因为是 Freeform，所以用法向矢量 Unset
        closed: Rail.IsClosed,
        blendType: SweepBlend.Local,   // _ShapeBlending=_Local (在 API 中对应 Loft)
        miterType: SweepMiter.Trimmed,   // 命令宏中未指定 Miter，使用默认 None
        tolerance: doc.ModelAbsoluteTolerance,     // 使用文档公差
        rebuildType: SweepRebuild.Rebuild, // _Simplify=_Rebuild    
        rebuildPointCount: 5,        // _RebuildCount=5
        refitTolerance: 0.001      // _RefitTolerance=0.001
    );

            if (sweepBreps.Length <= 0)
            {
                RhinoApp.WriteLine("未能生成曲面");
                return new List<Curve>();
            }

            List<Curve> isoCurvesToKeep = new List<Curve>();

            foreach (Brep sweepResult in sweepBreps)
            {


                if (sweepResult != null && FitPoints.Count > 0)
                {

                    var ecd = sweepResult.DuplicateEdgeCurves();

                    foreach (Curve isoCurve in ecd)
                    {
                        // 检查所有原始点是否都在这条等参线上
                        bool allOnCurve = FitPoints.All(
                            pt => isoCurve.ClosestPoint(pt, out double tIso) &&

                            pt.DistanceTo(isoCurve.PointAt(tIso)) < 0.001
                            );

                        if (allOnCurve)
                            isoCurvesToKeep.Add(isoCurve);
                    }
                }
            }
            return isoCurvesToKeep;
        }
    }


    internal class FitProcess
    {
        public Fit fit;

        public SelOption selop { get; }  // get only

        public RhinoDoc doc { get; set; }


        public FitProcess(SelOption selop, Curve BaseCurve, List<Point3d> FitPoints, RhinoDoc doc)
        {
            this.selop = selop;
            if (selop.FitMode == 2)
                fit = new FitMode2();
            else if (selop.FitMode == 1)
                fit = new FitMode1(BaseCurve, FitPoints);
            else
            {

            }
            fit.FitPoints = FitPoints;
            fit.Rail = BaseCurve;
            this.doc = doc;
        }

        public Result Process()
        {
            var resultCurves = fit.GetResultFitCurve(doc);
            if (resultCurves.Count <= 0)
            {
                return Result.Failure;
            }
            else
            {
                resultCurves.ForEach(c => doc.Objects.AddCurve(c));
                return Result.Success;
            }
        }

    }
}
