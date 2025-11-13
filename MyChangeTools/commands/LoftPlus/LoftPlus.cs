//using Rhino;
//using Rhino.Collections;
//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using Rhino.Input;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace MyChangeTools.LoftPlus
//{
//    public class LoftPlus : Command
//    {

//        public override string EnglishName => "LoftPlus";

//        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//        {
//            // --- 1. 获取两条曲线作为轨道 ---
//            ObjRef[] objRefs;
//            var rc = RhinoGet.GetMultipleObjects("Select two rail curves", false, ObjectType.Curve, out objRefs);
//            if (rc != Result.Success || objRefs.Length != 2)
//                return Result.Cancel;

//            double threshold = 0.0;
//            Rhino.Commands.Result rb = Rhino.Input.RhinoGet.GetNumber(
//                "请输入阈值",   // 提示信息
//                false,           // 是否可选（true 表示可以按回车使用默认值）
//                ref threshold    // 输出值
//            );

//            if (rc != Rhino.Commands.Result.Success)
//            {
//                Rhino.RhinoApp.WriteLine("用户取消输入。");
//                return rc;
//            }

//            Rhino.RhinoApp.WriteLine($"用户输入的阈值: {threshold}");


//            Curve rail1 = objRefs[0].Curve();
//            Curve rail2 = objRefs[1].Curve();

//            if (rail1 == null || rail2 == null) return Result.Failure;

//            // 确保曲线方向大致一致 (非常重要!)
//            if (rail1.IsClosed != rail2.IsClosed)
//            {
//                RhinoApp.WriteLine("Curves must both be open or both closed.");
//                return Result.Failure;
//            }

            

//            // --- 2. 获取并匹配所有连接点 ---
//            // 使用之前定义的 GetAllEndPoints 函数 (假设 CurveUtilities 类存在)
//            double tol = doc.ModelAbsoluteTolerance;
//            List<Point3d> points1 = GetAllEndPoints(rail1, doc);
//            List<Point3d> points2 = GetAllEndPoints(rail2, doc);


//            // 尝试自动对齐方向
//            if (rail1.PointAtStart.DistanceTo(rail2.PointAtEnd) < rail1.PointAtStart.DistanceTo(rail2.PointAtStart))
//            {
//                rail2.Reverse();
//            }

//            //if (points1[0].DistanceTo(rail1.PointAtStart) > doc.ModelAbsoluteTolerance) { }


//            // --- 3. 生成引导线/截面线 (Cross-section curves) ---
//            var guideCurves = new List<Curve>();

//            int count = System.Math.Min(points1.Count, points2.Count);

//            List<Point3d> points11 = new List<Point3d>();
//            List<Point3d> points22 = new List<Point3d>();


//            for (int i = 0; i < count; i++)
//            {

//                Point3d p1 = points1[i];
//                if (points11.Contains(p1)) continue;
//                Point3d p2 = FindClosest(p1, points2);
//                if (points22.Contains(p2)) continue;

//                if (p1.DistanceTo(p2) > threshold)
//                {
//                    continue;
//                }

//                doc.Objects.AddPoint(p1);
//                doc.Objects.AddPoint(p2);
//                //doc.Views.Redraw();


//                points11.Add(p1);
//                points22.Add(p2);
//                //doc.Objects.

//                // 创建连接两个匹配点的直线作为截面引导
//                Line crossSection = new Line(p1, p2);
//                guideCurves.Add(crossSection.ToNurbsCurve());
//                //doc.Objects.
//                // 可选：在文档中添加这些点和线以调试
//                doc.Objects.AddLine(crossSection);
//            }

//            if (points11.Count != points22.Count)
//            {

//                RhinoApp.WriteLine("Warning: The number of junction points do not match. Proceeding with potentially less accurate matching.");
//                doc.Objects.AddPoints(points11);
//                doc.Objects.AddPoints(points22);

//                //如果点数不一致，可以采用更复杂的插值逻辑，这里简化处理
//            }

//            // --- 4. 执行双轨成面 (Sweep Two Rails) ---

//            // 将轨道和引导线转换为 Rhino.Geometry.Curve 数组
//            Curve[] rails = { rail1, rail2 };
//            Curve[] sections = guideCurves.ToArray();

//            // 使用 SweepTwoRailsList 函数，这是最灵活的双轨扫掠方法
//            // 它允许同时输入轨道和一系列有序的截面线
//            Brep[] surfaces = Brep.CreateFromSweep(
//                rail1,
//                rail2,
//                guideCurves,
//                rail1.IsClosed && rail2.IsClosed,
//                doc.ModelAbsoluteTolerance
//            );

//            // --- 5. 处理结果 ---
//            if (surfaces != null && surfaces.Length > 0)
//            {
//                foreach (Brep surface in surfaces)
//                {
//                    doc.Objects.AddBrep(surface);
//                }
//                doc.Views.Redraw();
//                RhinoApp.WriteLine($"Successfully created {surfaces.Length} surface(s) using custom points matching.");
//                return Result.Success;
//            }
//            else
//            {
//                RhinoApp.WriteLine("Failed to create surfaces with the Super Sweep command.");
//                return Result.Failure;
//            }
//            //return Result.Success;
//        }

//        public static List<Point3d> GetPoints(Curve curve, int pointCount)
//        {

//            pointCount = Math.Max(2, pointCount);
//            // 3. 获取曲线对应点
//            List<Point3d> pts = new List<Point3d>();
//            for (int i = 0; i <= pointCount; i++)
//            {
//                double t = i / (double)pointCount;
//                pts.Add(curve.PointAtNormalizedLength(t));
//            }
//            return pts;
//        }

//        /// <summary>
//        /// 获取输入曲线的所有端点列表。
//        /// 自动处理单条曲线、多段线和组合曲线。
//        /// </summary>
//        /// <param name="curve">输入的曲线对象。</param>
//        /// <param name="tolerance">用于去除重复点的容差，通常使用 RhinoDoc.ModelAbsoluteTolerance。</param>
//        /// <returns>包含所有唯一端点/连接点的列表 (Point3d)。</returns>
//        public static List<Point3d> GetAllEndPoints(Curve curve, RhinoDoc doc)
//        {
//            PolyCurve polyCurve = curve as PolyCurve;
//            if (polyCurve != null)
//            {
//                Rhino.Geometry.Curve[] segments = new Curve[polyCurve.SegmentCount];

//                for (int i = 0; i < segments.Length; i++)
//                {
//                    segments.Append(polyCurve.SegmentCurve(i));
//                }
//                var endpoints = new List<Rhino.Geometry.Point3d>();

//                foreach (Rhino.Geometry.Curve segment in segments)
//                {
//                    Rhino.Geometry.Point3d startPoint = segment.PointAtStart;
//                    if (!endpoints.Contains(startPoint))
//                    {
//                        endpoints.Add(startPoint);
//                    }
//                    Rhino.Geometry.Point3d endPoint = segment.PointAtEnd;
//                    if (!endpoints.Contains(endPoint))
//                    {
//                        endpoints.Add(endPoint);
//                    }
//                }

//                Curve rebuildc = curve.Fit(curve.Degree, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceDegrees);

//                var sortpoint_ts = new List<(Point3d, double)>();

//                foreach (Point3d point in endpoints)
//                {
//                    rebuildc.ClosestPoint(point, out double t);
//                    sortpoint_ts.Add((point, t));
//                }
//                sortpoint_ts.Sort((a, b) => a.Item2.CompareTo(b.Item2));

//                return sortpoint_ts.Select(a => a.Item1).ToList();
//            }
//            else
//            {
//                var endpoints = new List<Rhino.Geometry.Point3d>();
//                endpoints.Add(curve.PointAtStart);
//                endpoints.Add(curve.PointAtEnd);
//                return endpoints;
//            }
//        }


//        // 找到最近端点
//        private Point3d FindClosest(Point3d pt, List<Point3d> list)
//        {
//            double minDist = double.MaxValue;
//            Point3d closest = list[0];
//            foreach (var p in list)
//            {
//                double d = pt.DistanceTo(p);
//                if (d < minDist)
//                {
//                    minDist = d;
//                    closest = p;
//                }
//            }
//            return closest;
//        }

//    }
//}