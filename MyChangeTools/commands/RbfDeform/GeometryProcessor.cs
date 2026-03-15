using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MyChangeTools.commands.RbfDeform
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
        private readonly Deform _deform;
        private readonly List<Vector3d> _moveVectors;
        private readonly double _Tolerance;
        private readonly bool _IsCopy;
        private readonly MyPointFieldMorph _morph;
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private long _failMorphPointCount;
        private long _successMorphPointCount;


        public GeometryProcessor(
            RhinoDoc doc,
            ObjRef[] objRefs,
            Curve[] baseCurves,
            Curve[] targetCurves,
            ObjRef[] limitedObjRfs,
            List<Vector3d> MoveVectors,
            SelectionOptions selectionOptions)
        {
            _doc = doc;
            _objRefs = objRefs;
            _moveVectors = MoveVectors;
            _Tolerance = selectionOptions.Tolerance;
            _deform = new Deform(baseCurves, targetCurves, limitedObjRfs, selectionOptions);

            // 预先单位化向量（避免百万次Unitize）
            for (int i = 0; i < _moveVectors.Count; i++)
            {
                var v = _moveVectors[i];
                v.Unitize();
                _moveVectors[i] = v;
            }

            _morph = new MyPointFieldMorph(pt =>
            {
                var ok = _deform.MorphPoint(pt, out Point3d newpt);
                if (!ok) { 
                    _failMorphPointCount++;

                }
                else
                {
                    _successMorphPointCount++;
                }
                //把最终的移动限制在几个向量方向上；
                if (_moveVectors.Count > 0)
                {
                    var delta = newpt - pt;
                    var resultDelta = Vector3d.Zero;
                    foreach (var v in _moveVectors)
                    {
                        Vector3d mv = Vector3d.Multiply(delta, v) * v;
                        resultDelta += mv;
                    }
                    newpt = pt + resultDelta;
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
