using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyChangeTools.commands.ProjectFlowEx2
{

    public class MyPointFieldMorph : SpaceMorph
    {
        private readonly Func<Point3d, (bool ok, Point3d newPt)> _processPointFunc;

        public MyPointFieldMorph(Func<Point3d, (bool ok, Point3d newPt)> processPointFunc, double tolerance, bool preserveStructure, bool quickPreview)
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
                var (ok, newPt) = _processPointFunc(point);
                return ok ? newPt : Point3d.Unset; //如果变换失败，返回Unset
            }
            catch
            {
                return Point3d.Unset; //如果变换过程中发生异常，也返回Unset
            }
        }
    }

    public class GeometryProcessor
    {
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private readonly Brep _baseBrep;
        private readonly Brep _targetBrep;
        private readonly Vector3d _projectionDirection;
        private readonly bool _projecVectocIsNormlvector;
        private readonly bool _isFlowOnNormalVector;
        private readonly int _controlPointMagnification;
        private readonly double _ModelTolerance;
        private readonly ConcurrentQueue<string> _logMessages = new ConcurrentQueue<string>(); // 全局日志队列
        private readonly ConcurrentQueue<GeometryBase> _logObjs = new ConcurrentQueue<GeometryBase>(); // 全局临时对象队列

        // 构建 Morph 对象
        private readonly MyPointFieldMorph _morph;
        private readonly bool _IsProcessBrepTogeTher;

        private readonly ConcurrentDictionary<Point3d, Point3d> _ptMapping =
    new ConcurrentDictionary<Point3d, Point3d>();


        private readonly bool _IsCopy;

        private readonly bool __ShowLogObj;

        private int _failTranPointCount = 0;

        private delegate Result ProcessBrepHandler(Brep brep, out List<Brep> newBreps);


        // 尝试从缓存获取变换结果，如果没有则计算并缓存结果
        private (bool ok, Point3d newPt) TryMapOrProcess(Point3d pt)
        {
            if (_ptMapping.TryGetValue(pt, out var cached))
                return (true, cached);
            if (ProcessPoint(pt, out var newPt))
            {
                if (_ptMapping.TryAdd(pt, newPt))
                    return (true, newPt);
            } 
            Interlocked.Increment(ref _failTranPointCount);
            _logObjs.Enqueue(new Point(pt));
            return (false, Point3d.Unset);
        }


        public GeometryProcessor(RhinoDoc doc, ObjRef[] objRefs, Brep baseBrep, Brep targetBrep, Vector3d projectionDirection, SelectionOptions options)
        {
            _doc = doc;
            _objRefs = objRefs;
            _baseBrep = Mylib.GeometryUtils.ToBrepSafe(baseBrep);
            _targetBrep = Mylib.GeometryUtils.ToBrepSafe(targetBrep);
            _projectionDirection = projectionDirection;
            _projecVectocIsNormlvector = options.IsNormalvectorAsProjectVector;
            _isFlowOnNormalVector = options.IsFlowOnTargetBaseNormalVector;
            _controlPointMagnification = options.ControlPointMagnification;

            _ModelTolerance = _doc?.ModelAbsoluteTolerance ?? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            //是否展示LogObj
            __ShowLogObj = options.ShowLogObj;

            _morph = new MyPointFieldMorph(pt =>
            {
                return TryMapOrProcess(pt);
            },
            _ModelTolerance,
            options.PreserveStructure,
            options.QuickPreview
            );
            _IsProcessBrepTogeTher = options.IsProcessBrepTogeTher;

            _IsCopy = options.IsCopy;


        }
        public Result Process()
        {
            var sw = Stopwatch.StartNew();

            ProcessBrepHandler processBrep;
            if (_IsProcessBrepTogeTher)
                processBrep = ProcessBrepTogether;
            else
                processBrep = ProcessBrepSplit;

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

                        Curve curve = geom as Curve;
                        if (curve != null)
                        {
                            Curve newCurve;
                            if (ProcessCurve(curve, out newCurve) == Result.Success)
                            {
                                localList.Add(newCurve);
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                lock (mergeLock) failProcessObjRefs.Add(objRef);
                                Interlocked.Increment(ref failCount);
                            }

                            return localList;
                        }

                        Brep brep = geom as Brep;
                        if (brep != null)
                        {
                            List<Brep> newBreps;
                            if (processBrep(brep, out newBreps) == Result.Success)
                            {
                                if (newBreps != null)
                                    localList.AddRange(newBreps);

                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                lock (mergeLock) failProcessObjRefs.Add(objRef);
                                Interlocked.Increment(ref failCount);
                            }

                            return localList;
                        }

                        // 其他类型直接morph尝试变换
                        //subd ,mesh等其他类型的对象也可以尝试变换，如果变换失败则记录日志并保留对象
                        if (_morph.Morph(geom))
                        {
                            localList.Add(geom);
                            Interlocked.Increment(ref successCount);
                        }
                        else
                        {
                            lock (mergeLock) failProcessObjRefs.Add(objRef);
                            Interlocked.Increment(ref failCount);
                        }

                    }
                    catch (Exception ex)
                    {
                        _logMessages.Enqueue("对象处理出错: " + ex.Message);
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
                List<Brep> breps = new List<Brep>();

                foreach (GeometryBase g in successProcessObjs)
                {
                    Brep b = g as Brep;

                    if (b != null)
                    {
                        breps.Add(b);
                        continue;
                    }
                    // 其他类型直接添加到文档

                    Guid id = _doc.Objects.Add(g);

                    if (id != Guid.Empty)
                        newIds.Add(id);
                }

                // Brep join
                if (breps.Count > 0)
                {
                    Brep[] joined = null;

                    if (breps.Count > 1)
                        joined = Brep.JoinBreps(breps, _doc.ModelAbsoluteTolerance);

                    IEnumerable<Brep> finalBreps;

                    if (joined != null)
                        finalBreps = joined;
                    else
                        finalBreps = breps;

                    foreach (Brep b in finalBreps)
                    {
                        Guid id = _doc.Objects.AddBrep(b);

                        if (id != Guid.Empty)
                            newIds.Add(id);
                    }
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

                // log

                while (_logMessages.TryDequeue(out string msg))
                    RhinoApp.WriteLine(msg);

                // if showlogobj
                if (__ShowLogObj)
                {
                    RhinoApp.WriteLine("LogObjCount: " + _logObjs.Count);
                    while (_logObjs.TryDequeue(out GeometryBase obj)){
                        Guid id = _doc.Objects.Add(obj);
                    }
                }


                _doc.Views.Redraw();

                RhinoApp.WriteLine("成功: " + successCount);
                RhinoApp.WriteLine("失败: " + failCount);
                RhinoApp.WriteLine("ControlPoint Failed " + _failTranPointCount);

                sw.Stop();
                RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");

                foreach (ObjRef fo in failProcessObjRefs)
                    _doc.Objects.Select(fo.ObjectId);
            }));

            return Result.Success;
        }

        // 基函数: 点变化
        private bool ProcessPoint(Point3d pt, out Point3d newPt)
        {
            newPt = Point3d.Unset;

            // 1️⃣ 求投影方向
            Vector3d projDir;
            if (_projecVectocIsNormlvector && !_projectionDirection.IsValid)
            {
                if (!_baseBrep.ClosestPoint(pt, out _, out _, out _, out _, double.MaxValue, out projDir))
                    return false;
            }
            else
            {
                projDir = _projectionDirection;
            }

            // 2️⃣ 计算两Brep的交点
            var fromPt = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_baseBrep, pt, projDir);
            var toPt = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_targetBrep, pt, projDir);

            if (fromPt == Point3d.Unset || toPt == Point3d.Unset)
                return false;

            // 3️⃣ 获取目标面法向
            if (!_targetBrep.ClosestPoint(toPt, out _, out _, out _, out _, _ModelTolerance * 10, out Vector3d targetNormal))
                return false;

            // 4️⃣ 计算最终方向
            Vector3d flowDir;
            Vector3d ptLocationOnBase = pt - fromPt; //判断初始点在基础曲面的上方还是下方
            if (ptLocationOnBase.Length > 0.001) //长度大于容差
            {
                bool isPositive = targetNormal * ptLocationOnBase >= 0;
                if (_isFlowOnNormalVector)
                {
                    flowDir = isPositive ? targetNormal : -targetNormal;

                }
                else
                {
                    Vector3d MoveVector = fromPt - toPt;
                    flowDir = isPositive ? MoveVector : -MoveVector;
                }


                // 5️⃣ 执行变换
                newPt = Mylib.GeometryUtils.MovePointAlongVector(toPt, flowDir, pt.DistanceTo(fromPt));
            }
            else
            {
                newPt = toPt;
            }
            return newPt != Point3d.Unset;
        }


        //基于点变换变换曲线
        private Result ProcessCurve(Curve curve, out Curve newCurve)
        {
            var curvedup = curve.DuplicateCurve();
            var nc = curvedup as NurbsCurve ?? curvedup.ToNurbsCurve();

            if (_controlPointMagnification > 1)
                nc = Mylib.GeometryUtils.DensifyNurbsCurve(nc, _controlPointMagnification);

            if (_morph.Morph(nc as GeometryBase))
                newCurve = nc;
            else
                newCurve = null;
            return newCurve != null ? Result.Success : Result.Failure;
        }

        private Result ProcessBrepTogether(Brep brep, out List<Brep> newBreps)
        {
            newBreps = new List<Brep>();
            if (brep == null || !brep.IsValid)
                return Result.Failure;
            brep.Standardize();
            brep.Compact();
            if (!_morph.Morph(brep))
                return Result.Failure;
            if (brep.IsValid)
            {
                newBreps.Add(brep);
                return Result.Success;
            }
            return Result.Failure;

        }

        private Result ProcessBrepSplit(Brep brep, out List<Brep> newBreps)
        {
            newBreps = new List<Brep>();
            if (brep == null || !brep.IsValid)
                return Result.Failure;
            var results = new ConcurrentBag<Brep>();
            Parallel.For(0, brep.Faces.Count, i =>
            {
                try
                {
                    Brep singleBrep = brep.Faces[i].DuplicateFace(false);
                    singleBrep.Standardize();
                    singleBrep.Compact();
                    if (singleBrep != null && singleBrep.IsValid)
                    {
                        if (_morph.Morph(singleBrep))
                            if (singleBrep.IsValid)
                                results.Add(singleBrep);
                            else
                            {
                                _logMessages.Enqueue($"Brep的单面无效");
                                if (__ShowLogObj)
                                {
                                    _logObjs.Enqueue(brep.Faces[i].DuplicateFace(false));
                                }
                            }
                    }
                }
                catch (Exception ex)
                {
                    _logMessages.Enqueue($"[线程异常] 面索引 {i}: {ex.Message}");
                }
            });
            newBreps = results.ToList();
            return newBreps.Count >0 ? Result.Success : Result.Failure;
        }
    }
}