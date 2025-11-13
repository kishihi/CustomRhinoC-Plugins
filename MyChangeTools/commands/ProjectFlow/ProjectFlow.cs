using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;

namespace MyChangeTools.commands.ProjectFlow
{
    public class ProjectFlow : Command
    {
        public override string EnglishName => "ProjectFlow";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1️⃣ Select the objects to flow (curve or surface)
            _ = new SelectionHelper();
            var rc = SelectionHelper.SelectGeometries(doc, "Select curve or surface to flow", ObjectType.AnyObject, out ObjRef[] objRefs);
            if (rc != Result.Success) return Result.Failure;

            // 2️⃣ Select base surface
            rc = SelectionHelper.SelectSurface(doc, "Select base surface", out Brep baseBrep);
            if (rc != Result.Success) return Result.Failure;

            // 3️⃣ Select target surface
            rc = SelectionHelper.SelectSurface(doc, "Select target surface", out Brep targetBrep);
            if (rc != Result.Success) return Result.Failure;

            // 4 Get project vector
            rc = SelectionHelper.GetProjectVector(out Vector3d projectVector, out bool isNormlvector, out bool isFlowOnNormalVector,out bool isTransformToMeshToAppylyTransForm, out int controlPointMagnification);
            if (rc != Result.Success) return Result.Failure;

            // 5 Process geometry
            var processor = new GeometryProcessor(doc, objRefs, baseBrep, targetBrep, projectVector, isNormlvector, isFlowOnNormalVector,isTransformToMeshToAppylyTransForm, controlPointMagnification);
            rc = processor.Process();
            if (rc != Result.Success) return Result.Failure;

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}






//代码原型demo
//using Rhino;
//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using Rhino.Input;
//namespace MyChangeTools.ProjectFlow
//{
//    public class ProjectFlow : Command
//    {
//        public override string EnglishName => "ProjectFlow";

//        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//        {
//            // 1️⃣ 选择要流动的对象（曲线或曲面）
//            var rc = RhinoGet.GetOneObject("选择要流动的曲线或曲面", false,
//                ObjectType.Curve | ObjectType.Brep, out ObjRef objRef);
//            if (rc != Result.Success) return rc;

//            var geom = objRef.Geometry();
//            if (geom == null) return Result.Failure;

//            //RhinoApp.WriteLine("已经获取要流动的曲线或曲面");

//            RhinoDoc.ActiveDoc.Objects.UnselectAll();

//            // 2️⃣ 选择基准曲面
//            rc = RhinoGet.GetOneObject("选择基准曲面", false, ObjectType.Brep, out ObjRef baseRef);
//            if (rc != Result.Success) return rc;
//            var baseSrf = baseRef.Surface();
//            if (baseSrf == null) return Result.Failure;

//            //RhinoApp.WriteLine("已经获取基准曲面");

//            RhinoDoc.ActiveDoc.Objects.UnselectAll();

//            // 3️⃣ 选择目标曲面
//            rc = RhinoGet.GetOneObject("选择目标曲面", false, ObjectType.Brep, out ObjRef targetRef);
//            if (rc != Result.Success) return rc;
//            var targetSrf = targetRef.Surface();
//            if (targetSrf == null) return Result.Failure;

//            //RhinoApp.WriteLine("已经获取目标曲面");
//            RhinoDoc.ActiveDoc.Objects.UnselectAll();

//            // 固定投影方向
//            Vector3d projDir = Vector3d.ZAxis;

//            // 4️⃣ 分两种情况处理
//            if (geom is Curve curve)
//            {
//                // 曲线：控制点映射
//                NurbsCurve nc = curve.ToNurbsCurve();
//                for (int i = 0; i < nc.Points.Count; i++)
//                {   
//                    Point3d pt = nc.Points[i].Location;

//                    // 在投影方向上找到与两曲面的交点
//                    Point3d fromPt = ProjectFlow.IntersectSurfaceAlongVector(baseSrf, pt, projDir);
//                    Point3d toPt = ProjectFlow.IntersectSurfaceAlongVector(targetSrf, pt, projDir);

//                    //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(fromPt));
//                    //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(toPt));

//                    if (fromPt == Point3d.Unset)
//                    {
//                        RhinoApp.WriteLine($"in {i} , fromPt 无效");
//                        continue;
//                    }
//                    if (toPt == Point3d.Unset)
//                    {
//                        RhinoApp.WriteLine($"in {i} , toPt 无效");
//                        continue;
//                    }

//                    //投影点
//                    Point3d curveControlpt2 = TransformPointAlongDirection(pt, fromPt, toPt, toPt - fromPt);

//                    //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(curveControlpt2));
//                    //RhinoDoc.ActiveDoc.Objects.Add(new TextDot($"{i}", curveControlpt2));

//                    if (curveControlpt2 == Point3d.Unset) continue;

//                    // 获取目标曲面的法向
//                    if (!targetSrf.ClosestPoint(toPt, out double u, out double v)) continue;
//                    Vector3d targetNormal = targetSrf.NormalAt(u, v);
//                    targetNormal.Unitize();

//                    //再次法向变换点
//                    Point3d curveControlpt3 = RotatePointToVector(curveControlpt2, toPt, targetNormal, fromPt - toPt);

//                    //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(curveControlpt3));

//                    if (curveControlpt3 == Point3d.Unset) continue;




//                    // 移动控制点到目标曲面法向方向
//                    Point3d newPt = curveControlpt3;
//                    nc.Points.SetPoint(i, newPt);
//                }
//                doc.Objects.AddCurve(nc);
//            }
//            else if (geom is Brep brep)
//            {
//                foreach (BrepFace face in brep.Faces)
//                {
//                    Surface srf = face.UnderlyingSurface();
//                    NurbsSurface ns = srf.ToNurbsSurface();
//                    for (int u = 0; u < ns.Points.CountU; u++)
//                    {
//                        for (int v = 0; v < ns.Points.CountV; v++)
//                        {
//                            ControlPoint cp = ns.Points.GetControlPoint(u, v);
//                            Point3d pt = cp.Location;

//                            Point3d fromPt = ProjectFlow.IntersectSurfaceAlongVector(baseSrf, pt, projDir);
//                            Point3d toPt = ProjectFlow.IntersectSurfaceAlongVector(targetSrf, pt, projDir);

//                            if (fromPt == Point3d.Unset)
//                            {
//                                RhinoApp.WriteLine($"in {u} {v} , fromPt 无效");
//                                continue;
//                            }
//                            if (toPt == Point3d.Unset)
//                            {
//                                RhinoApp.WriteLine($"in {u} {v} , toPt 无效");
//                                continue;
//                            }

//                            //投影点
//                            Point3d curveControlpt2 = TransformPointAlongDirection(pt, fromPt, toPt, toPt - fromPt);
//                            if (curveControlpt2 == Point3d.Unset) continue;

//                            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(fromPt));
//                            //Rhino.RhinoDoc.ActiveDoc.Objects.Add(new Point(toPt));

//                            if (!targetSrf.ClosestPoint(toPt, out double uu, out double vv)) continue;
//                            Vector3d targetNormal = targetSrf.NormalAt(uu, vv);
//                            targetNormal.Unitize();

//                            //再次法向变换点
//                            Point3d curveControlpt3 = RotatePointToVector(curveControlpt2, toPt, targetNormal, fromPt - toPt);

//                            if (curveControlpt3 == Point3d.Unset) continue;

//                            Point3d newPt = curveControlpt3;
//                            ns.Points.SetPoint(u, v, newPt);
//                        }
//                    }
//                    doc.Objects.AddSurface(ns);
//                }
//            }

//            doc.Views.Redraw();
//            return Result.Success;
//        }

//        //统一转为Brep
//        public static Brep ToBrepSafe(GeometryBase geom)
//        {
//            if (geom == null)
//                return null;

//            if (geom is Brep)
//                return geom as Brep;

//            if (geom is Extrusion)
//                return ((Extrusion)geom).ToBrep();

//            if (geom is Surface)
//                return ((Surface)geom).ToBrep();

//            if (geom is SubD)
//                return ((SubD)geom).ToBrep(new SubDToBrepOptions());

//            if (geom is Mesh)
//                return Brep.CreateFromMesh((Mesh)geom, true);

//            return null;
//        }

//        /// <summary>
//        /// 从点沿某个方向的无限长直线与曲面的交点
//        /// </summary>
//        /// 
//        public static Point3d IntersectSurfaceAlongVector(GeometryBase geom, Point3d fromPt, Vector3d dir)
//        {
//            // 基础检查
//            if (geom == null || !fromPt.IsValid || !dir.IsValid || dir.IsTiny())
//                return Point3d.Unset;

//            Brep brep = ToBrepSafe(geom);
//            if (brep == null || !brep.IsValid)
//                return Point3d.Unset;

//            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

//            // 构造一条“无限长”的射线线段（Line是Curve的子类）
//            Line line = new Line(fromPt, dir, 1e6);
//            Curve lineCurve = new LineCurve(line);

//            // 计算交点
//            if (Rhino.Geometry.Intersect.Intersection.CurveBrep(
//                    lineCurve, brep, tol, out Curve[] overlapCurves, out Point3d[] intersectionPoints))
//            {
//                if (intersectionPoints != null && intersectionPoints.Length > 0)
//                {

//                    foreach (var p in intersectionPoints)
//                    {
//                        if (p != Point3d.Unset) return p;
//                    }
//                }
//            }

//            // 如果正方向没找到，尝试反方向
//            Line revLine = new Line(fromPt, -dir, 1e6);
//            Curve revCurve = new LineCurve(revLine);

//            if (Rhino.Geometry.Intersect.Intersection.CurveBrep(
//                    revCurve, brep, tol, out Curve[] _, out Point3d[] revPoints))
//            {
//                if (revPoints != null && revPoints.Length > 0)
//                {
//                    foreach (var p in revPoints)
//                    {
//                        if (p != Point3d.Unset) return p;
//                    }

//                }
//            }

//            return Point3d.Unset;
//        }



//        /// <summary>
//        /// 将点A按照B1->B2的变化沿着vectorA方向变换
//        /// </summary>
//        public static Point3d TransformPointAlongDirection(Point3d A, Point3d B1, Point3d B2, Vector3d vectorA)
//        {
//            Vector3d move = B2 - B1;
//            if (!vectorA.Unitize())
//                return Point3d.Unset;

//            double moveLen = move * vectorA; // 取投影分量
//            Vector3d projectedMove = vectorA * moveLen;

//            return A + projectedMove;
//        }

//        //将点a以基点b,baseVec旋转,以对齐到过点b的向量V. 并得到a2,
//        public static Point3d RotatePointToVector(Point3d a, Point3d basePoint, Vector3d targetVec, Vector3d baseVec)
//        {

//            // 确保向量有效
//            if (!baseVec.Unitize() || !targetVec.Unitize())
//                return Point3d.Unset;

//            // 计算旋转轴 = 两向量的叉积
//            Vector3d axis = Vector3d.CrossProduct(baseVec, targetVec);
//            double angle = Vector3d.VectorAngle(baseVec, targetVec);

//            // 构造旋转变换
//            Transform rotation = Transform.Rotation(angle, axis, basePoint);

//            // 应用变换到点a
//            Point3d a2 = a;
//            a2.Transform(rotation);

//            return a2;
//        }
//    }
//}