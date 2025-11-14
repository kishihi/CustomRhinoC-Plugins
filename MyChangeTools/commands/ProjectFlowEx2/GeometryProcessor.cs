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
                return ok ? newPt : point;
            }
            catch
            {
                return point;
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
        private readonly ConcurrentQueue<string> _logMessages = new ConcurrentQueue<string>(); // ✅ 全局日志队列
        private readonly ConcurrentQueue<GeometryBase> _logObjs = new ConcurrentQueue<GeometryBase>(); // ✅ 全局临时对象队列

        // 构建 Morph 对象
        private readonly MyPointFieldMorph _morph;
        private readonly bool _PreserveStructure;
        private readonly bool _IsProcessBrepTogeTher;

        private readonly bool _IsCopy;

        private readonly bool __ShowLogObj;

        private int _failTranPointCount = 0;

        private delegate Result ProcessBrepHandler(Brep brep, out List<Brep> newBreps);

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
                var ok = ProcessPoint(pt, out var newPt);
                if (!ok)
                {
                    _failTranPointCount++;
                    _logObjs.Enqueue(new Point(pt));

                }
                return (ok, newPt);
            },
            _ModelTolerance,
            options.PreserveStructure,
            options.QuickPreview
            );
            _PreserveStructure = options.PreserveStructure;
            _IsProcessBrepTogeTher = options.IsProcessBrepTogeTher;

            _IsCopy = options.IsCopy;


        }

        public Result Process()
        {
            var sw = Stopwatch.StartNew();
            bool success = false;
            var curvesToAdd = new ConcurrentBag<Curve>();
            var brepsToAdd = new ConcurrentBag<Brep>();

            var failObj = new ConcurrentBag<ObjRef>();

            // 记录每个对象对应的群组信息
            var objGroupMap = new Dictionary<Guid, int[]>();
            foreach (var objRef in _objRefs)
            {
                var rhObj = objRef.Object();
                if (rhObj != null)
                {
                    var groups = rhObj.Attributes.GetGroupList();
                    if (groups != null && groups.Length > 0)
                        objGroupMap[objRef.ObjectId] = groups;
                }
            }


            int curveSuccess = 0, curveFail = 0;
            int brepSuccess = 0, brepFail = 0;

            Parallel.ForEach(_objRefs, objRef =>
            {
                var geom = objRef.Geometry();
                if (geom == null) return;

                try
                {
                    if (geom is Curve curve)
                    {
                        if (ProcessCurve(curve, out var newCurve) == Result.Success)
                        {
                            curvesToAdd.Add(newCurve);
                            Interlocked.Increment(ref curveSuccess);
                        }
                        else
                        {
                            Interlocked.Increment(ref curveFail);
                            failObj.Add(objRef);
                        }
                    }
                    else if (Mylib.GeometryUtils.ToBrepSafe(geom) is Brep brep)
                    {
                        ProcessBrepHandler ProcessBrep;
                        if (_IsProcessBrepTogeTher)
                            ProcessBrep = ProcessBrepTogether; // 方法组 -> 委托转换
                        else
                            ProcessBrep = ProcessBrepSplit;
                        if (ProcessBrep(brep, out var newBreps) == Result.Success)
                        {
                            foreach (var nb in newBreps)
                                brepsToAdd.Add(nb);
                            Interlocked.Increment(ref brepSuccess);
                        }
                        else
                        {
                            Interlocked.Increment(ref brepFail);
                            failObj.Add(objRef);
                        }
                    }
                    else
                    {
                        _logMessages.Enqueue($"不支持的几何类型: {geom.ObjectType}");
                    }
                }
                catch (Exception ex)
                {
                    _logMessages.Enqueue($"对象处理出错: {ex.Message}");
                }
            });

            // --- 主线程安全区 ---
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                List<Guid> newids = new List<Guid>();

                foreach (var c in curvesToAdd)
                {
                    var id = _doc.Objects.AddCurve(c);
                    newids.Add(id);
                }


                if (!brepsToAdd.IsEmpty)
                {
                    var joinedBreps = Brep.JoinBreps(brepsToAdd.ToList(), _doc.ModelAbsoluteTolerance);
                    if (joinedBreps != null)
                    {
                        foreach (var jb in joinedBreps)
                        {
                            var id = _doc.Objects.AddBrep(jb);
                            newids.Add(id);
                        }
                    }
                    else
                    {
                        foreach (var b in brepsToAdd)
                        {
                            var id = _doc.Objects.AddBrep(b);
                            newids.Add(id);
                        }
                    }
                }


                newids.ForEach(id => _doc.Objects.Select(id));



                _doc.Views.Redraw();

                while (_logMessages.TryDequeue(out string msg))
                    RhinoApp.WriteLine(msg);

                while (_logObjs.TryDequeue(out GeometryBase g))
                {

                    if (__ShowLogObj)
                    {
                        var addMap = new Dictionary<Type, Func<object, Guid>>
                            {
                                { typeof(Curve), o => _doc.Objects.AddCurve((Curve)o) },
                                { typeof(Brep),  o => _doc.Objects.AddBrep((Brep)o) },
                                { typeof(Mesh),  o => _doc.Objects.AddMesh((Mesh)o) },
                                { typeof(Point), o => _doc.Objects.AddPoint(((Point)o).Location) },
                                { typeof(SubD),  o => _doc.Objects.AddSubD((SubD)o) }
                            };

                        Guid id = addMap.TryGetValue(g.GetType(), out var addFunc)
                            ? addFunc(g)
                            : Guid.Empty;
                        if (id == Guid.Empty)
                            RhinoApp.WriteLine($"未知几何类型: {g.ObjectType}");
                        else
                            _doc.Objects.Select(id);
                    }
                }

                if (!_IsCopy)
                {
                    //_doc.Objects.Delete()
                    foreach (ObjRef obf in _objRefs)
                    {
                        _doc.Objects.Delete(obf, true);
                    }
                }

                RhinoApp.WriteLine($"Curve 成功: {curveSuccess}, 失败: {curveFail}");
                RhinoApp.WriteLine($"Brep  成功: {brepSuccess}, 失败: {brepFail}");
                RhinoApp.WriteLine($"ControlPoint Failed {_failTranPointCount}");
                sw.Stop();
                RhinoApp.WriteLine($"执行时间: {sw.ElapsedMilliseconds} ms");
                foreach (var fo in failObj)
                {
                    _doc.Objects.Select(fo.ObjectId);
                }

            }));

            success = curveSuccess + brepSuccess > 0;
            //sw.Stop();

            return success ? Result.Success : Result.Failure;
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
            if (ptLocationOnBase.Length > _ModelTolerance) //长度大于容差
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
            curve = curve.DuplicateCurve();
            var nc = curve as NurbsCurve ?? curve.ToNurbsCurve();

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
            brep = brep.DuplicateBrep();
            newBreps = new List<Brep>();
            if (_morph.Morph(brep as GeometryBase))
                newBreps.Add(brep);
            return newBreps.Count > 0 ? Result.Success : Result.Failure;
        }


        private Result ProcessBrepSplit(Brep brep, out List<Brep> newBreps)
        {
            newBreps = new List<Brep>();
            if (brep == null || !brep.IsValid)
                return Result.Failure;
            brep = brep.DuplicateBrep();
            var results = new ConcurrentBag<Brep>();
            Parallel.ForEach(brep.Faces, face =>
            {
                try
                {
                    // 每个面单独作为小brep
                    Brep singleBrep = face.DuplicateFace(false);
                    if (singleBrep != null && singleBrep.IsValid)
                    {
                        if (_morph.Morph(singleBrep as GeometryBase))
                            results.Add(singleBrep);
                    }
                }
                catch (Exception ex)
                {
                    _logMessages.Enqueue($"[线程异常] 面索引 {face.FaceIndex}: {ex.Message}");
                }
            });
            newBreps = results.ToList();
            return newBreps.Count > 0 ? Result.Success : Result.Failure;
        }
    }
}