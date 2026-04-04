using Rhino;
//using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
//using Rhino.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace MyChangeTools.commands.FlowAlongMesh2
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
        private readonly Mesh _baseMesh;
        private readonly Mesh _targetMesh;
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

        private readonly bool _boundaryInfer = false;

        private readonly double _boundaryInferOutsideDistanceTol;

        private readonly RbfDeform.RBFLib.RBFDeformer _rbfDeformer;

        private delegate Point3d ProcessPointDelegate(Point3d pt);


        public static Dictionary<Guid,Brep> CacheTransBrep = new Dictionary<Guid,Brep>();


        public GeometryProcessor(
            RhinoDoc doc,
            ObjRef[] objRefs, 
            ObjRef baseMeshRef, 
            ObjRef targetMeshRef, 
            Vector3d limitNormalVector, 
            SelectionOptions selectionOptions)
        {
            _doc = doc;
            _objRefs = objRefs;
            _baseMesh = baseMeshRef.Mesh();
            _targetMesh = targetMeshRef.Mesh();
            _limitNormalVector = limitNormalVector;
            _tolerance = selectionOptions.Tolerance;
            _boundaryInfer = selectionOptions.BoundaryInfer;
            _boundaryInferOutsideDistanceTol = selectionOptions.BoundaryInferOutsideDistanceTol;
            _IsCopy = selectionOptions.IsCopy;
            ProcessPointDelegate ProcessPoint = ProcessPointDefault;
            if (_boundaryInfer)
            {
                List<Vector3d> deltas = new List<Vector3d>();
                List<Point3d> _validSamplePoint = new List<Point3d>();
                List<Point3d> _baseMeshNakedPoints = new List<Point3d>();
                List<Point3d> _boundaryIntersectPoints = new List<Point3d>();
                _boundaryIntersectPoints.AddRange(
                    Mylib.GeometryUtils.GetObjsOutMeshBoundaryPoints(
                        _baseMesh,
                        objRefs.Select(t=>t.Geometry()).ToArray(),
                        selectionOptions.BoundaryInferOffsetCheckDistance,
                        _tolerance
                        )
                 );
                foreach (var spt in _boundaryIntersectPoints)
                {
                    Point3d tpt = ProcessPointDefault(spt);
                    if (tpt.IsValid && tpt != Point3d.Unset)
                    {
                        deltas.Add(tpt - spt);
                        _validSamplePoint.Add(spt);
                    }
                }
                if(_boundaryIntersectPoints.Count > 0)
                {
                    if (selectionOptions.BoundaryInferEdgeSample)
                    {

                        _baseMeshNakedPoints.AddRange(Mylib.GeometryUtils.GetMeshBoundaryPoints(_baseMesh));
                        foreach (var spt in _baseMeshNakedPoints)
                        {
                            Point3d tpt = ProcessPointDefault(spt);
                            if (tpt.IsValid && tpt != Point3d.Unset)
                            {
                                deltas.Add(tpt - spt);
                                _validSamplePoint.Add(spt);
                            }
                        }
                    }
                        if (deltas.Count == _validSamplePoint.Count && _validSamplePoint.Count > 0)
                        {
                            _rbfDeformer = new RbfDeform.RBFLib.RBFDeformerCommon(_validSamplePoint, deltas);
                            RhinoApp.WriteLine($"采集网格边界裸顶点:{_baseMeshNakedPoints.Count},物体与网格边界面相交点:{_boundaryIntersectPoints.Count()},共采集到{_validSamplePoint.Count}点进行边界推算");
                            _rbfDeformer.SolveWeights();
                            ProcessPoint = ProcessPointBoundaryInfer;
                        }
                }
                else
                {
                    RhinoApp.WriteLine("不进行边界推算，物体没有在网格内部的点或者全部在网格外，物体至少需要一部分在网格内。超过网格边界的物体将会缩在网格边界。");
                }

            }

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

        //base on mesh face smooth
        Point3d ProcessPointDefault(Point3d p)
        {
            MeshPoint mp = _baseMesh.ClosestMeshPoint(p, 0.0);
            double[] mpt = mp.T;
            MeshFace mpFace1 = _baseMesh.Faces[mp.FaceIndex];
            int[] mpFace1Vertexesindex = new int[] { mpFace1.A, mpFace1.B, mpFace1.C, mpFace1.D };
            Vector3d[] mpVertexNormals1 =
            mpFace1Vertexesindex.Select(
                i => new Vector3d(_baseMesh.Normals[i].X, _baseMesh.Normals[i].Y, _baseMesh.Normals[i].Z)
                ).ToArray();
            Point3d[] mpFace1Vertexes = mpFace1Vertexesindex.Select(
                i =>
                new Point3d(_baseMesh.Vertices[i].X, _baseMesh.Vertices[i].Y, _baseMesh.Vertices[i].Z)
                ).ToArray();
            Vector3d itNormal1 =
            mpt[0] * mpVertexNormals1[0]
            + mpt[1] * mpVertexNormals1[1]
            + mpt[2] * mpVertexNormals1[2]
            + mpt[3] * mpVertexNormals1[3];
            itNormal1.Unitize();
            Point3d mppoint =
            mpt[0] * mpFace1Vertexes[0]
            + mpt[1] * mpFace1Vertexes[1]
            + mpt[2] * mpFace1Vertexes[2]
            + mpt[3] * mpFace1Vertexes[3];
            double height = (p - mppoint) * 
                (_limitNormalVector!=Vector3d.Unset ?_limitNormalVector : itNormal1);

            //re-get mp . vertexes order may diff between two mesh
            MeshPoint mp2 = _targetMesh.ClosestMeshPoint(_targetMesh.PointAt(mp), 0.0);
            double[] mpt2 = mp2.T;
            MeshFace mpFace2 = _targetMesh.Faces[mp2.FaceIndex];
            int[] mpFace2Vertexesindex = new int[] { mpFace2.A, mpFace2.B, mpFace2.C, mpFace2.D };
            Vector3d[] mpVertexNormals2 =
            mpFace2Vertexesindex.Select(
                i => new Vector3d(_targetMesh.Normals[i].X, _targetMesh.Normals[i].Y, _targetMesh.Normals[i].Z)
                ).ToArray();
            Point3d[] mpFace2Vertexes = mpFace2Vertexesindex.Select(
                i =>
                new Point3d(_targetMesh.Vertices[i].X, _targetMesh.Vertices[i].Y, _targetMesh.Vertices[i].Z)
                ).ToArray();
            Vector3d itNormal2 =
            mpt2[0] * mpVertexNormals2[0]
            + mpt2[1] * mpVertexNormals2[1]
            + mpt2[2] * mpVertexNormals2[2]
            + mpt2[3] * mpVertexNormals2[3];
            itNormal2.Unitize();
            Point3d mppoint2 =
            mpt2[0] * mpFace2Vertexes[0]
            + mpt2[1] * mpFace2Vertexes[1]
            + mpt2[2] * mpFace2Vertexes[2]
            + mpt2[3] * mpFace2Vertexes[3];
            return mppoint2 + height *
                (_limitNormalVector != Vector3d.Unset ? _limitNormalVector : itNormal2);


            //Vector3d[] mpVertexNormals2 =
            //    mpFace1Vertexesindex.Select(
            //        i => new Vector3d(_targetMesh.Normals[i].X, _targetMesh.Normals[i].Y, _targetMesh.Normals[i].Z)
            //        ).ToArray();
            //Point3d[] mpFace2Vertexes =
            //    mpFace1Vertexesindex.Select(
            //        i =>
            //        new Point3d(_targetMesh.Vertices[i].X, _targetMesh.Vertices[i].Y, _targetMesh.Vertices[i].Z)
            //        ).ToArray();
            //Vector3d itNormal2 =
            //mpt[0] * mpVertexNormals2[0]
            //+ mpt[1] * mpVertexNormals2[1]
            //+ mpt[2] * mpVertexNormals2[2]
            //+ mpt[3] * mpVertexNormals2[3];
            //itNormal2.Unitize();
            //Point3d mppoint2 =
            //mpt[0] * mpFace2Vertexes[0]
            //+ mpt[1] * mpFace2Vertexes[1]
            //+ mpt[2] * mpFace2Vertexes[2]
            //+ mpt[3] * mpFace2Vertexes[3];

            //return mppoint2 + height *
            //    (_limitNormalVector != Vector3d.Unset ? _limitNormalVector:itNormal2);
        }

        public Point3d ProcessPointBoundaryInfer(
            Point3d p)
        {
            if (Mylib.GeometryUtils.IsPointOutsideMesh(_baseMesh, p, _boundaryInferOutsideDistanceTol))
            {
                _outsideMeshPointCount++;
                return _rbfDeformer.Evaluate(p);
            }
            else
            {
                _insideMeshPointCount++;
                return ProcessPointDefault(p);
            }

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
            RhinoApp.WriteLine($"_successMorphPointCount: {_successMorphPointCount},_failMorphPointCount{_failMorphPointCount},_outsideMeshPointCount: {_outsideMeshPointCount},_insideMeshPointCount: {_insideMeshPointCount}");
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

