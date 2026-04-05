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
        static readonly public Dictionary<int, string> PhiIndexFunNameDict = new Dictionary<int, string>
        {
            {0, "TPS"},
            {1, "CSRBFW2"},
            {2, "GAUSS"}
        };
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
        public static double GAUSS(double r, double R)
        {
            double t = r / R;
            return Math.Exp(-t * t);
        }
    }

    // 维度枚举
    [Flags]
    public enum RbfDimension
    {
        X = 1,
        Y = 2,
        Z = 4,
        XY = X | Y,
        YZ = Y | Z,
        XZ = X | Z,
        XYZ = X | Y | Z
    }

    //抽象类
    public abstract class RBFDeformer
    {
        // RBF 权重
        public double[] Wx { get; protected set; }
        public double[] Wy { get; protected set; }
        public double[] Wz { get; protected set; }

        // 原始控制点和位移
        protected List<Point3d> SourcePts { get; private set; }
        protected List<Vector3d> Deltas { get; private set; }

        public RbfDimension DimensionMask { get; set; } = RbfDimension.XYZ;

        // 核函数委托，实例化后可以自由替换
        public Func<double, double> Phi { get; set; } = null;

        // 构造函数：接收控制点和位移
        public RBFDeformer(List<Point3d> sourcePoints, List<Vector3d> deltas)
        {
            // 默认 Phi = 原始 RBF
            // Phi = RBFPhiFunctions.TPS;

            // 验证输入
            if (sourcePoints == null)
                throw new ArgumentNullException(nameof(sourcePoints));
            if (deltas == null)
                throw new ArgumentNullException(nameof(deltas));
            if (sourcePoints.Count != deltas.Count)
                throw new ArgumentException("SourcePoints count must equal Deltas count");

            this.SourcePts = sourcePoints;
            this.Deltas = deltas;

            RhinoApp.WriteLine($"RBFDeformer 初始化成功，控制点数量：{sourcePoints.Count}");
        }

        public abstract void SolveWeights();

        // 评估计算
        protected bool HasNanWx = false;
        protected bool HasNanWy = false;
        protected bool HasNanWz = false;

        public abstract Point3d Evaluate(Point3d p);

        // 计算两点之间的距离，根据维度掩码选择性地计算
        protected double ComputeDistance(Point3d a, Point3d b)
        {
            double dx = 0, dy = 0, dz = 0;

            if (DimensionMask.HasFlag(RbfDimension.X))
                dx = a.X - b.X;

            if (DimensionMask.HasFlag(RbfDimension.Y))
                dy = a.Y - b.Y;

            if (DimensionMask.HasFlag(RbfDimension.Z))
                dz = a.Z - b.Z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }


    }

    public class RBFDeformerWithLinearSystem : RBFDeformer
    {

        // affine 项
        private double A0x, A1x, A2x, A3x;
        private double A0y, A1y, A2y, A3y;
        private double A0z, A1z, A2z, A3z;

        private bool HasNanAffineX = false;
        private bool HasNanAffineY = false;
        private bool HasNanAffineZ = false;

        public RBFDeformerWithLinearSystem(List<Point3d> sourcePts, List<Vector3d> deltas) : base(sourcePts, deltas)
        {
        }

        public override void SolveWeights()
        {
            int n = SourcePts.Count;

            // 构造参与 affine 的列数量
            List<RbfDimension> activeDims = new List<RbfDimension>();
            if (DimensionMask.HasFlag(RbfDimension.X)) activeDims.Add(RbfDimension.X);
            if (DimensionMask.HasFlag(RbfDimension.Y)) activeDims.Add(RbfDimension.Y);
            if (DimensionMask.HasFlag(RbfDimension.Z)) activeDims.Add(RbfDimension.Z);

            int affCols = 1 + activeDims.Count; // 1列常数 + 参与维度列
            int m = n + affCols;

            RhinoApp.WriteLine($"构建 {m}×{m} TPS 系数矩阵, 控制点数量：{n}, 维度掩码：{DimensionMask}");

            var A = DenseMatrix.Create(m, m, 0);

            // ------------------------
            // K 矩阵 n×n phi(r)
            // ------------------------
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double r = ComputeDistance(SourcePts[i], SourcePts[j]);
                    A[i, j] = Phi(r);
                }
            }

            // ------------------------
            // P 矩阵 n×affCols
            // ------------------------
            for (int i = 0; i < n; i++)
            {
                int col = n;
                A[i, col++] = 1.0; // 常数列
                foreach (var dim in activeDims)
                {
                    switch (dim)
                    {
                        case RbfDimension.X: A[i, col++] = SourcePts[i].X; break;
                        case RbfDimension.Y: A[i, col++] = SourcePts[i].Y; break;
                        case RbfDimension.Z: A[i, col++] = SourcePts[i].Z; break;
                    }
                }
            }

            // ------------------------
            // P^T 矩阵 affCols×n
            // ------------------------
            for (int i = 0; i < n; i++)
            {
                int row = n;
                A[row++, i] = 1.0; // 常数列
                foreach (var dim in activeDims)
                {
                    switch (dim)
                    {
                        case RbfDimension.X: A[row++, i] = SourcePts[i].X; break;
                        case RbfDimension.Y: A[row++, i] = SourcePts[i].Y; break;
                        case RbfDimension.Z: A[row++, i] = SourcePts[i].Z; break;
                    }
                }
            }

            // ------------------------
            // 正则化，对 K 对角线加 lambda
            // ------------------------
            double lambda = 1e-8;
            for (int i = 0; i < n; i++)
                A[i, i] += lambda;

            // ------------------------
            // 构造 RHS
            // ------------------------
            var bx = DenseVector.Create(m, 0.0);
            var by = DenseVector.Create(m, 0.0);
            var bz = DenseVector.Create(m, 0.0);

            for (int i = 0; i < n; i++)
            {
                if (DimensionMask.HasFlag(RbfDimension.X)) bx[i] = Deltas[i].X;
                if (DimensionMask.HasFlag(RbfDimension.Y)) by[i] = Deltas[i].Y;
                if (DimensionMask.HasFlag(RbfDimension.Z)) bz[i] = Deltas[i].Z;
            }

            // ------------------------
            // 初始化权重和 affine
            // ------------------------
            Wx = new double[n];
            Wy = new double[n];
            Wz = new double[n];

            A0x = A1x = A2x = A3x = 0;
            A0y = A1y = A2y = A3y = 0;
            A0z = A1z = A2z = A3z = 0;

            try
            {

                // X 维
                if (DimensionMask.HasFlag(RbfDimension.X))
                {
                    var solx = A.Solve(bx);
                    Wx = solx.SubVector(0, n).ToArray();

                    // affine 系数
                    A0x = solx[n + 0];
                    int idx = 1;
                    foreach (var dim in activeDims)
                    {
                        switch (dim)
                        {
                            case RbfDimension.X: A1x = solx[n + idx++]; break;
                            case RbfDimension.Y: A2x = solx[n + idx++]; break;
                            case RbfDimension.Z: A3x = solx[n + idx++]; break;
                        }
                    }
                }

                // Y 维
                if (DimensionMask.HasFlag(RbfDimension.Y))
                {
                    var soly = A.Solve(by);
                    Wy = soly.SubVector(0, n).ToArray();

                    A0y = soly[n + 0];
                    int idx = 1;
                    foreach (var dim in activeDims)
                    {
                        switch (dim)
                        {
                            case RbfDimension.X: A1y = soly[n + idx++]; break;
                            case RbfDimension.Y: A2y = soly[n + idx++]; break;
                            case RbfDimension.Z: A3y = soly[n + idx++]; break;
                        }
                    }
                }

                // Z 维
                if (DimensionMask.HasFlag(RbfDimension.Z))
                {
                    var solz = A.Solve(bz);
                    Wz = solz.SubVector(0, n).ToArray();

                    A0z = solz[n + 0];
                    int idx = 1;
                    foreach (var dim in activeDims)
                    {
                        switch (dim)
                        {
                            case RbfDimension.X: A1z = solz[n + idx++]; break;
                            case RbfDimension.Y: A2z = solz[n + idx++]; break;
                            case RbfDimension.Z: A3z = solz[n + idx++]; break;
                        }
                    }
                }

                RhinoApp.WriteLine("RBF 权重求解完成");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("求解失败：" + ex.Message);
                Wx = Wy = Wz = null;
                throw new InvalidOperationException("Weights not valid");
            }
            finally
            {
                // 检查是否有 NaN
                HasNanWx = Wx != null && Wx.Any(double.IsNaN);
                HasNanWy = Wy != null && Wy.Any(double.IsNaN);
                HasNanWz = Wz != null && Wz.Any(double.IsNaN);

                HasNanAffineX = double.IsNaN(A0x) || double.IsNaN(A1x) || double.IsNaN(A2x) || double.IsNaN(A3x);
                HasNanAffineY = double.IsNaN(A0y) || double.IsNaN(A1y) || double.IsNaN(A2y) || double.IsNaN(A3y);
                HasNanAffineZ = double.IsNaN(A0z) || double.IsNaN(A1z) || double.IsNaN(A2z) || double.IsNaN(A3z);

                if (HasNanWx || HasNanWy || HasNanWz || HasNanAffineX || HasNanAffineY || HasNanAffineZ)
                {
                    RhinoApp.WriteLine("警告：权重或仿射系数包含 NaN，可能导致变形失败");
                    throw new InvalidOperationException("Weights not valid");
                }
            }
        }

        public override Point3d Evaluate(Point3d p)
        {

            double ox = 0;
            double oy = 0;
            double oz = 0;

            int n = SourcePts.Count;

            for (int i = 0; i < n; i++)
            {
                double r = ComputeDistance(p, SourcePts[i]);
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
            int n = SourcePts.Count;

            //nx n 的系数矩阵 A phi(r) 矩阵
            var A = DenseMatrix.Create(n, n, (i, j) =>
            {
                double dist = ComputeDistance(SourcePts[i], SourcePts[j]);
                double val = Phi(dist);
                return val;
            });
            // 正则化，增加对角线元素，防止矩阵奇异
            double lambda = 1e-8;

            for (int i = 0; i < n; i++)
            {
                A[i, i] += lambda;
            }

            // 构造 RHS 向量 , if a dimension is not involved in deformation, its corresponding RHS component remains 0
            var bx = DenseVector.OfEnumerable(Deltas.Select(d => DimensionMask.HasFlag(RbfDimension.X) ? d.X : 0.0));
            var by = DenseVector.OfEnumerable(Deltas.Select(d => DimensionMask.HasFlag(RbfDimension.Y) ? d.Y : 0.0));
            var bz = DenseVector.OfEnumerable(Deltas.Select(d => DimensionMask.HasFlag(RbfDimension.Z) ? d.Z : 0.0));

            try
            {
                // 默认Wx, Wy, Wz为0，只有参与变形的维度才会被求解
                Wx = new double[n];
                Wy = new double[n];
                Wz = new double[n];
                if (DimensionMask.HasFlag(RbfDimension.X))
                {
                    var solx = A.Solve(bx);
                    Wx = solx.ToArray();
                }
                if (DimensionMask.HasFlag(RbfDimension.Y))
                {
                    var soly = A.Solve(by);
                    Wy = soly.ToArray();
                }
                if (DimensionMask.HasFlag(RbfDimension.Z))
                {
                    var solz = A.Solve(bz);
                    Wz = solz.ToArray();
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
            finally
            {
                // 检查是否有 NaN
                HasNanWx = Wx != null && Wx.Any(double.IsNaN);
                HasNanWy = Wy != null && Wy.Any(double.IsNaN);
                HasNanWz = Wz != null && Wz.Any(double.IsNaN);

                if (HasNanWx || HasNanWy || HasNanWz)
                {
                    RhinoApp.WriteLine("警告：权重包含 NaN，可能导致变形失败");
                    throw new InvalidOperationException("Weights not valid");
                }
            }

            RhinoApp.WriteLine("RBFDeformer 构造完成");
        }

        public override Point3d Evaluate(Point3d p)
        {
            double ox = 0.0, oy = 0.0, oz = 0.0;
            int n = SourcePts.Count;
            for (int i = 0; i < n; i++)
            {
                double r = ComputeDistance(p, SourcePts[i]);
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