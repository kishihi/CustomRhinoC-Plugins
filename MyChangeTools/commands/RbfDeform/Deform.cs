using MathNet.Numerics.LinearAlgebra.Double;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeform
{
    //抽象类
    public abstract class RBFDeformer
    {
        // RBF 权重
        public double[] Wx { get; protected set; }
        public double[] Wy { get; protected set; }
        public double[] Wz { get; protected set; }

        // 原始控制点和位移
        protected List<Point3d> sourcePts { get; private set; }
        protected List<Vector3d> deltas { get; private set; }


        // 核函数委托，实例化后可以自由替换
        public Func<double, double> Phi { get; set; }

        // 构造函数：接收控制点和位移
        public RBFDeformer(List<Point3d> sourcePoints, List<Vector3d> deltas)
        {
            // 默认 Phi = 原始 RBF
            Phi = DefaultPhi;

            // 验证输入
            if (sourcePoints == null)
                throw new ArgumentNullException(nameof(sourcePoints));
            if (deltas == null)
                throw new ArgumentNullException(nameof(deltas));
            if (sourcePoints.Count != deltas.Count)
                throw new ArgumentException("SourcePoints count must equal Deltas count");

            this.sourcePts = sourcePoints;
            this.deltas = deltas;

            RhinoApp.WriteLine($"RBFDeformer 初始化成功，控制点数量：{sourcePoints.Count}");
        }

        // 默认核函数（可被委托替换）
        protected static double DefaultPhi(double r)
        {
            if (r < 1e-10) return 0.0;
            return r * r * Math.Log(r);
        }

        public abstract void SolveWeights();

        public abstract Point3d Evaluate(Point3d p);


    }


    public class RBFDeformerWithLinearSystem : RBFDeformer
    {

        // affine 项
        private double A0x, A1x, A2x, A3x;
        private double A0y, A1y, A2y, A3y;
        private double A0z, A1z, A2z, A3z;

        public RBFDeformerWithLinearSystem(List<Point3d> sourcePts, List<Vector3d> deltas) : base(sourcePts, deltas)
        {
        }

        public override void SolveWeights()
        {
            int n = sourcePts.Count;
            int m = n + 4;

            RhinoApp.WriteLine($"开始构建 {m}×{m} TPS 系数矩阵...");

            var A = DenseMatrix.Create(m, m, 0);

            // K matrix
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double dist = sourcePts[i].DistanceTo(sourcePts[j]);
                    A[i, j] = Phi(dist);
                }
            }

            // P matrix
            for (int i = 0; i < n; i++)
            {
                var p = sourcePts[i];

                A[i, n + 0] = 1.0;
                A[i, n + 1] = p.X;
                A[i, n + 2] = p.Y;
                A[i, n + 3] = p.Z;
            }

            // P^T matrix
            for (int i = 0; i < n; i++)
            {
                var p = sourcePts[i];

                A[n + 0, i] = 1.0;
                A[n + 1, i] = p.X;
                A[n + 2, i] = p.Y;
                A[n + 3, i] = p.Z;
            }

            // 正则化
            double lambda = 1e-8;
            for (int i = 0; i < n; i++)
            {
                A[i, i] += lambda;
            }

            // 构造 RHS
            var bx = DenseVector.Create(m, 0);
            var by = DenseVector.Create(m, 0);
            var bz = DenseVector.Create(m, 0);

            for (int i = 0; i < n; i++)
            {
                bx[i] = deltas[i].X;
                by[i] = deltas[i].Y;
                bz[i] = deltas[i].Z;
            }

            try
            {
                RhinoApp.WriteLine("求解 TPS 系统...");

                var solx = A.Solve(bx);
                var soly = A.Solve(by);
                var solz = A.Solve(bz);

                Wx = solx.SubVector(0, n).ToArray();
                Wy = soly.SubVector(0, n).ToArray();
                Wz = solz.SubVector(0, n).ToArray();

                A0x = solx[n + 0];
                A1x = solx[n + 1];
                A2x = solx[n + 2];
                A3x = solx[n + 3];

                A0y = soly[n + 0];
                A1y = soly[n + 1];
                A2y = soly[n + 2];
                A3y = soly[n + 3];

                A0z = solz[n + 0];
                A1z = solz[n + 1];
                A2z = solz[n + 2];
                A3z = solz[n + 3];

                RhinoApp.WriteLine("RBF 权重求解完成");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("求解失败");
                RhinoApp.WriteLine(ex.Message);

                Wx = Wy = Wz = null;
            }

            bool hasNanWx = Wx.Any(double.IsNaN);
            bool hasNanWy = Wy.Any(double.IsNaN);
            bool hasNanWz = Wz.Any(double.IsNaN);

            if (hasNanWx || hasNanWy || hasNanWz)
            {
                RhinoApp.WriteLine("Solve Fail, 采样点可能共线或者共平面");
                throw new InvalidOperationException("Weights not Vaild");
            }


            RhinoApp.WriteLine("RBFDeformer 构造完成");


        }

        public override Point3d Evaluate(Point3d p)
        {
            if (Wx == null)
                throw new InvalidOperationException("Weights not initialized");

            double ox = 0;
            double oy = 0;
            double oz = 0;

            int n = sourcePts.Count;

            for (int i = 0; i < n; i++)
            {
                double r = p.DistanceTo(sourcePts[i]);
                double phi = Phi(r);

                ox += Wx[i] * phi;
                oy += Wy[i] * phi;
                oz += Wz[i] * phi;
            }

            // affine part
            ox += A0x + A1x * p.X + A2x * p.Y + A3x * p.Z;
            oy += A0y + A1y * p.X + A2y * p.Y + A3y * p.Z;
            oz += A0z + A1z * p.X + A2z * p.Y + A3z * p.Z;

            return new Point3d(
                p.X + ox,
                p.Y + oy,
                p.Z + oz
            );
        }
    }

    public class RBFDeformerCommon : RBFDeformer
    {


        public RBFDeformerCommon(List<Point3d> sourcePts, List<Vector3d> deltas) : base(sourcePts, deltas)
        {
        }

        public override void SolveWeights()
        {
            int n = sourcePts.Count;
            RhinoApp.WriteLine($"开始构建 {n}×{n} 系数矩阵 A...");

            var A = DenseMatrix.Create(n, n, (i, j) =>
            {
                double dist = sourcePts[i].DistanceTo(sourcePts[j]);
                double val = Phi(dist);
                //// 打印一些代表性元素
                // #if DEBUG
                // if ((i == 0 && j <= 3) || (i == j && j < 3) || (i == n - 1 && j == n - 1))
                // {
                //    RhinoApp.WriteLine($"A[{i},{j}] = Phi({dist:F4}) = {val:F8}");
                // }
                // #endif
                return val;
            });

            RhinoApp.WriteLine($"系数矩阵 A 构建完成，Frobenius范数 ≈ {A.FrobeniusNorm():F4}");

            double lambda = 1e-8;
            RhinoApp.WriteLine($"应用正则化 lambda = {lambda}");

            for (int i = 0; i < n; i++)
            {
                A[i, i] += lambda;
            }

            // 打印几个对角线元素看看加了lambda后的变化
            //RhinoApp.WriteLine($"A[0,0] (加lambda后) = {A[0, 0]:F8}");
            //if (n > 1) RhinoApp.WriteLine($"A[1,1] (加lambda后) = {A[1, 1]:F8}");

            var bx = DenseVector.OfEnumerable(deltas.Select(d => d.X));
            var by = DenseVector.OfEnumerable(deltas.Select(d => d.Y));
            var bz = DenseVector.OfEnumerable(deltas.Select(d => d.Z));

            RhinoApp.WriteLine("右端向量 bx 前3个值：");
            for (int i = 0; i < Math.Min(3, n); i++)
                RhinoApp.WriteLine($"  bx[{i}] = {bx[i]:F6}");

            try
            {
                RhinoApp.WriteLine("开始求解线性方程组 Ax = bx ...");
                var solx = A.Solve(bx);

                RhinoApp.WriteLine("开始求解 Ay = by ...");
                var soly = A.Solve(by);

                RhinoApp.WriteLine("开始求解 Az = bz ...");
                var solz = A.Solve(bz);

                Wx = solx.ToArray();
                Wy = soly.ToArray();
                Wz = solz.ToArray();

                // 打印前几个权重值
                RhinoApp.WriteLine("求解完成，前几个权重值：");
                for (int i = 0; i < Math.Min(4, n); i++)
                {
                    RhinoApp.WriteLine($"  Wx[{i}] = {Wx[i]:F10}   Wy[{i}] = {Wy[i]:F10}   Wz[{i}] = {Wz[i]:F10}");
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                RhinoApp.WriteLine("求解线性方程组失败！");
                RhinoApp.WriteLine($"异常类型: {ex.GetType().Name}");
                RhinoApp.WriteLine($"错误信息: {ex.Message}");
                if (ex.InnerException != null)
                {
                    RhinoApp.WriteLine($"内部异常: {ex.InnerException.Message}");
                }
                RhinoApp.WriteLine("可能原因：矩阵严重奇异 / 点完全共线 / 控制点重复 / 数值溢出");
                RhinoApp.WriteLine($"建议：尝试增大 lambda (当前是 {lambda}) 到 1e-6 或 1e-4");
                RhinoApp.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                Wx = Wy = Wz = null; // 标记失败
            }

            RhinoApp.WriteLine("RBFDeformer 构造完成");
        }

        public override Point3d Evaluate(Point3d p)
        {
            if (Wx == null || Wy == null || Wz == null)
            {
                RhinoApp.WriteLine("Evaluate 失败：权重尚未成功计算");
                throw new InvalidOperationException("Weights not initialized");
                //return Point3d.Unset;
            }

            double ox = 0.0, oy = 0.0, oz = 0.0;
            int n = sourcePts.Count;

            for (int i = 0; i < n; i++)
            {
                double r = p.DistanceTo(sourcePts[i]);
                double phi = Phi(r);
                ox += Wx[i] * phi;
                oy += Wy[i] * phi;
                oz += Wz[i] * phi;
            }

            var result = new Point3d(p.X + ox, p.Y + oy, p.Z + oz);

            return result;
        }
    }



    internal class Deform
    {

        private readonly List<Point3d> _srcPoints = new List<Point3d>();
        private readonly List<Vector3d> _deltas = new List<Vector3d>();
        private readonly RBFDeformer _rbfDeformer;

        public Deform(Curve[] baseCurves, Curve[] targetCurves, Rhino.DocObjects.ObjRef[] limitedObjs, SelectionOptions selectionOptions)
        {
            // _rbfDeformer
            if (baseCurves.Length == 0 || baseCurves.Length != targetCurves.Length)
            {
                RhinoApp.WriteLine("baseCurves Count Must equal targetCurves Count , ant at least one");
                throw new InvalidOperationException("baseCurves Count Must equal targetCurves Count, ant at least one");
            }

            var bools = new List<bool>();

            for (int i = 0; i < baseCurves.Length; i++)
            {
                var baseCurve = baseCurves[i];
                var targetCurve = targetCurves[i];
                int count = (int)Math.Ceiling(Math.Min(baseCurve.GetLength(), targetCurve.GetLength()) / selectionOptions.SampleDistance + 1);
                int successCount = 0;
                if (!selectionOptions.SampleByParameter)
                    bools.Add(AddDeltasFromSamplePointsByClosestPoint(baseCurve, targetCurve, count, out successCount));
                else
                    bools.Add(AddDeltasFromSamplePointsByLength(baseCurve, targetCurve, count, out successCount));
                if (successCount > 0)
                    Rhino.RhinoApp.WriteLine($"曲线对{i + 1} 成功采样了{successCount}个点");
                else
                    Rhino.RhinoApp.WriteLine($"曲线对{i + 1} 没有采样到点");
            }

            // limitedObjs 可以是曲线,网格,点，分别处理

            if (limitedObjs != null && limitedObjs.Length > 0)
            {
                // 保存点类型的受限对象，等处理完所有对象后再统一添加到控制点列表中
                var limitedPoints = new List<Point3d>();
                // 如果是网格，使用网格顶点作为受限点；如果是曲线，按照曲线长度等距采样；如果是点，直接使用点位置
                for (int i = 0; i < limitedObjs.Length; i++)
                {
                    var obj = limitedObjs[i].Geometry();
                    if (obj.ObjectType == Rhino.DocObjects.ObjectType.Mesh)
                    {
                        var limitedmesh = obj as Mesh;
                        var ok = AddDeltasFromLimitedMesh(limitedmesh, out int successCount);
                        if (ok)
                        {
                            Rhino.RhinoApp.WriteLine($"limited 网格 {i + 1} 成功采样了 {successCount} 个点");
                        }
                        else
                        {
                            Rhino.RhinoApp.WriteLine($"limited 网格 {i + 1} 没有采样到点");
                        }
                        bools.Add(ok);
                    }
                    else if (obj.ObjectType == Rhino.DocObjects.ObjectType.Curve)
                    {
                        var limitedCurve = obj as Curve;
                        int count = (int)Math.Ceiling(limitedCurve.GetLength()/selectionOptions.SampleDistance + 1);
                        var ok =
                            AddDeltasFromLimitedCurve(limitedCurve, count);
                        if (ok)
                        {
                            Rhino.RhinoApp.WriteLine($"limited 曲线{i + 1} 成功采样了{count}个点");
                        }
                        else
                        {
                            Rhino.RhinoApp.WriteLine($"limited 曲线 {i + 1} 没有采样到点");
                        }
                        bools.Add(ok);
                    }
                    else if (obj.ObjectType == Rhino.DocObjects.ObjectType.Point)
                    {
                        // 点对象转为点位置Point3d
                        limitedPoints.Add(((Rhino.Geometry.Point)obj).Location);

                    }

                    else
                    {
                        Rhino.RhinoApp.WriteLine($"limited 对象 {i + 1} 不是曲线,网格,点，无法采样");
                    }
                }

                // 如果有点类型的受限对象，使用这些点作为受限点
                if (limitedPoints.Count > 0)
                {
                    var okp = AddDeltasFromLimitedPoints(limitedPoints, out int successCountPoints);

                    if (okp)
                    {
                        Rhino.RhinoApp.WriteLine($"limited 点集成功采样了 {successCountPoints} 个点");
                    }
                    else
                    {
                        Rhino.RhinoApp.WriteLine($"limited 点集没有采样到点");
                    }

                    bools.Add(okp);
                }
            }
            
            // 如果所有曲线对和受限对象都成功采样到点，则构造 RBFDeformer
            if (bools.All(t => t))
            {
                if (!selectionOptions.LinearSystem)
                    _rbfDeformer = new RBFDeformerCommon(_srcPoints, _deltas);
                else
                    _rbfDeformer = new RBFDeformerWithLinearSystem(_srcPoints, _deltas);

                if (selectionOptions.InfectRadius > 0)
                {
                    RhinoApp.WriteLine($"使用CSRBF，影响半径 R = {selectionOptions.InfectRadius}");
                    double R = selectionOptions.InfectRadius;
                    _rbfDeformer.Phi = r =>
                    {
                        double rho = r / R;
                        if (rho >= 1.0) return 0.0;
                        double t = 1 - rho;
                        return t * t * t * t * (4 * rho + 1);
                    };
                }

                _rbfDeformer.SolveWeights();

                Rhino.RhinoApp.WriteLine($"Length {_rbfDeformer.Wx.Length} WxMax: {_rbfDeformer.Wx.Max()},WxMin:{_rbfDeformer.Wx.Min()}");
                Rhino.RhinoApp.WriteLine($"Length {_rbfDeformer.Wy.Length} WyMax: {_rbfDeformer.Wy.Max()},WyMin:{_rbfDeformer.Wy.Min()}");
                Rhino.RhinoApp.WriteLine($"Length {_rbfDeformer.Wz.Length} WzMax: {_rbfDeformer.Wz.Max()},WzMin:{_rbfDeformer.Wz.Min()}");

            }
            else
            {
                _rbfDeformer = null;
                RhinoApp.WriteLine("有对象没有成功采样到点，无法构造 RBFDeformer, 请检查输入曲线和采样参数");
                throw new AggregateException("_rbfDeformer null !");
            }

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

        bool ComputeDeltaByNormal(Point3d sourcePoint, Curve targetCurve, out Vector3d delta)
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

        bool AddDeltasFromSamplePointsByClosestPoint(Curve baseCurve, Curve targetCurve, int count, out int successCount)
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

        bool AddDeltasFromSamplePointsByLength(Curve baseCurve, Curve targetCurve, int count, out int successCount)
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

    }
}
