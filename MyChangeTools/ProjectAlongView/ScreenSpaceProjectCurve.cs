//using Rhino;
//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using Rhino.Input.Custom;
//using System;
//using System.Collections.Generic;

//namespace MyChangeTools.ProjectAlongView
//{
//    public class ScreenSpaceProjectCurve : Command
//    {

//        public override string EnglishName => "ScreenSpaceProjectCurve";

//        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//        {
//            var vp = doc.Views.ActiveView?.ActiveViewport;
//            if (vp == null)
//            {
//                RhinoApp.WriteLine("No active view.");
//                return Result.Failure;
//            }

//            // 1. 选择多条曲线
//            var gc = new GetObject();
//            gc.SetCommandPrompt("Select curves to project");
//            gc.GeometryFilter = ObjectType.Curve;
//            gc.EnablePreSelect(true, true);
//            gc.GroupSelect = true;
//            gc.GetMultiple(1, 0);
//            if (gc.CommandResult() != Result.Success)
//                return gc.CommandResult();

//            List<Curve> curves = new List<Curve>();
//            for (int i = 0; i < gc.ObjectCount; i++)
//            {
//                Curve c = gc.Object(i).Curve();
//                if (c != null) curves.Add(c);
//            }

//            if (curves.Count == 0)
//            {
//                RhinoApp.WriteLine("No valid curves selected.");
//                return Result.Failure;
//            }

//            // 2. 选择目标 Brep
//            var gb = new GetObject();
//            gb.SetCommandPrompt("Select target Breps");
//            gb.GeometryFilter = ObjectType.Brep;
//            gb.GroupSelect = true;
//            gb.GetMultiple(1, 0);
//            if (gb.CommandResult() != Result.Success)
//                return gb.CommandResult();

//            List<Brep> breps = new List<Brep>();
//            for (int i = 0; i < gb.ObjectCount; i++)
//            {
//                Brep b = gb.Object(i).Brep();
//                if (b != null) breps.Add(b);
//            }

//            if (breps.Count == 0)
//            {
//                RhinoApp.WriteLine("No valid Breps selected.");
//                return Result.Failure;
//            }

//            // 3. 获取屏幕变换矩阵
//            var xformWorldToScreen = vp.GetTransform(CoordinateSystem.World, CoordinateSystem.Screen);
//            var xformScreenToWorld = vp.GetTransform(CoordinateSystem.Screen, CoordinateSystem.World);

//            // 4. 可视化投影方向（从第一条曲线起点出发）
//            //Point3d startPt = curves[0].PointAtStart;
//            //Vector3d screenDir = new Vector3d(0, 0, -1); // 屏幕空间向内
//            //Point3d endPt = startPt + screenDir * 10000;
//            //doc.Objects.AddLine(new Line(startPt, endPt));
//            //RhinoApp.WriteLine("Direction test line drawn.");

//            // 5. 投影每条曲线
//            double tol = doc.ModelAbsoluteTolerance;
//            int samplePoints = 50; // 采样点数
//            int successCount = 0;

//            foreach (var c in curves)
//            {
//                Polyline projected = new Polyline();

//                for (int i = 0; i <= samplePoints; i++)
//                {
//                    double t = i / (double)samplePoints;
//                    Point3d pt = c.PointAtNormalizedLength(t);

//                    // 转屏幕坐标
//                    Point3d screenPt = pt;
//                    screenPt.Transform(xformWorldToScreen);

//                    // 在屏幕空间沿 Z 投影到平面
//                    screenPt.Z = 0;

//                    // 反变换回世界坐标
//                    screenPt.Transform(xformScreenToWorld);

//                    // 如果需要投影到 Brep，沿视线方向求交点
//                    Ray3d ray = new Ray3d(screenPt, vp.CameraDirection);
//                    Point3d closestPt = screenPt;
//                    double minDist = double.MaxValue;

//                    foreach (var brep in breps)
//                    {
//                        var intersectPts = Rhino.Geometry.Intersect.Intersection.RayShoot(ray, new[] { brep }, 1);
//                        if (intersectPts != null && intersectPts.Length > 0)
//                        {
//                            double d = screenPt.DistanceTo(intersectPts[0]);
//                            if (d < minDist)
//                            {
//                                minDist = d;
//                                closestPt = intersectPts[0];
//                            }
//                        }
//                    }

//                    projected.Add(closestPt);
//                }

//                if (projected.Count > 1)
//                {
//                    doc.Objects.AddPolyline(projected);
//                    successCount++;
//                }
//            }

//            doc.Views.Redraw();
//            RhinoApp.WriteLine($"Projection completed: {successCount}/{curves.Count} curve(s) projected.");

//            return Result.Success;
//        }
//    }
//}