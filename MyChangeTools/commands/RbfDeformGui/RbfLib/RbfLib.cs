using MathNet.Numerics.LinearAlgebra.Double;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui.RBFLib
{
    public static class RBFPhiFunctions
    {
        public static double TPS(double r)
        {
            if (r < 1e-10) return 0.0;
            return r * r * Math.Log(r);
        }
        public static double CSRBFW2(double r, double R)
        {
            double rho = r / R;
            if (rho >= 1.0) return 0.0;
            double t = 1 - rho;
            return t * t * t * t * (4 * rho + 1);
        }
    }

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
            Phi = RBFPhiFunctions.TPS;

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
                return val;
            });

            RhinoApp.WriteLine($"系数矩阵 A 构建完成，Frobenius范数 ≈ {A.FrobeniusNorm():F4}");

            double lambda = 1e-8;
            RhinoApp.WriteLine($"应用正则化 lambda = {lambda}");

            for (int i = 0; i < n; i++)
            {
                A[i, i] += lambda;
            }

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
}