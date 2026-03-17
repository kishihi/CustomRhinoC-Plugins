using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MyChangeTools.commands.RbfDeformGui
{

    internal class GeometryProcessorSync
    {
        private readonly RBFLib.Deform _deform;
        private readonly List<Vector3d> _moveVectors;
        private readonly double _Tolerance;
        private readonly bool _IsCopy;
        private readonly MyPointFieldMorph _morph;
        private readonly RhinoDoc _doc;
        private readonly ObjRef[] _objRefs;
        private long _failMorphPointCount;
        private long _successMorphPointCount;

        private readonly Mylib.MyGeomMorph _myGeomMorph;

        private readonly bool _useCustomMorph = false;


        public GeometryProcessorSync(
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
                v.Unitize();
                _moveVectors[i] = v;
            }

            Func<Point3d, Point3d> processPoint = pt =>
            {
                var ok = _deform.MorphPoint(pt, out Point3d newpt);
                if (!ok)
                {
                    _failMorphPointCount++;

                }
                else
                {
                    _successMorphPointCount++;
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
            if (_configs.UseCustomMorph)
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

            _IsCopy = _configs.IsCopy;
        }

        public Result ProcessSync()
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

            foreach (var item in workItems)
            {
                try
                {
                    var geom = item.geom;
                    if (geom == null)
                        continue;

                    if (geom.ObjectType == Rhino.DocObjects.ObjectType.Extrusion)
                    {
                        var eo = geom as Extrusion;
                        geom = eo.ToBrep() as GeometryBase;
                    }

                    // 默认变形
                    if (!_useCustomMorph)
                    {
                        if (_morph.Morph(geom))
                        {
                            successProcessObjs.Add(new MorphedGeom
                            {
                                GeometryBases = new[] { geom },
                                Attributes = item.attr
                            });
                            successCount++;
                        }
                        else
                        {
                            failProcessObjRefs.Add(item.objRef);
                            failCount++;
                        }
                    }
                    // 自定义变形
                    else
                    {
                        var morphed = _myGeomMorph.MorphGeometry(geom, out GeometryBase[] newGeoms);
                        if (morphed)
                        {
                            successProcessObjs.Add(new MorphedGeom
                            {
                                GeometryBases = newGeoms,
                                Attributes = item.attr
                            });
                            successCount++;
                        }
                        else
                        {
                            failProcessObjRefs.Add(item.objRef);
                            failCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine("对象处理出错: " + ex.Message);
                }
            }

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
            RhinoApp.WriteLine($"成功MorphPoint:{_successMorphPointCount}, 失败MorphPoint: {_failMorphPointCount}.");
            RhinoApp.WriteLine("执行时间: " + sw.ElapsedMilliseconds + " ms");

            return Result.Success;
        }


    }
}
