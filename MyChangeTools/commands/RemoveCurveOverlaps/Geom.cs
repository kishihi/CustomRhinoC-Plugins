using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyChangeTools.commands.RemoveCurveOverlaps
{

    internal class Geom
    {
        public static List<double> FindKinks(NurbsCurve curve)
        {
            List<double> kinkParams = new List<double>();
            var knots = curve.Knots;
            int degree = curve.Degree;
            double domainMin = curve.Domain.Min;
            double domainMax = curve.Domain.Max;

            for (int i = 0; i < knots.Count;)
            {
                double currentKnot = knots[i];
                int multiplicity = 1;

                // 计算 multiplicity
                while
                (
                    i + multiplicity < knots.Count
                &&
                    Math.Abs(knots[i + multiplicity] - currentKnot) < Rhino.RhinoMath.ZeroTolerance
                )
                {
                    multiplicity++;
                }

                // 如果 multiplicity == degree 且是内部 knot，则是 kink
                if (multiplicity == degree && currentKnot > domainMin && currentKnot < domainMax)
                {
                    kinkParams.Add(currentKnot);
                }

                i += multiplicity;  // 跳到下一个独特 knot
            }

            return kinkParams;
        }
    }
}
