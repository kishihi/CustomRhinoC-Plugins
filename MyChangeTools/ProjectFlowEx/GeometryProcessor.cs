using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyChangeTools.ProjectFlowEx
{
    public class GeometryProcessor
    {
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private readonly Brep _baseBrep;
        private readonly Brep _targetBrep;
        private readonly Vector3d _projectionDirection;
        private readonly bool _projecVectocIsNormlvector;
        private readonly bool _isFlowOnNormalVector;
        private readonly bool _isTransformToMeshToAppylyTransForm;
        private readonly int _controlPointMagnification;

        //
        private readonly Mesh[] _baseMeshs;
        private readonly Mesh[] _targetMeshs;

        public GeometryProcessor(RhinoDoc doc, ObjRef[] objRefs, Brep baseBrep, Brep targetBrep, Vector3d projectionDirection, SelectionOptions options)
        {
            _doc = doc;
            _objRefs = objRefs;
            _baseBrep = GeometryUtils.ToBrepSafe(baseBrep);
            _targetBrep = GeometryUtils.ToBrepSafe(targetBrep);
            _projectionDirection = projectionDirection;
            _projecVectocIsNormlvector = options.IsNormalvectorAsProjectVector;
            _isFlowOnNormalVector = options.IsFlowOnTargetBaseNormalVector;
            _isTransformToMeshToAppylyTransForm = options.IsTransformToMeshToAppylyTransForm;
            _controlPointMagnification = options.ControlPointMagnification;

            _baseMeshs = Mesh.CreateFromBrep(_baseBrep, new MeshingParameters(0.5));
            _targetMeshs = Mesh.CreateFromBrep(_targetBrep, new MeshingParameters(0.5));

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

                if (geom is Curve curve)
                {
                    if (ProcessCurve(curve, out var newCurve) == Result.Success)
                    {
                        curvesToAdd.Add(newCurve);
                        Interlocked.Increment(ref curveSuccess);
                        success = true;
                    }
                    else
                    {
                        Interlocked.Increment(ref curveFail);
                    }
                }
                else if (GeometryUtils.ToBrepSafe(geom) is Brep brep)
                {
                    bool brepProcessed = false;

                    if (_isTransformToMeshToAppylyTransForm)
                    {
                        if (ProcessBrepAsMesh(brep, out var meshBreps) == Result.Success)
                        {
                            foreach (var mb in meshBreps)
                                brepsToAdd.Add(mb);
                            brepProcessed = true;
                        }
                    }
                    else
                    {
                        if (ProcessBrep(brep, out var newBreps) == Result.Success)
                        {
                            foreach (var nb in newBreps)
                                brepsToAdd.Add(nb);
                            brepProcessed = true;
                        }
                    }

                    if (brepProcessed)
                    {
                        Interlocked.Increment(ref brepSuccess);
                        success = true;
                    }
                    else
                    {
                        Interlocked.Increment(ref brepFail);
                    }
                }
                else
                {
                    RhinoApp.WriteLine($"不支持的几何类型: {geom.ObjectType}");
                }
            });

            // 添加到文档
            foreach (var c in curvesToAdd)
                _doc.Objects.AddCurve(c);

            if (!brepsToAdd.IsEmpty)
            {
                var brepList = brepsToAdd.ToList();
                var joinedBreps = Brep.JoinBreps(brepList, _doc.ModelAbsoluteTolerance);
                if (joinedBreps != null)
                {
                    foreach (var jb in joinedBreps)
                        _doc.Objects.AddBrep(jb);
                }
                else
                {
                    foreach (var b in brepList)
                        _doc.Objects.AddBrep(b);
                }
            }

            _doc.Views.Redraw();

            // 打印处理统计
            RhinoApp.WriteLine($"Curve 成功: {curveSuccess}, 失败: {curveFail}");
            RhinoApp.WriteLine($"Brep  成功: {brepSuccess}, 失败: {brepFail}");

            return success ? Result.Success : Result.Failure;
        }


        private Result ProcessCurve(Curve curve, out Curve newCurve)
        {
            newCurve = curve; // 直接修改原曲线
            if (!(curve is NurbsCurve nc))
                nc = curve.ToNurbsCurve();

            if (_controlPointMagnification > 1)
                nc = GeometryUtils.DensifyNurbsCurve(nc, _controlPointMagnification);

            Parallel.For(0, nc.Points.Count, i =>
            {
                var cp = nc.Points[i];
                if (ProcessPoint(cp.Location, out Point3d newPt))
                    nc.Points.SetPoint(i, newPt);
            });

            if (nc != null)
            {
                newCurve = nc;
                return Result.Success;
            }
            else
            {
                newCurve = null;
                return Result.Failure;
            }
        }

        private Result ProcessBrep(Brep brep, out List<Brep> newBreps)
        {
            brep = brep.DuplicateBrep();
            newBreps = new List<Brep>();
            var facesOut = new ConcurrentBag<Brep>(); // 线程安全集合

            Parallel.ForEach(brep.Faces.Cast<BrepFace>(), face =>
            {

                Surface s = face.UnderlyingSurface();
                NurbsSurface ns = s as NurbsSurface ?? face.ToNurbsSurface();
                if (_controlPointMagnification > 1)
                    ns = GeometryUtils.DensifyNurbsSurface(ns, _controlPointMagnification);

                int uCount = ns.Points.CountU;
                int vCount = ns.Points.CountV;

                for (int u = 0; u < uCount; u++)
                {
                    for (int v = 0; v < vCount; v++)
                    {
                        var cp = ns.Points.GetControlPoint(u, v);
                        if (ProcessPoint(cp.Location, out Point3d newPt))
                            ns.Points.SetPoint(u, v, newPt);
                    }
                }

                // 修改完成后生成 Brep
                Brep newBrep = Brep.CreateFromSurface(ns);
                if (newBrep != null)
                    facesOut.Add(newBrep); // 并行安全
            });

            newBreps = facesOut.ToList(); // lambda 外统一赋值
            return newBreps.Count > 0 ? Result.Success : Result.Failure;
        }


        private Result ProcessBrepAsMesh(Brep brep, out List<Brep> newBreps)
        {
            newBreps = new List<Brep>();
            if (brep == null || !brep.IsValid)
            {
                return Result.Failure;
            }

            //MeshingParameters mp = new MeshingParameters(1);
            Mesh[] meshes = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);

            if (meshes == null || meshes.Length == 0)
            {
                return Result.Failure;
            }

            var brepsBag = new ConcurrentBag<Brep>();

            Parallel.ForEach(meshes, mesh =>
            {
                Parallel.For(0, mesh.Vertices.Count, i =>
                {
                    Point3d pt = mesh.Vertices[i];
                    if (!ProcessPoint(pt, out Point3d newPt))
                        newPt = pt;
                    mesh.Vertices.SetVertex(i, newPt);
                });

                mesh.Normals.ComputeNormals();
                mesh.Compact();

                Brep nurbsBrep = GeometryUtils.ToBrepSafe(mesh);
                if (nurbsBrep != null)
                    brepsBag.Add(nurbsBrep);
                else
                {
                    ;
                }
            });

            newBreps.AddRange(brepsBag);
            return newBreps.Count > 0 ? Result.Success : Result.Failure;
        }


        private bool ProcessPoint(Point3d pt, out Point3d newPt)
        {
            newPt = Point3d.Unset;
            Point3d toPt;//投影方向与基础曲面的交点
            Point3d fromPt;//与目标曲面的交点
            //如果用户选择的投影方向是法向
            //需要计算每个点在基础曲面上的最近点的法向量方向作为投影方向
            if (_projecVectocIsNormlvector)
            {
                if (!_baseBrep.ClosestPoint(pt, out _, out _, out _, out _, double.MaxValue, out Vector3d projectionDirectionNomal))
                {
                    return false;
                }
                ;
                projectionDirectionNomal.Unitize();
                fromPt = GeometryUtils.IntersectMeshAlongVector(_baseMeshs, pt, projectionDirectionNomal);
                toPt = GeometryUtils.IntersectMeshAlongVector(_targetMeshs, pt, projectionDirectionNomal);
            }
            else
            {
                fromPt = GeometryUtils.IntersectMeshAlongVector(_baseMeshs, pt, _projectionDirection);
                toPt = GeometryUtils.IntersectMeshAlongVector(_targetMeshs, pt, _projectionDirection);
            }

            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(fromPt));
            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(toPt));

            if (fromPt == Point3d.Unset)
            {
                return false;
            }

            if (toPt == Point3d.Unset)
            {
                return false;
            }

            //投影变换
            Point3d projectedPt = GeometryUtils.TransformPointAlongDirection(pt, fromPt, toPt, toPt - fromPt);
            if (projectedPt == Point3d.Unset)
            {
                return false;
            }

            //
            if (!_targetBrep.ClosestPoint(toPt, out _, out _, out _, out _, double.MaxValue, out Vector3d targetNormal))
            {
                return false;
            }

            targetNormal.Unitize();

            //旋转变换
            if (_isFlowOnNormalVector)
            {
                newPt = GeometryUtils.RotatePointToVector(projectedPt, toPt, targetNormal, fromPt - toPt);
                if (newPt == Point3d.Unset)
                {
                    return false;
                }
            }
            else
            {
                newPt = projectedPt;
            }
            return newPt != Point3d.Unset;
        }
    }
}
