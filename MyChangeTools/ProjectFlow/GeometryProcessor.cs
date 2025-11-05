//using Rhino;
//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using Rhino.Input;
//using System;
//using System.Collections.Generic;

//namespace MyChangeTools.ProjectFlow
//{
//    public class GeometryProcessor
//    {
//        private readonly RhinoDoc _doc;
//        private readonly ObjRef[] _objRefs;
//        private readonly Brep _baseBrep;
//        private readonly Brep _targetBrep;
//        private readonly Vector3d _projectionDirection;
//        private readonly bool _projecVectocIsNormlvector;
//        private readonly bool _isFlowOnNormalVector;
//        private readonly bool _isTransformToMeshToAppylyTransForm;
//        private readonly int _controlPointMagnification = 4;

//        public GeometryProcessor(RhinoDoc doc, ObjRef[] objRefs, Brep baseBrep, Brep targetBrep, Vector3d projectionDirection, bool isNormlvector, bool isFlowOnNormalVector, bool isTransformToMeshToAppylyTransForm, int controlPointMagnification)
//        {
//            _doc = doc;
//            _objRefs = objRefs;
//            _baseBrep = GeometryUtils.ToBrepSafe(baseBrep);
//            _targetBrep = GeometryUtils.ToBrepSafe(targetBrep);
//            _projectionDirection = projectionDirection;
//            _projecVectocIsNormlvector = isNormlvector;
//            _isFlowOnNormalVector = isFlowOnNormalVector;
//            _isTransformToMeshToAppylyTransForm = isTransformToMeshToAppylyTransForm;
//            _controlPointMagnification = controlPointMagnification;
//        }

//        public Result Process()
//        {
//            bool success = false;

//            foreach (var objRef in _objRefs)
//            {
//                var geom = objRef.Geometry();
//                //objRef.Brep()
//                if (geom == null) continue;

//                if (geom is Curve curve)
//                {
//                    if (ProcessCurve(curve) == Result.Success)
//                        success = true;
//                }
//                else if (GeometryUtils.ToBrepSafe(geom) is Brep brep)
//                {
//                    Result r = _isTransformToMeshToAppylyTransForm ? ProcessBrepAsMesh(brep) : ProcessBrepAsUntrimmedSrf(brep);

//                    if (r == Result.Success)
//                        success = true;
//                }
//                else
//                {
//                    RhinoApp.WriteLine($"不支持的处理类型:{geom.ObjectType}");
//                }
//            }

//            return success ? Result.Success : Result.Failure;
//        }

//        //new MorphControl()

//        private Result ProcessCurve(Curve curve)
//        {
//            NurbsCurve nc = curve.ToNurbsCurve();
//            nc = GeometryUtils.DensifyNurbsCurve(nc, _controlPointMagnification);
//            for (int i = 0; i < nc.Points.Count; i++)
//            {
//                Point3d pt = nc.Points[i].Location;
//                if (!ProcessPoint(pt, out Point3d newPt, i.ToString()))
//                {
//                    RhinoApp.WriteLine($"Point {i}: 处理失败");
//                    continue;
//                }
//                nc.Points.SetPoint(i, newPt);
//            }
//            _doc.Objects.AddCurve(nc);
//            return Result.Success;
//        }

//        private Result ProcessBrepAsMesh(Brep brep)
//        {
//            if (brep == null || !brep.IsValid)
//            {

//                RhinoApp.WriteLine("无效的Brep");
//                return Result.Failure;
//            }

//            // 1️⃣ 将 Brep 转为网格
//            MeshingParameters mp = new MeshingParameters(1); // 或 MeshingParameters.FastRenderMesh
//            Mesh[] meshes = Mesh.CreateFromBrep(brep, mp);

//            if (meshes == null || meshes.Length == 0)
//            {
//                RhinoApp.WriteLine("转换成网格失败");
//                return Result.Failure;
//            }

//            foreach (var mesh in meshes)
//            {

//                for (int i = 0; i < mesh.Vertices.Count; i++)
//                {
//                    Point3d pt = mesh.Vertices[i];

//                    // 调用你的处理函数
//                    if (!ProcessPoint(pt, out Point3d newPt, i.ToString()))
//                    {
//                        RhinoApp.WriteLine($"Mesh Vertex {i}: 处理失败");
//                        continue;
//                    }

//                    mesh.Vertices.SetVertex(i, newPt);
//                }
//                mesh.Normals.ComputeNormals();
//                mesh.Compact();
//                //_doc.Objects.AddMesh(mesh);
//                Brep nurbsBrep = GeometryUtils.ToBrepSafe(mesh);
//                if (nurbsBrep != null)
//                {
//                    _doc.Objects.AddBrep(nurbsBrep);
//                }
//                else
//                {
//                    RhinoApp.WriteLine("投影流动失败!");
//                }
//            }

//            _doc.Views.Redraw();
//            return Result.Success;
//        }


//        private Result ProcessBrepAsUntrimmedSrf(Brep brep)
//        {
//            if (brep == null) return Result.Failure;

//            var brepsToJoin = new List<Brep>();

//            foreach (BrepFace face in brep.Faces)
//            {
//                // 获取未修剪曲面
//                Surface srf = face.UnderlyingSurface();
//                if (srf == null) continue;

//                NurbsSurface ns = srf.ToNurbsSurface();
//                ns = GeometryUtils.DensifyNurbsSurface(ns, _controlPointMagnification);

//                // 遍历控制点进行变换
//                for (int u = 0; u < ns.Points.CountU; u++)
//                {
//                    for (int v = 0; v < ns.Points.CountV; v++)
//                    {
//                        ControlPoint cp = ns.Points.GetControlPoint(u, v);
//                        Point3d pt = cp.Location;
//                        if (!ProcessPoint(pt, out Point3d newPt, $"{u},{v}"))
//                        {
//                            RhinoApp.WriteLine($"Point {u},{v}: 处理失败");
//                            continue;
//                        }
//                        ns.Points.SetPoint(u, v, newPt);
//                    }
//                }

//                // 将变换后的曲面转为 Brep
//                Brep faceBrep = Brep.CreateFromSurface(ns);
//                if (faceBrep != null)
//                {
//                    brepsToJoin.Add(faceBrep);
//                }
//            }

//            if (brepsToJoin.Count == 0)
//                return Result.Failure;

//            // 尝试将所有变换后的 BrepFace 组合成一个完整 Brep
//            Brep[] joined = Brep.JoinBreps(brepsToJoin, _doc.ModelAbsoluteTolerance);
//            if (joined != null && joined.Length > 0)
//            {
//                foreach (var b in joined)
//                {
//                    _doc.Objects.AddBrep(b);
//                }
//                _doc.Views.Redraw();
//                return Result.Success;
//            }
//            else
//            {
//                RhinoApp.WriteLine("Brep faces 无法成功组合，已单独添加各面。");
//                foreach (var b in brepsToJoin)
//                {
//                    _doc.Objects.AddBrep(b);
//                }
//                _doc.Views.Redraw();
//                return Result.Success;
//            }
//        }





//        //private Result ProcessBrepAsUntrimmedSrf(Brep brep)
//        //{
//        //    foreach (BrepFace face in brep.Faces)
//        //    {
//        //        Surface srf = face.UnderlyingSurface();
//        //        NurbsSurface ns = srf.ToNurbsSurface();

//        //        for (int u = 0; u < ns.Points.CountU; u++)
//        //        {
//        //            for (int v = 0; v < ns.Points.CountV; v++)
//        //            {
//        //                ControlPoint cp = ns.Points.GetControlPoint(u, v);
//        //                Point3d pt = cp.Location;
//        //                if (!ProcessPoint(pt, out Point3d newPt, $"{u},{v}"))
//        //                {
//        //                    RhinoApp.WriteLine($"Point {u},{v}: 处理失败");
//        //                    continue;
//        //                }
//        //                ns.Points.SetPoint(u, v, newPt);
//        //            }
//        //        }
//        //        _doc.Objects.AddSurface(ns);
//        //    }
//        //    return Result.Success;
//        //}

//        private bool ProcessPoint(Point3d pt, out Point3d newPt, string pointId)
//        {
//            newPt = Point3d.Unset;
//            Point3d toPt;//投影方向与基础曲面的交点
//            Point3d fromPt;//与目标曲面的交点
//            //如果用户选择的投影方向是法向
//            //需要计算每个点在基础曲面上的最近点的法向量方向作为投影方向
//            if (_projecVectocIsNormlvector)
//            {
//                ////全局搜索最近点
//                if (!_baseBrep.ClosestPoint(pt, out _, out _, out _, out _, double.MaxValue, out Vector3d projectionDirectionNomal))
//                {
//                    RhinoApp.WriteLine("在基准曲面搜索最近点失败");
//                    return false;
//                }
//                ;
//                projectionDirectionNomal.Unitize();
//                fromPt = GeometryUtils.IntersectSurfaceAlongVector(_baseBrep, pt, projectionDirectionNomal);
//                toPt = GeometryUtils.IntersectSurfaceAlongVector(_targetBrep, pt, projectionDirectionNomal);
//            }
//            else
//            {
//                fromPt = GeometryUtils.IntersectSurfaceAlongVector(_baseBrep, pt, _projectionDirection);
//                toPt = GeometryUtils.IntersectSurfaceAlongVector(_targetBrep, pt, _projectionDirection);
//            }
//            if (fromPt == Point3d.Unset)
//            {
//                RhinoApp.WriteLine($"Point {pointId}: Invalid fromPt");
//                return false;
//            }

//            if (toPt == Point3d.Unset)
//            {
//                RhinoApp.WriteLine($"Point {pointId}: Invalid toPt");
//                return false;
//            }

//            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(fromPt));
//            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(toPt));

//            //投影变换
//            Point3d projectedPt = GeometryUtils.TransformPointAlongDirection(pt, fromPt, toPt, toPt - fromPt);
//            if (projectedPt == Point3d.Unset)
//            {
//                RhinoApp.WriteLine($"Point {pointId}: Invalid projectedPt");
//                return false;
//            }
//            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(projectedPt));
//            if (!_targetBrep.ClosestPoint(toPt, out _, out _, out _, out _, double.MaxValue, out Vector3d targetNormal))
//            {
//                RhinoApp.WriteLine("在目标曲面上查找toPt的法向量和最近点失败");
//                return false;
//            }

//            targetNormal.Unitize();

//            //旋转变换
//            if (_isFlowOnNormalVector)
//            {
//                newPt = GeometryUtils.RotatePointToVector(projectedPt, toPt, targetNormal, fromPt - toPt);
//                if (newPt == Point3d.Unset)
//                {
//                    RhinoApp.WriteLine($"Point {pointId}: Invalid newPt");
//                    return false;
//                }
//            }
//            else
//            {
//                newPt = projectedPt;
//            }
//            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(newPt));



//            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new TextDot("pt", projectedPt));
//            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new TextDot("rt", newPt));
//            return newPt != Point3d.Unset;
//        }
//    }
//}


////https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.morphcontrol