using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;


namespace MyChangeTools.commands.FlowAlongMesh
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

    public class GeometryProcessor
    {
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private readonly Mesh _baseMesh;
        private readonly Mesh _targetMesh;
        private readonly Vector3d _limitNormalVector;
        private readonly double _Tolerance;
        private readonly SubD _targetSubD;
        private readonly Brep _targetBrep;

        private readonly bool _IsCopy;

        private int _failMorphPointCount = 0;
        private int _successMorphPointCount = 0;


        private readonly MyPointFieldMorph _morph;
        public GeometryProcessor(RhinoDoc doc, ObjRef[] objRefs, Mesh baseMesh, Mesh targetMesh, Vector3d limitNormalVector, SelectionOptions selectionOptions)
        {
            _doc = doc;
            _objRefs = objRefs;
            _baseMesh = baseMesh;
            _targetMesh = targetMesh;
            _limitNormalVector = limitNormalVector;
            _Tolerance = selectionOptions.Tolerance;

            _morph = new MyPointFieldMorph(pt =>
            {
                var newpt = ProcessPoint(pt);
                if (newpt == Point3d.Unset)
                    _failMorphPointCount++;
                else
                    _successMorphPointCount++;
                return ProcessPoint(pt);
            },
            selectionOptions.Tolerance,
            selectionOptions.PreserveStructure,
            selectionOptions.QuickPreview
            );

            _IsCopy = selectionOptions.IsCopy;

            _targetSubD = SubD.CreateFromMesh(targetMesh);

            _targetBrep = _targetSubD.ToBrep(SubDToBrepOptions.Default);

        }


        public Point3d ProcessPoint(
            Point3d p)
        {
            MeshPoint mp = _baseMesh.ClosestMeshPoint(p, 0.0);

            Point3d q = mp.Point;

            Vector3d n;

            if (_limitNormalVector == Vector3d.Unset)
            {
                n = _baseMesh.NormalAt(mp);
            }
            else
            {
                n = _limitNormalVector;
            }

            double height = (p - q) * n;

            Point3d q2 = _targetMesh.PointAt(mp);
            Vector3d n2;

            //Point3d q3
            //_targetBrep.ClosestPoint()
            if (_targetBrep.ClosestPoint(
                    q2,
                    out Point3d closestPoint,
                    out ComponentIndex ci,
                    out double s,
                    out double t,
                    0,
                    out Vector3d normal
                )
                )
            {
                q2 = closestPoint;
                n2 = normal;
            }

            else

            {
                if (_limitNormalVector == Vector3d.Unset)
                {
                    n2 = _targetMesh.NormalAt(mp);
                }
                else
                {
                    n2 = _limitNormalVector;
                }
            }
            return q2 + n2 * height;

        }

        public Result Process()
        {
            if (_baseMesh.Vertices.Count != _targetMesh.Vertices.Count || _baseMesh.Faces.Count != _targetMesh.Faces.Count)
            {
                RhinoApp.WriteLine("基准网格和目标网格的顶点和面数需要相同,否则造成未预料的变形结果. 请重新选择");
                return Result.Failure;
            }

            //计时开始
            var sw = Stopwatch.StartNew();

            List<GeometryBase> successProcessObjs = new List<GeometryBase>();
            List<ObjRef> failProcessObjRefs = new List<ObjRef>();

            object mergeLock = new object();

            int successCount = 0;
            int failCount = 0;

            Parallel.ForEach(
                _objRefs,

                // 每线程初始化
                () => new List<GeometryBase>(8),

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

                        //直接morph尝试变换
                        if (_morph.Morph(geom))
                        {
                            localList.Add(geom);
                            Interlocked.Increment(ref successCount);
                        }
                        else
                        {
                            lock (mergeLock) failProcessObjRefs.Add(objRef);
                            Interlocked.Increment(ref failCount);
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
                List<Guid> newIds = new List<Guid>(successProcessObjs.Count);

                foreach (GeometryBase g in successProcessObjs)
                {

                    Guid id = _doc.Objects.Add(g);

                    if (id != Guid.Empty)
                        newIds.Add(id);
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

                sw.Stop();
                _doc.Views.Redraw();

                RhinoApp.WriteLine("成功: " + successCount);
                RhinoApp.WriteLine($"失败: {failCount}, 失败变形的对象将会添加到选择集中");
                RhinoApp.WriteLine($"_successMorphPointCount: {_successMorphPointCount},_failMorphPointCount{_failMorphPointCount}");

                RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");

                foreach (ObjRef fo in failProcessObjRefs)
                    _doc.Objects.Select(fo.ObjectId);


            }));


            return Result.Success;
        }


    }
}
