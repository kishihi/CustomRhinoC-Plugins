using MyChangeTools.commands.RbfDeform.RBFLib;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyChangeTools.commands.DualSurfaceMapping
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

    internal class MorphedGeom
    {
        public GeometryBase[] GeometryBases { get; set; }
        public ObjectAttributes Attributes { get; set; }
    }


    public class GeometryProcessor
    {


        //特别参数
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;//需要变换的物体
        private readonly Brep _BrepA1;
        private readonly Brep _BrepA2;
        private readonly Brep _BrepB1;
        private readonly Brep _BrepB2;
        private readonly Vector3d _projectionDirection; //投影方向

        private long _failMorphPointCount;
        private long _successMorphPointCount;
        private readonly Mylib.MyGeomMorph _myGeomMorph;
        private readonly bool _useCustomMorph = false;
        private readonly bool _IsCopy;
        private readonly MyPointFieldMorph _morph;

        private readonly double _Tolerance;

        public GeometryProcessor(
            RhinoDoc doc,
            ObjRef[] objRefs,
            Brep BrepA1,
            Brep BrepA2,
            Brep BrepB1,
            Brep BrepB2,
            Vector3d projectionDirection,
            SelectionOptions selectionOptions)
        {
            _doc = doc;
            _objRefs = objRefs;
            _projectionDirection = projectionDirection;
            _BrepA1 = BrepA1;
            _BrepA2 = BrepA2;
            _BrepB1 = BrepB1;
            _BrepB2 = BrepB2;
            _Tolerance = selectionOptions.Tolerance;

            if (selectionOptions.IsUseRbfFit)
            {
                Point3d processPointSample(Point3d pt)
                {
                    Point3d newpt = Point3d.Unset;
                    if (selectionOptions.IsB2NormalBase)
                        ProcessPointAtB2NormalBase(pt, out newpt);
                    else
                        ProcessPointAtOneVector(pt, out newpt);
                    return newpt;
                }
                var obboxs = _objRefs.Select(f => f.Geometry().GetBoundingBox(false));
                var samps = obboxs
                .SelectMany(b => Mylib.GeometryUtils.SampleBoundingBoxSimple(b))
                .ToList();
                var src = new Point3d[samps.Count];
                var dst = new Point3d[samps.Count];
                var valid = new bool[samps.Count];
                Parallel.For(0, samps.Count, i =>
                {
                    Point3d dt = processPointSample(samps[i]);
                    if (dt != Point3d.Unset)
                    {
                        src[i] = samps[i];
                        dst[i] = dt;
                        valid[i] = true;
                    }
                });
                var sampSrcPts = new List<Point3d>();
                var sampDesPts = new List<Point3d>();

                for (int i = 0; i < samps.Count; i++)
                {
                    if (valid[i])
                    {
                        sampSrcPts.Add(src[i]);
                        sampDesPts.Add(dst[i]);
                    }
                }

                var deltas = sampSrcPts.Zip(sampDesPts, (srcc, dstt) => dstt - srcc).ToList();

                var _rbfDeformer = new RBFDeformerCommon(sampSrcPts, deltas);

                RhinoApp.WriteLine($"采集到{sampSrcPts.Count}点进行RBF近似拟合");

                _rbfDeformer.SolveWeights();

                RhinoApp.WriteLine($"Length {_rbfDeformer.Wx.Length} WxMax: {_rbfDeformer.Wx.Max()},WxMin:{_rbfDeformer.Wx.Min()}");
                RhinoApp.WriteLine($"Length {_rbfDeformer.Wy.Length} WyMax: {_rbfDeformer.Wy.Max()},WyMin:{_rbfDeformer.Wy.Min()}");
                RhinoApp.WriteLine($"Length {_rbfDeformer.Wz.Length} WzMax: {_rbfDeformer.Wz.Max()},WzMin:{_rbfDeformer.Wz.Min()}");


                Func<Point3d, Point3d> processPointRbfFit = pt =>
                {
                    Point3d newpt = Point3d.Unset;
                    if (selectionOptions.IsB2NormalBase)
                        ProcessPointAtB2NormalBase(pt, out newpt);
                    else
                        ProcessPointAtOneVector(pt, out newpt);
                    if (newpt == Point3d.Unset)
                    {
                        _failMorphPointCount++;
                        newpt = _rbfDeformer.Evaluate(pt);
                    }
                    return newpt;
                };
                // use custommorph
                if (selectionOptions.UseCustomMorph)
                {
                    _myGeomMorph = new Mylib.MyGeomMorph(
                        doc,
                        processPointRbfFit,
                        selectionOptions.Tolerance,
                        selectionOptions.RebuildFaceUCount,
                        selectionOptions.RebuildFaceVCount,
                        selectionOptions.RebuildCurveCount,
                        selectionOptions.ShrinkSurfaceToEdge
                    );
                    _useCustomMorph = true;
                }
                else
                {
                    _morph = new MyPointFieldMorph(
                        processPointRbfFit,
                        selectionOptions.Tolerance,
                        selectionOptions.PreserveStructure,
                        selectionOptions.QuickPreview
                    );
                    _useCustomMorph = false;
                }
            }
            else

            {
                Func<Point3d, Point3d> processPointRealPointTranForm = pt =>
                {
                    bool ok = false;
                    Point3d newpt = Point3d.Unset;
                    if (selectionOptions.IsB2NormalBase)
                        ok = ProcessPointAtB2NormalBase(pt, out newpt);
                    else
                        ok = ProcessPointAtOneVector(pt, out newpt);
                    if (!ok)
                    {
                        _failMorphPointCount++;

                    }
                    else
                    {
                        _successMorphPointCount++;
                    }
                    return newpt;
                };



                // use custommorph
                if (selectionOptions.UseCustomMorph)
                {
                    _myGeomMorph = new Mylib.MyGeomMorph(
                        doc,
                        processPointRealPointTranForm,
                        selectionOptions.Tolerance,
                        selectionOptions.RebuildFaceUCount,
                        selectionOptions.RebuildFaceVCount,
                        selectionOptions.RebuildCurveCount,
                        selectionOptions.ShrinkSurfaceToEdge
                    );
                    _useCustomMorph = true;
                }
                else
                {
                    _morph = new MyPointFieldMorph(
                        processPointRealPointTranForm,
                        selectionOptions.Tolerance,
                        selectionOptions.PreserveStructure,
                        selectionOptions.QuickPreview
                    );
                    _useCustomMorph = false;
                }
            }

            _IsCopy = selectionOptions.IsCopy;



        }


        public Result Process()
        {

            //计时开始
            var sw = Stopwatch.StartNew();


            List<MorphedGeom> successProcessObjs = new List<MorphedGeom>();
            List<ObjRef> failProcessObjRefs = new List<ObjRef>();

            object mergeLock = new object();

            int successCount = 0;
            int failCount = 0;

            List<(GeometryBase geom, ObjectAttributes attr, ObjRef objRef)> workItems
               = new List<(GeometryBase, ObjectAttributes, ObjRef)>();

            foreach (var objRef in _objRefs)
            {
                var geom = objRef.Geometry();
                if (geom == null) continue;

                geom = geom.Duplicate();

                if (geom.ObjectType == ObjectType.Extrusion)
                    geom = ((Extrusion)geom).ToBrep();

                var attr = objRef.Object().Attributes.Duplicate();

                workItems.Add((geom, attr, objRef));
            }

            Parallel.ForEach(
                workItems,

                // 每线程初始化
                () => new List<MorphedGeom>(8),

                // 并行处理
                (item, loopState, localList) =>
                {
                    try
                    {
                        var geom = item.geom;
                        if (geom == null)
                            return localList;

                        if (geom.ObjectType == Rhino.DocObjects.ObjectType.Extrusion)
                        {
                            var eo = geom as Extrusion;
                            geom = eo.ToBrep() as GeometryBase;
                        }

                        //默认变形
                        if (!_useCustomMorph)
                        {
                            if (_morph.Morph(geom))
                            {
                                localList.Add(new MorphedGeom
                                {
                                    GeometryBases = new[] { geom },
                                    Attributes = item.attr
                                });

                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                lock (mergeLock) failProcessObjRefs.Add(item.objRef);
                                Interlocked.Increment(ref failCount);
                            }
                        }
                        //自定义的变形类
                        else
                        {
                            var morphed = _myGeomMorph.MorphGeometry(geom, out GeometryBase[] newGeoms);
                            if (morphed)
                            {
                                localList.Add(new MorphedGeom
                                {
                                    GeometryBases = newGeoms,
                                    Attributes = item.attr
                                });
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                lock (mergeLock) failProcessObjRefs.Add(item.objRef);
                                Interlocked.Increment(ref failCount);

                            }

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

                _doc.Views.RedrawEnabled = false;
                List<Guid> newIds = new List<Guid>(successProcessObjs.Count);

                foreach (var mg in successProcessObjs)
                {
                    foreach (var g in mg.GeometryBases)
                    {

                        //添加对象同时把属性加过去
                        Guid id = _doc.Objects.Add(g, mg.Attributes.Duplicate());

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

                foreach (ObjRef fo in failProcessObjRefs)
                    _doc.Objects.Select(fo.ObjectId);

                sw.Stop();


                RhinoApp.WriteLine("成功: " + successCount);
                RhinoApp.WriteLine($"失败: {failCount}, 失败变形的对象将会添加到选择集中");
                RhinoApp.WriteLine($"成功MorphPoint:{_successMorphPointCount}, 失败或RBFFITMorphPoint: {_failMorphPointCount}.");
                RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");


                _doc.Views.RedrawEnabled = true;
                _doc.Views.Redraw();


            }));

            return Result.Success;
        }



        // 基函数: 点变化
        private bool ProcessPointAtOneVector(Point3d pt, out Point3d newPt)
        {
            newPt = Point3d.Unset;

            try
            {
                var dir = _projectionDirection;
                if (!dir.IsValid || dir.IsZero)
                    return false;

                // 1️⃣ 求交点：pt 方向上与四个面相交
                var pa1 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepA1, pt, dir);
                var pa2 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepA2, pt, dir);
                var pb1 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepB1, pt, dir);
                var pb2 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepB2, pt, dir);

                if (pa1 == Point3d.Unset || pa2 == Point3d.Unset || pb1 == Point3d.Unset || pb2 == Point3d.Unset)
                    return false;

                // 2️⃣ 在旧空间(A1~B1)上计算点相对位置比例 ratio
                var vAB_old = pb1 - pa1;
                double denom = vAB_old * dir;

                //if (Math.Abs(denom) < _Tolerance)
                //return false;

                double s = (pt - pa1) * dir;
                double ratio = s / denom;

                // 只允许在 0~1 之间（避免射线穿出空间）
                //if (ratio < -1e-6 || ratio > 1.0 + 1e-6)
                //    return false;

                // 3️⃣ 在新空间(A2~B2)上按相同 ratio 插值
                var vAB_new = pb2 - pa2;
                newPt = pa2 + ratio * vAB_new;

                if (!newPt.IsValid || newPt == Point3d.Unset)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }


        private bool ProcessPointAtB2NormalBase(Point3d pt, out Point3d newPt)
        {
            newPt = Point3d.Unset;

            try
            {

                var dir = _projectionDirection;
                if (!dir.IsValid || dir.IsZero)
                    return false;

                // 1️⃣ 求交点与四个面相交
                var pa1 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepA1, pt, dir);
                var pb1 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepB1, pt, dir);
                var pb2 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepB2, pt, dir);
                if (!_BrepB2.ClosestPoint(pb2, out _, out _, out _, out _, 0,
                    out Vector3d pb2AtB2Nomal))
                {
                    return false;
                }
                var pa2 = Mylib.GeometryUtils.IntersectSurfaceAlongVector(_BrepA2, pb2, pb2AtB2Nomal);

                if (pa1 == Point3d.Unset || pa2 == Point3d.Unset || pb1 == Point3d.Unset || pb2 == Point3d.Unset)
                    return false;

                // 2️⃣ 在旧空间(A1~B1)上计算点相对位置比例 ratio
                var vAB_old = pb1 - pa1;
                double denom = vAB_old * dir;
                //if (Math.Abs(denom) < _Tolerance)
                //return false;

                double s = (pt - pa1) * dir;
                double ratio = s / denom;

                // 3️⃣ 在新空间(A2~B2)上按相同 ratio 插值
                var vAB_new = pb2 - pa2;// Norlmal , equal pb2AtB2Nomal
                newPt = pa2 + ratio * vAB_new;

                if (!newPt.IsValid || newPt == Point3d.Unset)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}