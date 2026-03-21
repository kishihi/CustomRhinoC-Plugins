using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MyChangeTools.commands.RbfDeformGui
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

    internal class GeometryProcessor
    {
        private readonly RBFLib.Deform _deform;
        private readonly List<Vector3d> _moveVectors;
        private readonly double _Tolerance;
        private readonly MyPointFieldMorph _morph;
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private long _failMorphPointCount;
        private long _successMorphPointCount;

        private readonly Mylib.MyGeomMorph _myGeomMorph;

        private readonly bool _useCustomMorph = false;
        


        public GeometryProcessor(
            RhinoDoc doc,
            ObjRef[] objRefs,
            ObjRef[] baseObjRfs,
            ObjRef[] targetObjRfs,
            ObjRef[] limitedObjRfs,
            List<Vector3d> MoveVectors,
            Config _configs)
        {
            _doc = doc;
            _objRefs = objRefs;
            _moveVectors = MoveVectors;
            _Tolerance = _configs.Tolerance;
            _deform = new RBFLib.Deform(baseObjRfs, targetObjRfs, limitedObjRfs, _configs);

            // 预先单位化向量（避免百万次Unitize）
            for (int i = 0; i < _moveVectors.Count; i++)
            {
                var v = _moveVectors[i];
                if (v != Vector3d.Unset && v.IsValid)
                {
                    v.Unitize();
                    _moveVectors[i] = v;
                }
            }

            Func<Point3d, Point3d> processPoint = pt =>
            {
                var ok = _deform.MorphPoint(pt, out Point3d newpt);
                if (!ok)
                {
                    // _failMorphPointCount++;
                    Interlocked.Increment(ref _failMorphPointCount);

                }
                else
                {
                    // _successMorphPointCount++;
                    Interlocked.Increment(ref _successMorphPointCount);
                }
                //把最终的移动限制在几个向量方向上；
                if (_moveVectors.Count > 0)
                {
                    var delta = newpt - pt;
                    var resultDelta = Vector3d.Zero;
                    foreach (var v in _moveVectors)
                    {
                        Vector3d mv = Vector3d.Multiply(delta, v) * v;
                        resultDelta += mv;
                        
                    }
                    newpt = pt + resultDelta;
                }
                return newpt;
            };


            // use custommorph
            if (_configs.IsUseCustomMorph)
            {
                _myGeomMorph = new Mylib.MyGeomMorph(
                    doc,
                    processPoint,
                    _configs.Tolerance,
                    _configs.MyGeomMorphConfig.RebuildFaceUCount,
                    _configs.MyGeomMorphConfig.RebuildFaceVCount,
                    _configs.MyGeomMorphConfig.RebuildCurveCount,
                    _configs.MyGeomMorphConfig.ShrinkSurfaceToEdge
                );
                _useCustomMorph = true;
            }
            else
            {
                _morph = new MyPointFieldMorph(
                    processPoint,
                    _configs.Tolerance,
                    _configs.SpaceMorphConfig.PreserveStructure,
                    _configs.SpaceMorphConfig.QuickPreview
                );
                _useCustomMorph = false;
            }

        }

        public (List<MorphedGeom>,List<ObjRef>) Process(CancellationToken token)
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
                    token.ThrowIfCancellationRequested();
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

            sw.Stop();

            RhinoApp.WriteLine("成功: " + successCount);
            RhinoApp.WriteLine($"失败: {failCount}, 失败变形的对象将会添加到选择集中");
            RhinoApp.WriteLine($"成功MorphPoint:{_successMorphPointCount}, 失败MorphPoint: {_failMorphPointCount}.");
            RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");

            
            return (successProcessObjs,failProcessObjRefs);

        }


    }
}
