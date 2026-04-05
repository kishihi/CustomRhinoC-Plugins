using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui.RBFLib
{

    internal partial class Deform
    {

        private readonly List<Point3d> _srcPoints = new List<Point3d>();
        private readonly List<Vector3d> _deltas = new List<Vector3d>();
        private readonly RBFDeformer _rbfDeformer;

        private readonly Config _config;

        static readonly public Dictionary<int, string> SurfaceMappingMethod = new Dictionary<int, string>
        {
            {0, "UVCorrespond"},
            {1, "XAxis"},
            {2, "YAxis"},
            {3, "ZAxis"},
            {4,"Normal"},
            {5,"TwoPtDefine"}
        };

        public Deform(
            Rhino.DocObjects.ObjRef[] baseObjRfs,
            Rhino.DocObjects.ObjRef[] targetObjRfs,
            Rhino.DocObjects.ObjRef[] limitedObjRfs,
            Config _config)
        {
            // _rbfDeformer
            if (baseObjRfs.Length == 0 || baseObjRfs.Length != targetObjRfs.Length)
            {
                RhinoApp.WriteLine("baseObjRfs Count Must equal targetObjRfs Count , ant at least one");
                throw new InvalidOperationException("baseObjRfs Count Must equal targetObjRfs Count , ant at least one");
            }

            this._config = _config;

            // set shape matching constraints
            var ok1 = SampleMatchObj(baseObjRfs, targetObjRfs);

            // set limited constraints
            var ok2 = SampleLimitedObj(limitedObjRfs);


            // 如果所有曲线对和受限对象都成功采样到点，则构造 RBFDeformer
            if (ok1 && ok2 && _srcPoints.Count > 0)
            {
                if (!_config.RBFConfig.RBFAddLinearSystem)
                    _rbfDeformer = new RBFDeformerCommon(_srcPoints, _deltas);
                else
                    _rbfDeformer = new RBFDeformerWithLinearSystem(_srcPoints, _deltas);
                if (_config.RBFConfig.InfectRadius > 0)
                {
                    if (_config.RBFConfig.PhiFunctionID == 1)
                    {
                        RhinoApp.WriteLine($"使用CSRBFW2，影响半径 R = {_config.RBFConfig.InfectRadius}");
                        double R = _config.RBFConfig.InfectRadius;
                        _rbfDeformer.Phi = r => RBFPhiFunctions.CSRBFW2(r, R);
                    }
                    else if (_config.RBFConfig.PhiFunctionID == 2)
                    {
                        RhinoApp.WriteLine($"使用GAUSS，影响半径 R = {_config.RBFConfig.InfectRadius}");
                        double R = _config.RBFConfig.InfectRadius;
                        _rbfDeformer.Phi = r => RBFPhiFunctions.GAUSS(r, R);
                    }
                }
                if (_config.RBFConfig.PhiFunctionID == 0)
                {
                    _rbfDeformer.Phi = r => RBFPhiFunctions.TPS(r);
                }

                if (_rbfDeformer.Phi != null)
                {
                    _rbfDeformer.SolveWeights();

                    RhinoApp.WriteLine($"Length {_rbfDeformer.Wx.Length} WxMax: {_rbfDeformer.Wx.Max()},WxMin:{_rbfDeformer.Wx.Min()}");
                    RhinoApp.WriteLine($"Length {_rbfDeformer.Wy.Length} WyMax: {_rbfDeformer.Wy.Max()},WyMin:{_rbfDeformer.Wy.Min()}");
                    RhinoApp.WriteLine($"Length {_rbfDeformer.Wz.Length} WzMax: {_rbfDeformer.Wz.Max()},WzMin:{_rbfDeformer.Wz.Min()}");
                }
                else
                {
                    _rbfDeformer = null;
                    RhinoApp.WriteLine("Phi函数设置错误,请注意半径,Guass和csrbfw2需要半径");
                    throw new ArgumentException("Phi函数设置错误,请注意半径,Guass和csrbfw2需要半径");
                }

            }
            else
            {
                _rbfDeformer = null;
                RhinoApp.WriteLine("有对象没有成功采样到点，无法构造 RBFDeformer, 请检查输入的 baseObjRfs, targetObjRfs, limitedObjRfs 是否正确，或者调整采样选项");
                throw new AggregateException("_rbfDeformer null !");
            }

        }


        bool SampleMatchObj(Rhino.DocObjects.ObjRef[] baseObjRfs, Rhino.DocObjects.ObjRef[] targetObjRfs)
        {

            List<bool> allSampleBools = new List<bool>();
            //shape matching 
            var basePts = new List<Point3d>();
            var targetPts = new List<Point3d>();
            for (int i = 0; i < baseObjRfs.Length; i++)
            {
                var baseobj = baseObjRfs[i].Geometry();
                var targetobj = targetObjRfs[i].Geometry();
                if (baseobj.ObjectType != targetobj.ObjectType)
                {
                    RhinoApp.WriteLine("Every baseObj ObjectType Must equal to targetObj ObjectType");
                    throw new InvalidOperationException("Every baseObj ObjectType Must equal to targetObj ObjectType");
                }

                //如果是挤出物体，先转换为曲面再采样
                if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Extrusion)
                {
                    baseobj = (baseobj as Extrusion).ToBrep();
                    targetobj = (targetobj as Extrusion).ToBrep();
                }

                if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Curve)
                {
                    var baseCurve = baseObjRfs[i].Curve();
                    var targetCurve = targetObjRfs[i].Curve();
                    int count = (int)Math.Ceiling(Math.Min(baseCurve.GetLength(), targetCurve.GetLength()) / _config.SampleConfig.CurveSampleDistance + 1);
                    int successCount;
                    if (!_config.SampleConfig.CurveSampleByParameter)
                        allSampleBools.Add(AddCurveMapByClosestPoint(baseCurve, targetCurve, count, out successCount));
                    else
                        allSampleBools.Add(AddCurveMapByParameter(baseCurve, targetCurve, count, out successCount));
                    if (successCount > 0)
                        RhinoApp.WriteLine($"曲线对{i + 1} 成功采样了{successCount}个点");
                    else
                        RhinoApp.WriteLine($"曲线对{i + 1} 没有采样到点");
                }
                else if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Mesh)
                {
                    var baseMesh = baseObjRfs[i].Mesh();
                    var targetMesh = targetObjRfs[i].Mesh();
                    var ok = false;
                    int successCount;
                    if (!_config.SampleConfig.MatchMeshByCoordinate)
                        ok = AddMeshMapByVertexOrder(baseMesh, targetMesh, out successCount);
                    else
                        ok = AddMeshMapByCoordinate(baseMesh, targetMesh, out successCount);
                    if (ok)
                    {
                        RhinoApp.WriteLine($"Mesh对{i + 1} 成功采样了{successCount}个点");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Mesh对{i + 1} 没有采样到点");
                    }
                    allSampleBools.Add(ok);
                }

                else if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Point)
                {
                    // 点对象转为点位置Point3d
                    basePts.Add(((Point)baseobj).Location);
                    targetPts.Add(((Point)targetobj).Location);

                }
                else if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Surface)
                {

                    var baseSrf = baseObjRfs[i].Surface().ToNurbsSurface();
                    var targetSrf = targetObjRfs[i].Surface().ToNurbsSurface();
                    int successCount = 0;
                    var ok = false;
                    switch (_config.SampleConfig.SurfaceSampleMethod)
                    {
                        case 0:
                            //uv count必须一样，才能一一对应采样点对计算增量
                            if (baseSrf.Points.CountU != targetSrf.Points.CountU || baseSrf.Points.CountV != targetSrf.Points.CountV)
                            {
                                RhinoApp.WriteLine($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                                throw new InvalidOperationException($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                            }
                            ok = AddSurfaceMapByUVCorrespond(baseSrf, targetSrf, out successCount);
                            break;
                        case 1:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, Vector3d.XAxis, out successCount);
                            break;
                        case 2:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, Vector3d.YAxis, out successCount);
                            break;
                        case 3:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, Vector3d.ZAxis, out successCount);
                            break;
                        case 4:
                            ok = AddSurfaceMapByNormalDirection(baseSrf, targetSrf, out successCount);
                            break;
                        case 5:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, _config.SampleConfig.SurfaceSampleDirection, out successCount);
                            break;
                        default:
                            break;
                    }
                    ;
                    if (ok)
                    {
                        RhinoApp.WriteLine($"曲面对{i + 1} 成功采样了{successCount}个点");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"曲面对{i + 1} 没有采样到点");
                    }
                    allSampleBools.Add(ok);
                }

                else if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Brep)
                {
                    var baseBrep = baseObjRfs[i].Brep();
                    var targetBrep = targetObjRfs[i].Brep();
                    baseBrep.Faces.ShrinkFaces();  //收缩一下
                    baseBrep.Standardize(); //
                    targetBrep.Faces.ShrinkFaces();  //收缩一下
                    targetBrep.Standardize(); //
                    baseBrep.Compact(); // Brep对象有时候会有重复的面，导致采样时一一对应出问题，先Compact一下去掉重复面
                    targetBrep.Compact();
                    if (baseBrep.Surfaces.Count != 1 || targetBrep.Surfaces.Count != 1)
                    {
                        RhinoApp.WriteLine($"Brep对象 {i + 1} 的面数不为1，无法采样");
                        continue;
                        // throw new InvalidOperationException($"Brep对象 {i + 1} 的面数不为1，无法采样");
                    }
                    var baseSrf = baseBrep.Surfaces[0].ToNurbsSurface();
                    var targetSrf = targetBrep.Surfaces[0].ToNurbsSurface();

                    int successCount = 0;
                    var ok = false;

                    switch (_config.SampleConfig.SurfaceSampleMethod)
                    {
                        case 0:
                            //uv count必须一样，才能一一对应采样点对计算增量
                            if (baseSrf.Points.CountU != targetSrf.Points.CountU || baseSrf.Points.CountV != targetSrf.Points.CountV)
                            {
                                RhinoApp.WriteLine($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                                throw new InvalidOperationException($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                            }
                            ok = AddSurfaceMapByUVCorrespond(baseSrf, targetSrf, out successCount);
                            break;
                        case 1:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, Vector3d.XAxis, out successCount);
                            break;
                        case 2:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, Vector3d.YAxis, out successCount);
                            break;
                        case 3:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, Vector3d.ZAxis, out successCount);
                            break;
                        case 4:
                            ok = AddSurfaceMapByNormalDirection(baseSrf, targetSrf, out successCount);
                            break;
                        case 5:
                            ok = AddSurfaceMapByDirection(baseSrf, targetSrf, _config.SampleConfig.SurfaceSampleDirection, out successCount);
                            break;
                        default:
                            break;
                    }
                    ;
                    if (ok)
                    {
                        RhinoApp.WriteLine($"曲面对{i + 1} 成功采样了{successCount}个点");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"曲面对{i + 1} 没有采样到点");
                    }
                    allSampleBools.Add(ok);
                }

                else
                {
                    RhinoApp.WriteLine($"base or target 对象 {i + 1} 类型 {baseobj.ObjectType} 不受支持，无法采样");
                }


            }

            if (basePts.Count > 0 && targetPts.Count > 0)
            {

                var ok = AddPtMap(basePts, targetPts, out int successCount);
                if (ok)
                {
                    RhinoApp.WriteLine($"Point对成功采样了{successCount}个点");
                }
                else
                {
                    RhinoApp.WriteLine($"Point对 Fail to sample points");
                }
                allSampleBools.Add(ok);

            }

            return allSampleBools.All(t => t);
        }


        bool SampleLimitedObj(Rhino.DocObjects.ObjRef[] limitedObjRfs)
        {

            if (limitedObjRfs == null || limitedObjRfs.Length == 0)
            {
                RhinoApp.WriteLine("没有受限对象，跳过采样受限对象步骤");
                return true;
            }

            List<bool> allSampleBools = new List<bool>();
            if (limitedObjRfs != null && limitedObjRfs.Length > 0)
            {
                // 保存点类型的受限对象，等处理完所有对象后再统一添加到控制点列表中
                var limitedPoints = new List<Point3d>();
                // 如果是网格，使用网格顶点作为受限点；如果是曲线，按照曲线长度等距采样；如果是点，直接使用点位置
                for (int i = 0; i < limitedObjRfs.Length; i++)
                {
                    var obj = limitedObjRfs[i].Geometry();
                    if (obj.ObjectType == Rhino.DocObjects.ObjectType.Mesh)
                    {
                        var limitedmesh = obj as Mesh;
                        var ok = AddLimitMesh(limitedmesh, out int successCount);
                        if (ok)
                        {
                            RhinoApp.WriteLine($"limited 网格 {i + 1} 成功采样了 {successCount} 个点");
                        }
                        else
                        {
                            RhinoApp.WriteLine($"limited 网格 {i + 1} 没有采样到点");
                        }
                        allSampleBools.Add(ok);
                    }
                    else if (obj.ObjectType == Rhino.DocObjects.ObjectType.Curve)
                    {
                        var limitedCurve = obj as Curve;
                        int count = (int)Math.Ceiling(limitedCurve.GetLength() / _config.SampleConfig.CurveSampleDistance + 1);
                        var ok =
                            AddLimitCurve(limitedCurve, count);
                        if (ok)
                        {
                            RhinoApp.WriteLine($"limited 曲线{i + 1} 成功采样了{count}个点");
                        }
                        else
                        {
                            RhinoApp.WriteLine($"limited 曲线 {i + 1} 没有采样到点");
                        }
                        allSampleBools.Add(ok);
                    }
                    else if (obj.ObjectType == Rhino.DocObjects.ObjectType.Point)
                    {
                        // 点对象转为点位置Point3d
                        limitedPoints.Add(((Point)obj).Location);

                    }

                    else if (obj.ObjectType == Rhino.DocObjects.ObjectType.Surface)
                    {
                        var limitedSrf = obj as Surface;
                        var limitedNurbsSrf = limitedSrf.ToNurbsSurface();
                        var ok = AddLimitSurface(limitedNurbsSrf, out int successCount);
                        if (ok)
                        {
                            RhinoApp.WriteLine($"limited 曲面 {i + 1} 成功采样了 {successCount} 个点");
                        }
                        else
                        {
                            RhinoApp.WriteLine($"limited 曲面 {i + 1} 没有采样到点");
                        }
                        allSampleBools.Add(ok);
                    }
                    else if (obj.ObjectType == Rhino.DocObjects.ObjectType.Brep)
                    {
                        var limitedBrep = obj as Brep;
                        limitedBrep.Faces.ShrinkFaces();  //收缩一下
                        limitedBrep.Standardize(); //
                        limitedBrep.Compact(); // Brep对象有时候会有重复的面，导致采样时一一对应出问题，先Compact一下去掉重复面
                        if (limitedBrep.Surfaces.Count != 1)
                        {
                            RhinoApp.WriteLine($"limited Brep对象 {i + 1} 的面数不为1，无法采样");
                            allSampleBools.Add(false);
                            continue;
                        }
                        var limitedSrf = limitedBrep.Surfaces[0];
                        var limitedNurbsSrf = limitedSrf.ToNurbsSurface();
                        var ok = AddLimitSurface(limitedNurbsSrf, out int successCount);
                        if (ok)
                        {
                            RhinoApp.WriteLine($"limited 曲面 {i + 1} 成功采样了 {successCount} 个点");
                        }
                        else
                        {
                            RhinoApp.WriteLine($"limited 曲面 {i + 1} 没有采样到点");
                        }
                        allSampleBools.Add(ok);
                    }

                    else
                    {
                        RhinoApp.WriteLine($"limited 对象 {i + 1} 类型 {obj.ObjectType} 不受支持，无法采样");
                    }
                }

                // 如果有点类型的受限对象，使用这些点作为受限点
                if (limitedPoints.Count > 0)
                {
                    var okp = AddLimitPoints(limitedPoints, out int successCountPoints);

                    if (okp)
                    {
                        RhinoApp.WriteLine($"limited 点集成功采样了 {successCountPoints} 个点");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"limited 点集没有采样到点");
                    }

                    allSampleBools.Add(okp);
                }
            }

            return allSampleBools.All(t => t);
        }

        public bool MorphPoint(Point3d pt, out Point3d newpt)
        {
            newpt = Point3d.Unset;
            if (_rbfDeformer != null)
            {
                newpt = _rbfDeformer.Evaluate(pt);
                if (newpt != null && newpt != Point3d.Unset)
                    return true;

            }
            return false;

        }



    }
}
