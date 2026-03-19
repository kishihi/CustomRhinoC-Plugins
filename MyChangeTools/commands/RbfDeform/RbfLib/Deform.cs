using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeform.RBFLib
{

    internal class Deform
    {

        private readonly List<Point3d> _srcPoints = new List<Point3d>();
        private readonly List<Vector3d> _deltas = new List<Vector3d>();
        private readonly RBFDeformer _rbfDeformer;

        private readonly SelectionOptions _selectionOptions;

        public Deform(
            Rhino.DocObjects.ObjRef[] baseObjRfs,
            Rhino.DocObjects.ObjRef[] targetObjRfs,
            Rhino.DocObjects.ObjRef[] limitedObjRfs,
            SelectionOptions selectionOptions)
        {
            // _rbfDeformer
            if (baseObjRfs.Length == 0 || baseObjRfs.Length != targetObjRfs.Length)
            {
                RhinoApp.WriteLine("baseObjRfs Count Must equal targetObjRfs Count , ant at least one");
                throw new InvalidOperationException("baseObjRfs Count Must equal targetObjRfs Count , ant at least one");
            }

            _selectionOptions = selectionOptions;

            // set shape matching constraints
            var ok1 = SampleMatchObj(baseObjRfs, targetObjRfs);

            // set limited constraints
            var ok2 = SampleLimitedObj(limitedObjRfs);


            // 如果所有曲线对和受限对象都成功采样到点，则构造 RBFDeformer
            if (ok1 && ok2 && _srcPoints.Count > 0)
            {
                if (!selectionOptions.RBFAddLinearSystem)
                    _rbfDeformer = new RBFDeformerCommon(_srcPoints, _deltas);
                else
                    _rbfDeformer = new RBFDeformerWithLinearSystem(_srcPoints, _deltas);

                if (selectionOptions.InfectRadius > 0)
                {
                    
                    double R = selectionOptions.InfectRadius;
                    if (!selectionOptions.IsSoftInfectRadius)
                    {
                        _rbfDeformer.Phi = r => RBFPhiFunctionsWithRadius.CSRBFW2(r, R);
                        RhinoApp.WriteLine($"使用CSRBFW2，影响半径 R = {selectionOptions.InfectRadius}");

                    }
                    else
                    {   _rbfDeformer.Phi = r => RBFPhiFunctionsWithRadius.GAUSS(r, R);
                        RhinoApp.WriteLine($"使用GAUSS，半径参数 R = {selectionOptions.InfectRadius}");
                    }
                }

                _rbfDeformer.SolveWeights();

                RhinoApp.WriteLine($"Length {_rbfDeformer.Wx.Length} WxMax: {_rbfDeformer.Wx.Max()},WxMin:{_rbfDeformer.Wx.Min()}");
                RhinoApp.WriteLine($"Length {_rbfDeformer.Wy.Length} WyMax: {_rbfDeformer.Wy.Max()},WyMin:{_rbfDeformer.Wy.Min()}");
                RhinoApp.WriteLine($"Length {_rbfDeformer.Wz.Length} WzMax: {_rbfDeformer.Wz.Max()},WzMin:{_rbfDeformer.Wz.Min()}");

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
                if (baseobj.ObjectType == Rhino.DocObjects.ObjectType.Curve)
                {
                    var baseCurve = baseObjRfs[i].Curve();
                    var targetCurve = targetObjRfs[i].Curve();
                    int count = (int)Math.Ceiling(Math.Min(baseCurve.GetLength(), targetCurve.GetLength()) / _selectionOptions.CurveSampleDistance + 1);
                    int successCount;
                    if (!_selectionOptions.CurveSampleByParameter)
                        allSampleBools.Add(AddDeltasFromTwoCurveSamplePointsByClosestPoint(baseCurve, targetCurve, count, out successCount));
                    else
                        allSampleBools.Add(AddDeltasFromTwoCurveSamplePointsByLength(baseCurve, targetCurve, count, out successCount));
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
                    if (!_selectionOptions.MatchMeshByCoordinate)
                        ok = AddDeltasFromTwoMeshByVertexOrder(baseMesh, targetMesh, out successCount);
                    else
                        ok = AddDeltasFromTwoMeshByCoordinate(baseMesh, targetMesh, out successCount);
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

                    //uv count必须一样，才能一一对应采样点对计算增量
                    if (baseSrf.Points.CountU != targetSrf.Points.CountU || baseSrf.Points.CountV != targetSrf.Points.CountV)
                    {
                        RhinoApp.WriteLine($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                        throw new InvalidOperationException($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                    }
                    var ok = AddDeltasFromTwoNurbsSurfaceSamplePoints(baseSrf, targetSrf, out int successCount);
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
                    baseBrep.Compact(); // Brep对象有时候会有重复的面，导致采样时一一对应出问题，先Compact一下去掉重复面
                    targetBrep.Compact();
                    if(baseBrep.Surfaces.Count != 1 || targetBrep.Surfaces.Count != 1)
                    {
                        RhinoApp.WriteLine($"Brep对象 {i + 1} 的面数不为1，无法采样");
                        continue;
                        // throw new InvalidOperationException($"Brep对象 {i + 1} 的面数不为1，无法采样");
                    }
                    var baseSrf = baseBrep.Surfaces[0].ToNurbsSurface();
                    var targetSrf = targetBrep.Surfaces[0].ToNurbsSurface();

                    //uv count必须一样，才能一一对应采样点对计算增量
                    if (baseSrf.Points.CountU != targetSrf.Points.CountU || baseSrf.Points.CountV != targetSrf.Points.CountV)
                    {
                        RhinoApp.WriteLine($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                        throw new InvalidOperationException($"曲面对 {i + 1} 的 UV count 不一致，无法采样");
                    }
                    var ok = AddDeltasFromTwoNurbsSurfaceSamplePoints(baseSrf, targetSrf, out int successCount);
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

                var ok = AddDeltasFormTwoPts(basePts, targetPts, out int successCount);
                if (ok)
                {
                    RhinoApp.WriteLine($"Point对成功采样了{successCount}个点");
                }
                else
                {
                    RhinoApp.WriteLine($"Point对Fail to sample points");
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
                        var ok = AddDeltasFromLimitedMesh(limitedmesh, out int successCount);
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
                        int count = (int)Math.Ceiling(limitedCurve.GetLength() / _selectionOptions.CurveSampleDistance + 1);
                        var ok =
                            AddDeltasFromLimitedCurve(limitedCurve, count);
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
                        var ok = AddDeltasFromLimitedNurbsSurface(limitedNurbsSrf, out int successCount);
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
                        limitedBrep.Compact(); // Brep对象有时候会有重复的面，导致采样时一一对应出问题，先Compact一下去掉重复面
                        if(limitedBrep.Surfaces.Count != 1)
                        {
                            RhinoApp.WriteLine($"limited Brep对象 {i + 1} 的面数不为1，无法采样");
                            allSampleBools.Add(false);
                            continue;
                        }
                        var limitedSrf = limitedBrep.Surfaces[0];
                        var limitedNurbsSrf = limitedSrf.ToNurbsSurface();
                        var ok = AddDeltasFromLimitedNurbsSurface(limitedNurbsSrf, out int successCount);
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
                    var okp = AddDeltasFromLimitedPoints(limitedPoints, out int successCountPoints);

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

        static bool ComputeDeltaByNormal(Point3d sourcePoint, Curve targetCurve, out Vector3d delta)
        {
            delta = Vector3d.Unset;
            if (targetCurve.ClosestPoint(sourcePoint, out double t, 0))
            {
                var dstpoint = targetCurve.PointAt(t);
                delta = dstpoint - sourcePoint;
                return true;
            }

            return false;
        }


        bool AddDeltasFromLimitedCurve(Curve limitedCurve, int count)
        {
            NurbsCurve nc = limitedCurve.ToNurbsCurve();
            nc.Domain = new Interval(0, count);
            var samplePoints = Enumerable.Range(0, count + 1).Select(t => nc.PointAt(t));
            var ZeroDeltas = Enumerable.Range(0, count + 1).Select(t => Vector3d.Zero);

            _srcPoints.AddRange(samplePoints);
            _deltas.AddRange(ZeroDeltas);

            return samplePoints.Count() > 0;
        }

        //use mesh vertex as limited point
        bool AddDeltasFromLimitedMesh(Mesh limitedMesh, out int successCount)
        {
            successCount = 0;
            if (limitedMesh == null || limitedMesh.Vertices.Count == 0 || !limitedMesh.IsValid)
            {
                return false;
            }
            else
            {
                var srcPoints = limitedMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));
                var zeroDeltas = limitedMesh.Vertices.Select(v => Vector3d.Zero);
                _srcPoints.AddRange(srcPoints);
                _deltas.AddRange(zeroDeltas);
                successCount = limitedMesh.Vertices.Count;
            }
            return successCount > 0;

        }

        //use points as limited point
        bool AddDeltasFromLimitedPoints(IEnumerable<Point3d> points, out int successCount)
        {
            successCount = 0;
            if (points == null || !points.Any())
            {
                return false;
            }
            else
            {
                var zeroDeltas = points.Select(p => Vector3d.Zero);
                _srcPoints.AddRange(points);
                _deltas.AddRange(zeroDeltas);
                successCount = points.Count();
            }
            return successCount > 0;
        }

        bool AddDeltasFromTwoCurveSamplePointsByClosestPoint(Curve baseCurve, Curve targetCurve, int count, out int successCount)
        {
            successCount = 0;
            NurbsCurve nurbsBaseCurve = baseCurve.ToNurbsCurve();
            nurbsBaseCurve.Domain = new Interval(0, count);
            var srcPoints = Enumerable.Range(0, count + 1).Select(t => nurbsBaseCurve.PointAt(t));
            var successSrcPoints = new List<Point3d>();
            var successDeltas = new List<Vector3d>();
            foreach (Point3d srcpoint in srcPoints)
            {

                if (ComputeDeltaByNormal(srcpoint, targetCurve, out Vector3d delta))
                {
                    successDeltas.Add(delta);
                    successSrcPoints.Add(srcpoint);
                }

            }
            _srcPoints.AddRange(successSrcPoints);
            _deltas.AddRange(successDeltas);
            if (successSrcPoints.Count > 0)
            {
                successCount = successSrcPoints.Count;

                return true;
            }
            else
            {
                return false;
            }

        }

        bool AddDeltasFromTwoCurveSamplePointsByLength(Curve baseCurve, Curve targetCurve, int count, out int successCount)
        {
            successCount = 0;

            NurbsCurve nurbsBase = baseCurve.ToNurbsCurve();
            NurbsCurve nurbsTarget = targetCurve.ToNurbsCurve();

            double baseLength = nurbsBase.GetLength();
            double targetLength = nurbsTarget.GetLength();

            // 采样参数列表
            var srcPoints = new List<Point3d>();
            var dstPoints = new List<Point3d>();

            for (int i = 0; i <= count; i++)
            {
                if (nurbsBase.LengthParameter(baseLength * i / count, out double tBase) &&
                nurbsTarget.LengthParameter(targetLength * i / count, out double tTarget))
                {

                    srcPoints.Add(nurbsBase.PointAt(tBase));
                    dstPoints.Add(nurbsTarget.PointAt(tTarget));
                }
            }
            var deltas = srcPoints.Zip(dstPoints, (src, dst) => dst - src).ToList();

            _srcPoints.AddRange(srcPoints);
            _deltas.AddRange(deltas);

            if (srcPoints.Count > 0)
            {
                successCount = srcPoints.Count;
                return true;
            }
            else
            {
                return false;
            }
        }


        bool AddDeltasFromTwoMeshByVertexOrder(Mesh baseMesh, Mesh targetMesh, out int successCount)
        {
            successCount = 0;
            if (baseMesh.Vertices.Count != targetMesh.Vertices.Count) { return false; }
            ;
            var srcPts = baseMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));
            var dstPts = targetMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));
            var deltas = srcPts.Zip(dstPts, (src, dst) => dst - src);
            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);
            successCount = srcPts.Count();
            return successCount > 0;
        }

        bool AddDeltasFromTwoMeshByCoordinate(Mesh baseMesh, Mesh targetMesh, out int successCount)
        {
            successCount = 0;
            if (baseMesh.Vertices.Count != targetMesh.Vertices.Count) { return false; }

            var srcPts = baseMesh.Vertices.Select(v => new Point3d(v.X, v.Y, v.Z));

            var baseMps = srcPts.Select(t => baseMesh.ClosestMeshPoint(t, 0.0));

            var dstPts = baseMps.Select(bmp => targetMesh.PointAt(bmp));

            var deltas = srcPts.Zip(dstPts, (src, dst) => dst - src);
            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);
            successCount = srcPts.Count();
            return successCount > 0;
        }

        bool AddDeltasFormTwoPts(IEnumerable<Point3d> basePts, IEnumerable<Point3d> targetPts, out int successCount)
        {
            successCount = 0;
            if (basePts.Count() != targetPts.Count()) return false;
            var deltas = basePts.Zip(targetPts, (src, dst) => dst - src);
            _srcPoints.AddRange(basePts);
            _deltas.AddRange(deltas);
            successCount = basePts.Count();
            return successCount > 0;
        }

        //对UVcount一样的两张曲面进行采样，得到对应点对，计算位移增量
        bool AddDeltasFromTwoNurbsSurfaceSamplePoints(NurbsSurface baseSrf, NurbsSurface targetSrf, out int successCount)
        {
            successCount = 0;
            if (baseSrf == null || targetSrf == null) return false;
            if (baseSrf.Points.CountU != targetSrf.Points.CountU || baseSrf.Points.CountV != targetSrf.Points.CountV) return false;
            var srcPts = new List<Point3d>();
            var dstPts = new List<Point3d>();
            for (int i = 0; i < baseSrf.Points.CountU; i++)
            {
                for (int j = 0; j < baseSrf.Points.CountV; j++)
                {
                    srcPts.Add(baseSrf.Points.GetControlPoint(i, j).Location);
                    dstPts.Add(targetSrf.Points.GetControlPoint(i, j).Location);
                }
            }
            var deltas = srcPts.Zip(dstPts, (src, dst) => dst - src);
            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(deltas);
            successCount = srcPts.Count();
            return successCount > 0;
        }

        // 对一张曲面进行采样，得到点，计算位移增量为0，作为受限点
        bool AddDeltasFromLimitedNurbsSurface(NurbsSurface limitedSrf, out int successCount)
        {
            successCount = 0;
            if (limitedSrf == null) return false;
            var srcPts = new List<Point3d>();
            for (int i = 0; i < limitedSrf.Points.CountU; i++)
            {
                for (int j = 0; j < limitedSrf.Points.CountV; j++)
                {
                    srcPts.Add(limitedSrf.Points.GetControlPoint(i, j).Location);
                }
            }
            var zeroDeltas = srcPts.Select(p => Vector3d.Zero);
            _srcPoints.AddRange(srcPts);
            _deltas.AddRange(zeroDeltas);
            successCount = srcPts.Count();
            return successCount > 0;
        }

    }
}
