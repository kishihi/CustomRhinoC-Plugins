using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace MyChangeTools.commands.MyFlowAlongNurbsSurface
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

    public class MorphedGeom
    {
        public GeometryBase[] GeometryBases { get; set; }
        public ObjectAttributes Attributes { get; set; }
    }

    public class GeometryProcessor
    {
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private readonly NurbsSurface _baseSurf;
        private readonly NurbsSurface _targetSurf;
        private readonly Vector3d _limitNormalVector;
        private readonly double _tolerance;


        private readonly bool _IsCopy;

        private int _failMorphPointCount = 0;
        private int _successMorphPointCount = 0;
        private int _outsideMeshPointCount = 0;
        private int _insideMeshPointCount = 0;

        private readonly MyPointFieldMorph _morph;

        private readonly Mylib.MyGeomMorph _myGeomMorph;

        private readonly bool _useCustomMorph = false;

        private delegate Point3d ProcessPointDelegate(Point3d pt);


        public GeometryProcessor(
            RhinoDoc doc,
            ObjRef[] objRefs,
            Surface baseSurf, 
            Surface targetSurf,
            Vector3d limitNormalVector,
            SelectionOptions selectionOptions)
        {
            _doc = doc;
            _objRefs = objRefs;
            _limitNormalVector = limitNormalVector;
            _tolerance = selectionOptions.Tolerance;
            _IsCopy = selectionOptions.IsCopy;
            _baseSurf = baseSurf.ToNurbsSurface();
            _targetSurf = targetSurf.ToNurbsSurface();
            //reparmet
            _baseSurf.SetDomain(0, new Interval(0, 100));
            _baseSurf.SetDomain(1, new Interval(0, 100));
            _targetSurf.SetDomain(0, new Interval(0, 100));
            _targetSurf.SetDomain(1, new Interval(0, 100));
            _limitNormalVector = limitNormalVector;
            _tolerance = selectionOptions.Tolerance;


            ProcessPointDelegate ProcessPoint = ProcessPointDefault;
            Func<Point3d, Point3d> LastProcessPoint = pt =>
            {
                var newpt = ProcessPoint(pt);
                if (newpt == Point3d.Unset)
                    _failMorphPointCount++;
                else
                    _successMorphPointCount++;
                return newpt;
            };

            if (!selectionOptions.UseCustomMorph)
            {
                _morph = new MyPointFieldMorph(
                LastProcessPoint,
                selectionOptions.Tolerance,
                selectionOptions.PreserveStructure,
                selectionOptions.QuickPreview
                );
                _useCustomMorph = false;
            }
            else
            {
                _myGeomMorph = new Mylib.MyGeomMorph(
                    doc,
                    LastProcessPoint,
                    selectionOptions.Tolerance,
                    selectionOptions.RebuildFaceUCount,
                    selectionOptions.RebuildFaceVCount,
                    selectionOptions.RebuildCurveCount,
                    selectionOptions.ShrinkSurfaceToEdge
                    );
                _useCustomMorph = true;
            }
        }

        public Point3d ProcessPointDefault(
            Point3d testpt)
        {
            if(_baseSurf.ClosestPoint(testpt,out double u1,out double v1))
            {
                if(_baseSurf.UVNDirectionsAt(
                    u1,
                    v1,
                    out Vector3d uDir1,
                    out Vector3d vDir1,
                    out Vector3d nDir1
                ))
                {
                    uDir1.Unitize();
                    vDir1.Unitize();
                    nDir1.Unitize();

                    Point3d cp1 = _baseSurf.PointAt(u1, v1);
                    Vector3d va = testpt - cp1;

                    double uj1 = va * uDir1;
                    double vj1 = va * vDir1;
                    double nj1 = va * nDir1;

                    Point3d cp2 = _targetSurf.PointAt(u1, v1);

                    if(_targetSurf.UVNDirectionsAt(
                        u1,v1,
                        out Vector3d uDir2,
                        out Vector3d vDir2,
                        out Vector3d nDir2
                        )
                     )
                    {
                        Vector3d cp2N = uj1*uDir2 + vj1*vDir2 + nj1*nDir2;
                        return cp2 + cp2N;
                    }

                }
            }
            return Point3d.Unset;
        }



        public (List<MorphedGeom>, List<ObjRef>) Process()
        {


            //计时开始
            var sw = Stopwatch.StartNew();

            List<MorphedGeom> successProcessObjs = new List<MorphedGeom>();
            List<ObjRef> failProcessObjRefs = new List<ObjRef>();

            object mergeLock = new object();

            int successCount = 0;
            int failCount = 0;

            Parallel.ForEach(
                _objRefs,

                // 每线程初始化
                () => new List<MorphedGeom>(8),

                // 并行处理
                (objRef, loopState, localList) =>
                {
                    try
                    {
                        GeometryBase geom = objRef.Geometry();
                        if (geom == null)
                            return localList;

                        geom = geom.Duplicate();

                        if (geom.ObjectType == Rhino.DocObjects.ObjectType.Extrusion)
                        {
                            var eo = geom as Extrusion;
                            geom = eo.ToBrep() as GeometryBase;
                        }

                        if (geom.ObjectType == Rhino.DocObjects.ObjectType.Brep)
                        {
                            var bo = geom as Brep;
                            bo.Standardize();
                            bo.Compact();
                            geom = bo as GeometryBase;
                        }

                        //默认使用Rhino自带的变形方法
                        if (!_useCustomMorph)
                        {
                            if (_morph.Morph(geom))
                            {
                                localList.Add(new MorphedGeom
                                {
                                    GeometryBases = new[] { geom },
                                    Attributes = objRef.Object().Attributes.Duplicate()
                                });
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                lock (mergeLock) failProcessObjRefs.Add(objRef);
                                Interlocked.Increment(ref failCount);
                            }
                        }
                        else
                        {
                            var morphed = _myGeomMorph.MorphGeometry(geom, out GeometryBase[] newGeoms);
                            if (morphed)
                            {
                                localList.Add(new MorphedGeom
                                {
                                    GeometryBases = newGeoms,
                                    Attributes = objRef.Object().Attributes.Duplicate()
                                });
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                lock (mergeLock) failProcessObjRefs.Add(objRef);
                                Interlocked.Increment(ref failCount);

                            }

                        }
                    }
                    catch
                    {
                        // RhinoApp.WriteLine("对象处理出错: " + ex.Message);
                    }

                    return localList;
                },

                // 合并线程结果
                localList =>
                {
                    lock (mergeLock)
                        successProcessObjs.AddRange(localList);
                });


            sw.Stop();
            RhinoApp.WriteLine("成功: " + successCount);
            RhinoApp.WriteLine($"失败: {failCount}, 失败变形的对象将会添加到选择集中");
            RhinoApp.WriteLine(
                $"_successMorphPointCount: {_successMorphPointCount}," +
                $"_failMorphPointCount: {_failMorphPointCount}," +
                $"_outsideMeshPointCount: {_outsideMeshPointCount}," +
                $"_insideMeshPointCount: {_insideMeshPointCount}");
            RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");
            return (successProcessObjs, failProcessObjRefs);
        }

        public void ApplyResultToDoc((List<MorphedGeom> successProcessObjs, List<ObjRef> failProcessObjRefs) result)
        {
            var successProcessObjs = result.successProcessObjs;
            var failProcessObjRefs = result.failProcessObjRefs;
            uint undo = _doc.BeginUndoRecord("FlowAlongMesh");
            try
            {
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
            }
            finally
            {
                _doc.EndUndoRecord(undo);
                _doc.Views.Redraw();
            }
        }


    }
}
