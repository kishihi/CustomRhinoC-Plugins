using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mesh = Rhino.Geometry.Mesh;
namespace MyRhinoSelectTools.Commands.SelectAboveSurface
{

    public static class GeometrySampler
    {
        /// <summary>
        /// 对任何几何对象均匀取样指定数量的点，即使点不够也补齐
        /// </summary>
        public static List<Point3d> SamplePoints(GeometryBase geom, int count = 5)
        {
            var pts = new List<Point3d>();
            if (geom == null || count <= 0)
                return pts;

            // --- 按类型分别处理 ---
            switch (geom)
            {
                case Point pt:
                    pts.Add(pt.Location);
                    break;

                case Curve crv:
                    {
                        double step = 1.0 / (count - 1);
                        for (int i = 0; i < count; i++)
                            pts.Add(crv.PointAtNormalizedLength(i * step));
                        break;
                    }

                case Brep brep:
                    {
                        var meshes = Mesh.CreateFromBrep(brep, MeshingParameters.FastRenderMesh);
                        if (meshes != null && meshes.Length > 0)
                            pts.AddRange(meshes.SelectMany(m => m.Vertices.Select(v => (Point3d)v)));
                        break;
                    }

                case Surface srf:
                    {
                        var mesh = Mesh.CreateFromSurface(srf, MeshingParameters.FastRenderMesh);
                        if (mesh != null)
                            pts.AddRange(mesh.Vertices.Select(v => (Point3d)v));
                        break;
                    }

                case SubD subd:
                    {
                        if (subd.Vertices.Count > 0)
                        {
                            var v = subd.Vertices.First;
                            for (int i = 0; i < subd.Vertices.Count; i++)
                            {
                                pts.Add(v.SurfacePoint());
                                v = v.Next;
                            }
                        }
                        break;
                    }

                case Mesh mesh:
                    {
                        pts.AddRange(mesh.Vertices.Select(v => (Point3d)v));
                        break;
                    }

                default:
                    {
                        var bbox = geom.GetBoundingBox(true);
                        pts.Add(bbox.Center);
                        break;
                    }
            }

            // --- 确保返回点数量等于 count ---
            var finalPts = new List<Point3d>();
            if (pts.Count == 0)
            {
                // 没有点则生成重复的 bbox 中心
                var defaultPt = geom.GetBoundingBox(true).Center;
                for (int i = 0; i < count; i++)
                    finalPts.Add(defaultPt);
            }
            else if (pts.Count >= count)
            {
                // 点够多，均匀采样
                for (int i = 0; i < count; i++)
                {
                    int idx = (int)((i / (double)(count - 1)) * (pts.Count - 1));
                    finalPts.Add(pts[idx]);
                }
            }
            else
            {
                // 点不够，循环补齐
                for (int i = 0; i < count; i++)
                {
                    finalPts.Add(pts[i % pts.Count]);
                }
            }

            return finalPts;
        }
    }

    internal class SurfaceCompute
    {

        public static Result ComputeDirection(
    RhinoDoc doc,
    ObjRef baseBrepRef,
    ObjRef[] objRefs,
    out ConcurrentBag<Guid> abovedObjIds,
    out ConcurrentBag<Guid> belowObjIds,
    out ConcurrentBag<Guid> onObjids,
    out ConcurrentBag<(Point3d pt, string text)> textDots)
        {
            Brep baseBrep = CustomQuickClass.QuickGeometry.ToBrepSafe(baseBrepRef.Geometry());
            double tol = doc.ModelAbsoluteTolerance;

            // 临时局部变量，不是 out 参数
            var _abovedObjIds = new ConcurrentBag<Guid>();
            var _belowObjIds = new ConcurrentBag<Guid>();
            var _onObjids = new ConcurrentBag<Guid>();
            var _textDots = new ConcurrentBag<(Point3d pt, string text)>();

            if (baseBrep == null || baseBrep.Faces.Count == 0)
            {
                abovedObjIds = _abovedObjIds;
                belowObjIds = _belowObjIds;
                onObjids = _onObjids;
                textDots = _textDots;
                return Result.Failure;
            }

            Parallel.ForEach(objRefs, objRef =>
            {
                if (objRef.ObjectId == baseBrepRef.ObjectId)
                    return;

                var geom = objRef.Geometry();
                if (geom == null)
                    return;






                //pts
                int aboveCount = 0;
                int belowCount = 0;
                int onCount = 0;
                int sampleNumber = 0;

                {
                    Point3d centroid = CustomQuickClass.QuickGeometry.GetCentroidFast(geom);


                    TryGetStableClosestPoint(baseBrep, centroid, out Point3d pInBrepCloest, out Vector3d normal);

                    if (centroid == Point3d.Unset || pInBrepCloest == Point3d.Unset)
                        return;

                    Vector3d vec = centroid - pInBrepCloest;
                    if (vec.IsTiny())
                    {
                        _onObjids.Add(objRef.ObjectId);
                        return;
                    }

                    vec.Unitize();
                    normal.Unitize();

                    double dot = vec * normal;
                    if (dot > tol)
                        //_abovedObjIds.Add(objRef.ObjectId);
                        aboveCount++;
                    else if (dot < -tol)
                        //_belowObjIds.Add(objRef.ObjectId);
                        belowCount++;
                    else
                        //_onObjids.Add(objRef.ObjectId);
                        onCount++;
                }


                foreach (var samplept in GeometrySampler.SamplePoints(geom, sampleNumber))

                {

                    //TryGetStableClosestPoint(baseBrep, samplept, out Point3d pInBrepCloest, out Vector3d normal);
                    baseBrep.ClosestPoint(samplept, out Point3d pInBrepCloest, out ComponentIndex ci, out double s, out double t, double.MaxValue, out Vector3d normal);

                    if (samplept == Point3d.Unset || pInBrepCloest == Point3d.Unset)
                        return;

                    Vector3d vec = samplept - pInBrepCloest;
                    if (vec.IsTiny())
                    {
                        _onObjids.Add(objRef.ObjectId);
                        return;
                    }

                    vec.Unitize();
                    normal.Unitize();

                    double dot = vec * normal;
                    if (dot > tol)
                        //_abovedObjIds.Add(objRef.ObjectId);
                        aboveCount++;
                    else if (dot < -tol)
                        //_belowObjIds.Add(objRef.ObjectId);
                        belowCount++;
                    else
                        //_onObjids.Add(objRef.ObjectId);
                        onCount++;
                    //doc.Objects.AddPoint(samplept);
                    _textDots.Add((samplept, ""));
                }

                if (aboveCount + onCount > belowCount)
                    _abovedObjIds.Add(objRef.ObjectId);
                else if (belowCount + onCount > aboveCount)
                    _belowObjIds.Add(objRef.ObjectId);
                else
                    _onObjids.Add(objRef.ObjectId);



            });

            // 最后再赋值给 out 参数
            abovedObjIds = _abovedObjIds;
            belowObjIds = _belowObjIds;
            onObjids = _onObjids;
            textDots = _textDots;

            return Result.Success;
        }



        /// <summary>
        /// 获取 Brep 上稳定的最近点与法向量。
        /// 自动避开边缘和角点，保证法向量来自单一面。
        /// </summary>
        /// <param name="brep">要查询的 Brep</param>
        /// <param name="testPt">测试点</param>
        /// <param name="ptOnFace">输出：Brep 面上的点</param>
        /// <param name="normal">输出：法向量</param>
        /// <param name="tolerance">可选公差</param>
        /// <returns>是否成功获取稳定结果</returns>
        public static bool TryGetStableClosestPoint(Brep brep, Point3d testPt, out Point3d ptOnFace, out Vector3d normal, double tolerance = 1e-6)
        {
            ptOnFace = Point3d.Unset;
            normal = Vector3d.Unset;

            if (brep == null)
                return false;

            // 获取最近点
            if (!brep.ClosestPoint(testPt,
                out Point3d pClosest,
                out ComponentIndex ci,
                out double s,
                out double t,
                double.MaxValue,
                out Vector3d n))
            {
                return false;
            }

            // 默认输出
            ptOnFace = pClosest;
            normal = n;



            return true;
        }


        //        RhinoApp.WriteLine($"{ci.ComponentIndexType}");

        //            // ---------- ① 若命中的是 Edge 或 Vertex ----------
        //            // ---------- ① 若命中的是 Edge 或 Vertex ----------
        //            if (ci.ComponentIndexType == ComponentIndexType.BrepEdge ||
        //                ci.ComponentIndexType == ComponentIndexType.BrepVertex)
        //            {
        //                Console.WriteLine("当前最近点在 brep 的边或顶点上");

        //                BrepFace nearestFace = null;
        //        double minDist = double.MaxValue;
        //        double bestU = 0.0, bestV = 0.0;

        //                // 找到距离 pClosest 最近的面，同时记录该面的参数坐标 u1,v1
        //                foreach (var face in brep.Faces)
        //                {
        //                    if (face.ClosestPoint(pClosest, out double u1, out double v1))
        //                    {
        //                        var testOnFace = face.PointAt(u1, v1);
        //        double dist = testOnFace.DistanceTo(pClosest);
        //                        if (dist<minDist)
        //                        {
        //                            minDist = dist;
        //                            nearestFace = face;
        //                            bestU = u1;
        //                            bestV = v1;
        //                        }
        //}
        //                }

        //                if (nearestFace != null)
        //{
        //    // 检查 bestU/bestV 是否靠近该面参数域边界
        //    var domU = nearestFace.Domain(0);
        //    var domV = nearestFace.Domain(1);

        //    // 参数域尺寸（防止参数空间非常小/非常大时失效）
        //    double lenU = Math.Abs(domU.Length);
        //    double lenV = Math.Abs(domV.Length);

        //    // 用于判定“靠边”的阈值：基于域长度与几何容差
        //    double paramEps = Math.Max(tolerance * 10.0, Math.Min(lenU, lenV) * 1e-6);

        //    bool nearBoundary =
        //        Math.Abs(bestU - domU.T0) < paramEps ||
        //        Math.Abs(bestU - domU.T1) < paramEps ||
        //        Math.Abs(bestV - domV.T0) < paramEps ||
        //        Math.Abs(bestV - domV.T1) < paramEps;

        //    double uFinal = bestU;
        //    double vFinal = bestV;

        //    if (nearBoundary)
        //    {
        //        // 将 (uFinal,vFinal) 向参数域中心微调一点，确保落在内部
        //        // 调整量按域长度的一个小比例或按公差的倍数来决定
        //        double uMargin = Math.Max(tolerance * 20.0, lenU * 1e-4);
        //        double vMargin = Math.Max(tolerance * 20.0, lenV * 1e-4);

        //        // clamp 到 [T0 + uMargin, T1 - uMargin]（防止越界）
        //        uFinal = RhinoMath.Clamp(uFinal, domU.T0 + uMargin, domU.T1 - uMargin);
        //        vFinal = RhinoMath.Clamp(vFinal, domV.T0 + vMargin, domV.T1 - vMargin);

        //        // 如果域非常窄，Clamp 可能将 uFinal/vFinal 变成 dom 中点，确保在内
        //        uFinal = RhinoMath.Clamp(uFinal, domU.T0, domU.T1);
        //        vFinal = RhinoMath.Clamp(vFinal, domV.T0, domV.T1);
        //    }

        //    // 此时 uFinal/vFinal 应该在面内部（或至少不太靠近边界）
        //    ptOnFace = nearestFace.PointAt(uFinal, vFinal);

        //    // 得到面法向（来自该面内部）
        //    normal = nearestFace.NormalAt(uFinal, vFinal);
        //    normal.Unitize();

        //    // 为了保险，可以在法向方向再稍微偏移一点点到面外 (可选)
        //    //ptOnFace = ptOnFace + normal * (tolerance * 5.0);

        //    return true;
        //}
        //            }



        //public static bool GetBaseBrep(out ObjRef basebrepref, out int direction)
        //{
        //    basebrepref = null;
        //    direction = 0;

        //    var go = new GetObject();
        //    go.SetCommandPrompt("选择一个基准曲面或多重曲面");
        //    go.GeometryFilter = ObjectType.Brep;
        //    go.EnablePreSelect(true, true);
        //    go.EnablePostSelect(true);

        //    // 添加选项
        //    int opUpIndex = go.AddOption("曲面上方");
        //    int opDownIndex = go.AddOption("曲面下方");
        //    int opOnIndex = go.AddOption("曲面上");

        //    for (; ; )
        //    {
        //        var res = go.Get();
        //        if (res == GetResult.Cancel)
        //        {
        //            RhinoApp.WriteLine("用户取消了操作。");
        //            return false;
        //        }
        //        else if (res == GetResult.Option)
        //        {
        //            // 处理选项
        //            int opt = go.Option().Index;
        //            if (opt == opUpIndex)
        //            {
        //                direction = 1;
        //                RhinoApp.WriteLine("方向：曲面上方");
        //            }
        //            else if (opt == opDownIndex)
        //            {
        //                direction = -1;
        //                RhinoApp.WriteLine("方向：曲面下方");
        //            }
        //            else if (opt == opOnIndex)
        //            {
        //                direction = 0;
        //                RhinoApp.WriteLine("方向：曲面上");
        //            }
        //            continue; // 回到选择状态
        //        }
        //        else if (res == GetResult.Object)
        //        {
        //            basebrepref = go.Object(0);
        //            if (basebrepref != null)
        //            {
        //                RhinoApp.WriteLine($"选中对象：{basebrepref.ObjectId}");
        //                return true;
        //            }
        //        }
        //    }
        //}



        //public static bool GetCheckObjects(out ObjRef[] checkObjects)
        //{

        //    using (var escHandler = new MyRhinoSelectTools.CustomQuickClass.QuickGetObject.EscapeKeyEventHandler("选择要计算的对象（按 ESC 取消）"))
        //    {

        //        //RhinoDoc.ActiveDoc.Objects.UnselectAll();
        //        var go = new GetObject();
        //        go.SetCommandPrompt("选择需要检查的物体");
        //        go.GeometryFilter = ObjectType.AnyObject;
        //        go.GroupSelect = true;
        //        go.EnablePostSelect(false);
        //        go.GetMultiple(1, 0);

        //        if (escHandler.EscapeKeyPressed)
        //        {
        //            RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
        //            checkObjects = null;
        //            return false;
        //        }

        //        if (go.CommandResult() == Result.Success && go.ObjectCount > 0)
        //        {

        //            checkObjects = go.Objects();
        //            RhinoApp.WriteLine($"用户选择了{checkObjects.Length}个对象进行检查");
        //            return true;

        //        }
        //        else
        //        {
        //            checkObjects = RhinoDoc.ActiveDoc.Objects
        //                .Where(o => o != null)
        //                .Select(o => new ObjRef(o))
        //                .ToArray();

        //            RhinoApp.WriteLine($"未手动选择，自动选择全部对象，共 {checkObjects.Length} 个。");
        //            return true;
        //        }

        //    }
        //}


    }
}
