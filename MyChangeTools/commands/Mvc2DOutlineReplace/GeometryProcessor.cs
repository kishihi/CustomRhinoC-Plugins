using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MyChangeTools.commands.Mvc2DOutlineReplace
{
    class MyPointFieldMorph : SpaceMorph
    {
        private readonly Func<Point3d, Point3d> _processPointFunc;

        public MyPointFieldMorph(Func<Point3d, Point3d> processPointFunc, double tolerance, bool preserveStructure, bool quickPreview)
        {
            _processPointFunc = processPointFunc;
            PreserveStructure = preserveStructure;
            Tolerance = tolerance;
            QuickPreview = quickPreview;
        }

        public override Point3d MorphPoint(Point3d point)
        {
            try
            {
                var newPt = _processPointFunc(point);
                return newPt;
            }
            catch
            {
                return Point3d.Unset; //如果变换过程中发生异常，也返回Unset
            }
        }
    }

    internal class GeometryProcessor
    {
        //private readonly double _Tolerance;
        private readonly bool _IsCopy;
        private readonly MyPointFieldMorph _morph;
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private long _failMorphPointCount;
        private long _successMorphPointCount;


        bool GetSamplePoint2dsOnTwoCurves(Curve baseCurve, Curve targetCurve, double samplePointDistance, out List<Point2d> basePts, out List<Point2d> targetPts)
        {
            basePts = new List<Point2d>();
            targetPts = new List<Point2d>();
            double baseLength = baseCurve.GetLength();
            double targetLength = targetCurve.GetLength();
            int sampleCount = (int)System.Math.Ceiling((Math.Max(baseLength, targetLength) / samplePointDistance) + 1);
            for (int i = 0; i < sampleCount; i++)
            {
                double tBase = baseCurve.Domain.ParameterAt((double)i / (sampleCount - 1));
                double tTarget = targetCurve.Domain.ParameterAt((double)i / (sampleCount - 1));
                Point3d ptBase = baseCurve.PointAt(tBase);
                Point3d ptTarget = targetCurve.PointAt(tTarget);
                basePts.Add(new Point2d(ptBase.X, ptBase.Y));
                targetPts.Add(new Point2d(ptTarget.X, ptTarget.Y));
            }

            if(basePts.Count < 2 || targetPts.Count < 2)
            {
                RhinoApp.WriteLine("采样点不足，无法进行变形。请调整采样点距离或检查曲线。");
                return false;
            }
            if (basePts.Count != targetPts.Count)
            {
                RhinoApp.WriteLine("基准曲线和目标曲线的采样点数量不匹配，无法进行变形。请检查曲线。");
                return false;
            }
            return true;
        }


        public GeometryProcessor(
            RhinoDoc doc,
            ObjRef[] objRefs,
            Curve baseCurve,
            Curve targetCurve,
            SelectionOptions selectionOptions)
        {
            _doc = doc;
            _objRefs = objRefs;
            //_Tolerance = selectionOptions.Tolerance;


            var sampleok = GetSamplePoint2dsOnTwoCurves(baseCurve, targetCurve, selectionOptions.SamplePointDistance, out List<Point2d> basePts, out List<Point2d> targetPts);

            if(!sampleok)
            {
                RhinoApp.WriteLine("采样点不足，无法进行变形。请调整采样点距离或检查曲线。");
                throw new ArgumentException("采样点不足，无法进行变形。请调整采样点距离或检查曲线。");
            }

            _morph = new MyPointFieldMorph(pt =>
            {
                var newpt = MvcCompute.DeformPoint(pt, basePts, targetPts);
                if (newpt == Point3d.Unset || newpt == null)
                {
                    _failMorphPointCount++;

                }
                else
                {
                    _successMorphPointCount++;
                }
                return newpt;
            },
            selectionOptions.Tolerance,
            selectionOptions.PreserveStructure,
            selectionOptions.QuickPreview
            );

            _IsCopy = selectionOptions.IsCopy;
        }

        public Result Process()
        {

            //计时开始
            var sw = Stopwatch.StartNew();


            List<GeometryBase> successProcessObjs = new List<GeometryBase>();
            List<ObjRef> failProcessObjRefs = new List<ObjRef>();

            object mergeLock = new object();

            int successCount = 0;
            int failCount = 0;

            Parallel.ForEach(
                _objRefs,

                // 每线程初始化
                () => new List<GeometryBase>(8),

                // 并行处理
                (objRef, loopState, localList) =>
                {
                    try
                    {
                        GeometryBase geom = objRef.Geometry();
                        if (geom == null)
                            return localList;

                        geom = geom.Duplicate();

                        if (geom.ObjectType == Rhino.DocObjects.ObjectType.Extrusion)
                        {
                            var eo = geom as Extrusion;
                            geom = eo.ToBrep() as GeometryBase;
                        }

                        //直接morph尝试变换
                        if (_morph.Morph(geom))
                        {
                            localList.Add(geom);
                            System.Threading.Interlocked.Increment(ref successCount);
                        }
                        else
                        {
                            lock (mergeLock) failProcessObjRefs.Add(objRef);
                            System.Threading.Interlocked.Increment(ref failCount);
                        }

                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine("对象处理出错: " + ex.Message);
                    }

                    return localList;
                },

                // 合并线程结果
                localList =>
                {
                    lock (mergeLock)
                        successProcessObjs.AddRange(localList);
                });

            // ================= UI线程 =================

            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                List<Guid> newIds = new List<Guid>(successProcessObjs.Count);

                foreach (GeometryBase g in successProcessObjs)
                {

                    Guid id = _doc.Objects.Add(g);

                    if (id != Guid.Empty)
                        newIds.Add(id);
                }

                // 选中新对象
                foreach (Guid id in newIds)
                    _doc.Objects.Select(id);

                // 删除旧对象
                if (!_IsCopy)
                {
                    HashSet<Guid> failSet = new HashSet<Guid>();

                    foreach (ObjRef fo in failProcessObjRefs)
                        failSet.Add(fo.ObjectId);

                    foreach (ObjRef ob in _objRefs)
                    {
                        // 失败的对象不删除，保留在场景中以供用户查看
                        if (!failSet.Contains(ob.ObjectId))
                            _doc.Objects.Delete(ob.ObjectId, true);
                    }
                }

                sw.Stop();

                _doc.Views.Redraw();

                RhinoApp.WriteLine("成功: " + successCount);
                RhinoApp.WriteLine($"失败: {failCount}, 失败变形的对象将会添加到选择集中");
                RhinoApp.WriteLine($"成功MorphPoint:{_successMorphPointCount}, 失败MorphPoint: {_failMorphPointCount}.");
                RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");

                foreach (ObjRef fo in failProcessObjRefs)
                    _doc.Objects.Select(fo.ObjectId);


            }));


            return Result.Success;
        }


    }
}
