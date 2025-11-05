using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace MyChangeTools.ProjectFlowEx2
{

    public class MyPointFieldMorph : SpaceMorph
    {
        private readonly Func<Point3d, (bool ok, Point3d newPt)> _processPointFunc;

        public MyPointFieldMorph(Func<Point3d, (bool ok, Point3d newPt)> processPointFunc, double tolerance)
        {
            _processPointFunc = processPointFunc;
            PreserveStructure = false; // 一般几何变形要设置 false
            Tolerance = tolerance;
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

        //private readonly Mesh[] _baseMeshs;
        //private readonly Mesh[] _targetMeshs;

        // 构建 Morph 对象
        private readonly MyPointFieldMorph _morph;

        public GeometryProcessor(RhinoDoc doc, ObjRef[] objRefs, Brep baseBrep, Brep targetBrep, Vector3d projectionDirection, SelectionOptions options)
        {
            _doc = doc;
            _objRefs = objRefs;
            _baseBrep = GeometryUtils.ToBrepSafe(baseBrep);
            _targetBrep = GeometryUtils.ToBrepSafe(targetBrep);
            _projectionDirection = projectionDirection;
            _projecVectocIsNormlvector = options.IsNormalvectorAsProjectVector;
            _isFlowOnNormalVector = options.IsFlowOnTargetBaseNormalVector;
            _controlPointMagnification = options.ControlPointMagnification;

            _ModelTolerance = _doc?.ModelAbsoluteTolerance ?? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            //_baseMeshs = Mesh.CreateFromBrep(_baseBrep, new MeshingParameters(1));
            //_targetMeshs = Mesh.CreateFromBrep(_targetBrep, new MeshingParameters(1));
            //var fastParam = MeshingParameters.FastRenderMesh;
            //_baseMeshs = Mesh.CreateFromBrep(_baseBrep, fastParam);
            //_targetMeshs = Mesh.CreateFromBrep(_targetBrep, fastParam);


            _morph = new MyPointFieldMorph(pt =>
            {
                var ok = ProcessPoint(pt, out var newPt);
                if (!ok)
                {

                    _logMessages.Enqueue("应用点变换失败,保持原点");

                }
                return (ok, newPt);
            },
            _ModelTolerance
            );

        }

        public Result Process()
        {
            bool success = false;
            var curvesToAdd = new ConcurrentBag<Curve>();
            var brepsToAdd = new ConcurrentBag<Brep>();

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
                        }
                    }
                    else if (GeometryUtils.ToBrepSafe(geom) is Brep brep)
                    {
                        if (ProcessBrep(brep, out var newBreps) == Result.Success)
                        {
                            foreach (var nb in newBreps)
                                brepsToAdd.Add(nb);
                            Interlocked.Increment(ref brepSuccess);
                        }
                        else
                        {
                            Interlocked.Increment(ref brepFail);
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
                foreach (var c in curvesToAdd)
                    _doc.Objects.AddCurve(c);

                if (!brepsToAdd.IsEmpty)
                {
                    var joinedBreps = Brep.JoinBreps(brepsToAdd.ToList(), _doc.ModelAbsoluteTolerance);
                    if (joinedBreps != null)
                    {
                        foreach (var jb in joinedBreps)
                        {
                            _doc.Objects.AddBrep(jb);
                        }
                    }
                    else
                    {
                        foreach (var b in brepsToAdd)
                        {
                            _doc.Objects.AddBrep(b);
                        }
                    }
                }
                _doc.Views.Redraw();

                while (_logMessages.TryDequeue(out string msg))
                    RhinoApp.WriteLine(msg);

                while (_logObjs.TryDequeue(out GeometryBase g))
                {
                    switch (g)
                    {
                        case Curve c: _doc.Objects.AddCurve(c); break;
                        case Brep b: _doc.Objects.AddBrep(b); break;
                        case Mesh m: _doc.Objects.AddMesh(m); break;
                        case Point p: _doc.Objects.AddPoint(p.Location); break;
                        case SubD sd: _doc.Objects.AddSubD(sd); break;
                        default:
                            RhinoApp.WriteLine($"未知几何类型: {g.ObjectType}");
                            break;
                    }
                }

                RhinoApp.WriteLine($"Curve 成功: {curveSuccess}, 失败: {curveFail}");
                RhinoApp.WriteLine($"Brep  成功: {brepSuccess}, 失败: {brepFail}");

            }));

            success = curveSuccess + brepSuccess > 0;
            return success ? Result.Success : Result.Failure;
        }


        //基函数,点变化
        private bool ProcessPoint(Point3d pt, out Point3d newPt)
        {
            newPt = Point3d.Unset;
            Point3d fromPt, toPt;

            if (_projecVectocIsNormlvector)
            {
                if (!_baseBrep.ClosestPoint(pt, out _, out _, out _, out _, double.MaxValue, out Vector3d projNormal))
                    return false;

                projNormal.Unitize();
                //fromPt = GeometryUtils.IntersectMeshAlongVector(_baseMeshs, pt, projNormal);
                //toPt = GeometryUtils.IntersectMeshAlongVector(_targetMeshs, pt, projNormal);
                fromPt = GeometryUtils.IntersectSurfaceAlongVector(_baseBrep, pt, projNormal);
                toPt = GeometryUtils.IntersectSurfaceAlongVector(_targetBrep, pt, projNormal);
            }
            else
            {
                //fromPt = GeometryUtils.IntersectMeshAlongVector(_baseMeshs, pt, _projectionDirection);
                //toPt = GeometryUtils.IntersectMeshAlongVector(_targetMeshs, pt, _projectionDirection);
                fromPt = GeometryUtils.IntersectSurfaceAlongVector(_baseBrep, pt, _projectionDirection);
                toPt = GeometryUtils.IntersectSurfaceAlongVector(_targetBrep, pt, _projectionDirection);
            }

            if (fromPt == Point3d.Unset || toPt == Point3d.Unset)
                return false;

            Point3d projectedPt = GeometryUtils.TransformPointAlongDirection(pt, fromPt, toPt, toPt - fromPt);
            if (projectedPt == Point3d.Unset)
                return false;

            if (!_targetBrep.ClosestPoint(toPt, out _, out _, out _, out _, double.MaxValue, out Vector3d targetNormal))
                return false;

            targetNormal.Unitize();

            newPt = _isFlowOnNormalVector
                ? GeometryUtils.RotatePointToVector(projectedPt, toPt, targetNormal, fromPt - toPt)
                : projectedPt;

            return newPt != Point3d.Unset;
        }

        //基于点变换变换曲线
        private Result ProcessCurve(Curve curve, out Curve newCurve)
        {
            var nc = curve as NurbsCurve ?? curve.ToNurbsCurve();

            if (_controlPointMagnification > 1)
                nc = GeometryUtils.DensifyNurbsCurve(nc, _controlPointMagnification);

            if (_morph.Morph(nc as GeometryBase))
                newCurve = nc;
            else 
                newCurve = null;
            return newCurve != null ? Result.Success : Result.Failure;
        }


        //基于点变换变换brep
        private Result ProcessBrep(Brep brep, out List<Brep> newBreps)
        {
            brep = brep.DuplicateBrep();
            newBreps = new List<Brep>();
            if (_morph.Morph(brep as GeometryBase))
                newBreps.Add(brep);
            return newBreps.Count > 0 ? Result.Success : Result.Failure;
        }

    }
}