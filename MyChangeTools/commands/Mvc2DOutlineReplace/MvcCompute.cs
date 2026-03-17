using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyChangeTools.commands.Mvc2DOutlineReplace
{
    internal class MvcCompute
    {
        public static double[] ComputeWeights(Point2d p, List<Point2d> cage)
        {
            int n = cage.Count;

            double[] w = new double[n];
            double[] r = new double[n];
            double[] theta = new double[n];

            Vector2d[] diff = new Vector2d[n];

            const double eps = 1e-8;

            // 计算向量和距离
            for (int i = 0; i < n; i++)
            {
                diff[i] = cage[i] - p;
                r[i] = diff[i].Length;

                // 特殊情况：点接近顶点
                if (r[i] < eps)
                {
                    double[] result = new double[n];
                    result[i] = 1.0;
                    return result;
                }
            }

            // 计算相邻向量夹角
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;

                Vector2d vi = diff[i];
                Vector2d vj = diff[j];

                double dot = vi * vj;
                double len = vi.Length * vj.Length;

                double cos = dot / len;
                cos = Math.Max(-1.0, Math.Min(1.0, cos));

                theta[i] = Math.Acos(cos);
            }

            // 计算权重
            for (int i = 0; i < n; i++)
            {
                int im = (i - 1 + n) % n;

                double tan1 = Math.Tan(theta[im] * 0.5);
                double tan2 = Math.Tan(theta[i] * 0.5);

                w[i] = (tan1 + tan2) / r[i];
            }

            // 归一化
            double sum = 0.0;
            for (int i = 0; i < n; i++)
                sum += w[i];

            for (int i = 0; i < n; i++)
                w[i] /= sum;

            return w;
        }

        public static Point3d DeformPoint(
    Point3d p , List<Point2d> srcCage, List<Point2d> dstCage)
        {
            var p2d = new Point2d(p.X, p.Y);
            double[] w = ComputeWeights(p2d, srcCage);

            Point2d result = Point2d.Origin;

            for (int i = 0; i < dstCage.Count; i++)
            {
                result += w[i] * dstCage[i];
            }

            return new Point3d(result.X, result.Y, p.Z);
        }
    }


}
