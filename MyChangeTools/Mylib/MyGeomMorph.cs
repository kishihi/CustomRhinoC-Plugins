using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyChangeTools.Mylib
{
    public class MyGeomMorph
    {

        private readonly int _rebuildFaceUcount;
        private readonly int _rebuildFaceVcount;
        private readonly int _rebuildCurveCount;
        private readonly double _tolerance;
        private readonly bool _shrinkSurfaceToEdge;
        private readonly Func<Point3d, Point3d> _morphFunc;

        public MyGeomMorph(
            RhinoDoc doc,
            Func<Point3d, Point3d> morphFunc,
            double tolerance,
            int rebuildFaceUcount,
            int rebuildFaceVcount,
            int rebuildCurveCount,
            bool shrinkSurfaceToEdge)
        {
            _morphFunc = morphFunc;
            _tolerance = tolerance;
            _rebuildFaceUcount = rebuildFaceUcount;
            _rebuildFaceVcount = rebuildFaceVcount;
            _rebuildCurveCount = rebuildCurveCount;
            _shrinkSurfaceToEdge = shrinkSurfaceToEdge;
        }


        /// <summary>
        /// 确保曲面有足够的控制点数量以进行变形
        /// </summary>
        /// <param name="nsrf">输入曲面</param>
        /// <param name="userU">用户指定的重建后U方向控制点数量</param>
        /// <param name="userV">用户指定的重建后V方向控制点数量</param>
        /// <returns>如果当前曲面已经满足控制点数量要求，返回原曲面，否则返回重建后的曲面</returns>
        /// <remarks>重建曲面可能会改变曲面的形状，用户需要根据实际情况调整重建后的控制点数量以平衡变形质量和性能</remarks>
        public static NurbsSurface EnsureSurfaceResolution(
    NurbsSurface nsrf,
    int userU,
    int userV)
        {

            int curU = nsrf.Points.CountU;
            int curV = nsrf.Points.CountV;

            if (curU >= userU && curV >= userV)
                return nsrf;

            NurbsSurface rebuilt = nsrf.Rebuild(3, 3, userU, userV);

            if (rebuilt != null)
                return rebuilt;

            return rebuilt;
        }


        /// <summary>
        /// 找到曲线上的锐点位置，锐点定义为曲线在该位置的连续性为C0但不为C1，即曲线在该位置有一个明显的折角
        /// </summary>
        /// <param name="curve">输入曲线</param>
        /// <returns>锐点位置的参数列表</returns>
        /// <remarks>锐点通常出现在多段曲线的连接处，或者是单段曲线的某些特殊位置，如折线的转折点，或者是某些特殊的Nurbs曲线的控制点位置</remarks>
        /// <remarks>找到这些锐点位置后，可以在变形前对曲线进行切割，避免变形后出现不必要的弧形边</remarks>
        /// <remarks>目前的实现是通过检查曲线的节点矢量来找到锐点位置，适用于Nurbs曲线，如果输入的是其他类型的曲线，可能需要先转换为Nurbs曲线</remarks>
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

                while (
                    i + multiplicity < knots.Count &&
                    Math.Abs(knots[i + multiplicity] - currentKnot) < Rhino.RhinoMath.ZeroTolerance
                )
                {
                    multiplicity++;
                }

                if (multiplicity == degree && currentKnot > domainMin && currentKnot < domainMax)
                {
                    kinkParams.Add(currentKnot);
                }

                i += multiplicity;
            }

            return kinkParams;
        }

        /// <summary>
        /// 确保曲线有足够的控制点数量以进行变形，并且切割掉锐点以防止变形后出现不必要的弧形边
        /// </summary>
        /// <param name="ncrv">输入曲线</param>
        /// <param name="userCount">用户指定的重建后控制点数量</param>
        /// <returns>切割和重建后的曲线数组，如果输入曲线没有锐点且控制点数量已经满足要求，返回原曲线的数组</returns>
        /// <remarks>如果输入曲线有锐点，首先会在锐点位置进行切割，得到多个子曲线，然后对每个子曲线进行重建以满足控制点数量要求，最后返回重建后的曲线数组</remarks>
        public static NurbsCurve[] EnsureCurveResolution(NurbsCurve ncrv, int userCount)
        {   //切割锐点,防止直角边重建后变弧
            var kinkParams = FindKinks(ncrv);
            Curve[] splitCas = ncrv.Split(kinkParams);

            if (splitCas == null || splitCas.Length == 0)
                splitCas = new Curve[] { ncrv };

            NurbsCurve[] newNcrvs = new NurbsCurve[splitCas.Length];

            for (int i = 0; i < splitCas.Length; i++)
            {
                var nsc = splitCas[i].ToNurbsCurve();
                if (nsc.Points.Count < userCount)
                    nsc = nsc.Rebuild(userCount, 3, true);
                newNcrvs[i] = nsc;
            }
            return newNcrvs;
        }

        /// <summary>
        /// 对曲线进行morph变形
        /// </summary> <param name="crv">输入曲线</param>
        /// <param name="new
        /// crvs">输出曲线数组，可能会有多条，特别是当输入曲线有锐点时</param>
        /// <returns>是否成功</returns>
        /// <remarks>会自动切割锐点，并重建曲线以保证足够的控制点数量</remarks>
        bool MorphCurve(Curve crv, out Curve[] newCrvs)
        {
            newCrvs = null;
            var ncrv = crv.ToNurbsCurve();
            NurbsCurve[] newNcrvs = null;
            //如果用户指定了重建后的控制点数量，则进行切割和重建，否则直接变形
            if (_rebuildCurveCount > 0)
                newNcrvs = EnsureCurveResolution(ncrv, _rebuildCurveCount);
            else
                newNcrvs = new NurbsCurve[] { ncrv };

            if (newNcrvs == null || newNcrvs.Length == 0)
                return false;

            List<Curve> morphedCurves = new List<Curve>();

            foreach (var nc in newNcrvs)
            {
                for (int i = 0; i < nc.Points.Count; i++)
                {
                    var cp = nc.Points[i];
                    var newCp = _morphFunc(cp.Location);
                    if (newCp.IsValid && newCp != Point3d.Unset)
                        nc.Points.SetPoint(i, newCp);
                }
                morphedCurves.Add(nc);
            }

            if (morphedCurves.Count == 0)
                return false;
            newCrvs = Curve.JoinCurves(morphedCurves);
            if (newCrvs == null || newCrvs.Length == 0)
                return false;
            else
                return true;

        }



        /// <summary>
        /// 对Brep进行morph变形
        /// </summary>
        /// <param name="bp">输入Brep</param>
        /// <param name="brepJoinTolerance">Brep Join的容差，过大可能导致不必要的面合并，过小可能导致无法合并</param>
        /// <param name="newBreps">输出Brep数组，可能会有多条，特别是当输入Brep的面较多且复杂时</param>
        /// <returns>是否成功</returns>
        /// <remarks>会对每个面进行变形，然后尝试合并，用户可以通过调整重建后的控制点数量来平衡变形质量和性能</remarks>
        /// <remarks>目前的实现是单线程的，如果Brep面数较多且复杂，可能会比较慢，可以考虑使用Parallel.For进行并行处理</remarks>
        bool MorphBrep(Brep bp, double brepJoinTolerance, out Brep[] newBreps)
        {
            newBreps = null;
            var singleFaceBreps = bp.Faces.Select(bf => bf.DuplicateFace(false)).ToArray();
            foreach (var sfb in singleFaceBreps)
            {
                BrepFace face = sfb.Faces[0];
                if (_shrinkSurfaceToEdge)
                    face.ShrinkSurfaceToEdge();//收缩到边界，避免重建后曲面过大导致变形不准确
                NurbsSurface ns = face.ToNurbsSurface();
                if (_rebuildFaceUcount > 0 && _rebuildFaceVcount > 0)
                    ns = EnsureSurfaceResolution(ns, _rebuildFaceUcount, _rebuildFaceVcount);
                for (int u = 0; u < ns.Points.CountU; u++)
                {
                    for (int v = 0; v < ns.Points.CountV; v++)
                    {
                        var cp = ns.Points.GetControlPoint(u, v);
                        var newCp = _morphFunc(cp.Location);
                        if (newCp.IsValid && newCp != Point3d.Unset)
                            ns.Points.SetPoint(u, v, newCp);
                    }
                }
                int newindex = sfb.AddSurface(ns);
                sfb.Faces[0].ChangeSurface(newindex);
                sfb.Faces[0].RebuildEdges(_tolerance, true, true);
                sfb.Standardize();
                sfb.Compact();

            }
            //尝试join
            try
            {
                newBreps = Brep.JoinBreps(singleFaceBreps, brepJoinTolerance);
                if (newBreps == null || newBreps.Length == 0)
                    newBreps = singleFaceBreps; //如果join失败，返回单面Brep数组
                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 对Brep进行morph变形，使用Parallel.For进行并行处理
        /// </summary>
        /// <param name="bp">输入Brep</param>
        /// <param name="brepJoinTolerance">Brep Join的容差，过大可能导致不必要的面合并，过小可能导致无法合并</param>
        /// <param name="newBreps">输出Brep数组，可能会有多条，特别是当输入Brep的面较多且复杂时</param>
        /// <returns>是否成功</returns>
        /// <remarks>会对每个面进行变形，然后尝试合并，用户可以通过调整重建后的控制点数量来平衡变形质量和性能</remarks>
        bool MorphBrepParallel(Brep bp, double brepJoinTolerance, out Brep[] newBreps)
        {
            newBreps = null;

            var singleFaceBreps = bp.Faces
                .Select(bf => bf.DuplicateFace(false))
                .ToArray();

            Parallel.For(0, singleFaceBreps.Length, i =>
            {
                var sfb = singleFaceBreps[i];
                var  face = sfb.Faces[0];
                if (_shrinkSurfaceToEdge)
                    face.ShrinkSurfaceToEdge();//收缩到边界，避免重建后曲面过大导致变形不准确
                NurbsSurface ns = face.ToNurbsSurface();
                if (_rebuildFaceUcount > 0 && _rebuildFaceVcount > 0)
                    ns = EnsureSurfaceResolution(ns, _rebuildFaceUcount, _rebuildFaceVcount);

                for (int u = 0; u < ns.Points.CountU; u++)
                {
                    for (int v = 0; v < ns.Points.CountV; v++)
                    {
                        var cp = ns.Points.GetControlPoint(u, v);
                        var newCp = _morphFunc(cp.Location);
                        if (newCp.IsValid && newCp != Point3d.Unset)
                            ns.Points.SetPoint(u, v, newCp);
                    }
                }

                int newindex = sfb.AddSurface(ns);

                sfb.Faces[0].ChangeSurface(newindex);

                sfb.Faces[0].RebuildEdges(_tolerance, true, true);

                sfb.Standardize();
                sfb.Compact();
            });

            try
            {
                newBreps = Brep.JoinBreps(singleFaceBreps, brepJoinTolerance);
                if (newBreps == null || newBreps.Length == 0)
                    newBreps = singleFaceBreps; //如果join失败，返回单面Brep数组
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 对Mesh进行morph变形
        /// </summary>
        /// <param name="mesh">输入Mesh</param>
        /// <param name="newMesh">输出Mesh</param>
        /// <returns>是否成功</returns>
        /// <remarks>直接修改输入的Mesh可能会有问题，先复制一份，避免影响原始数据</remarks>
        /// <remarks>Mesh的变形相对简单，只需要修改顶点位置，不涉及拓扑结构的改变，所以性能应该比较好</remarks>
        bool MorphMesh(Mesh mesh, out Mesh newMesh)
        {
            mesh = mesh.DuplicateMesh();//直接修改输入的Mesh可能会有问题，先复制一份，避免影响原始数据
            newMesh = null;
            var vertices = mesh.Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var newV = _morphFunc(v);
                if (newV.IsValid && newV != Point3d.Unset)
                    vertices.SetVertex(i, newV);
            }
            newMesh = mesh;
            return newMesh.IsValid;
        }

        /// <summary>
        /// 对SubD进行morph变形
        /// </summary>
        /// <param name="subd"></param>
        /// <param name="newSubD"></param>
        /// <returns></returns>
        bool MorphSubD(SubD subd, out SubD newSubD)
        {
            newSubD = null;

            if (subd == null || _morphFunc == null)
                return false;

            // 复制一份
            newSubD = subd.Duplicate() as SubD;

            for (int i = 0; i < newSubD.Vertices.Count; i++)
            {
                SubDVertex sv = newSubD.Vertices.Find(i);
                Point3d v = sv.ControlNetPoint;
                Point3d newV = _morphFunc(v);
                sv.ControlNetPoint = newV;//为了兼容7，直接修改控制点位置, 8 版本后可以考虑
                // call vertex.SetControlNetPoint(position, false) for all the vertices you want to modify, then call subd.ClearEvaluationCache()
            }

            return newSubD.IsValid;
        }

        public bool MorphGeometry(GeometryBase geom, out GeometryBase[] newGeoms)
        {
            newGeoms = null;
            if (geom == null || !geom.IsValid) return false;

            if (geom is Brep bp)
            {
                if (MorphBrepParallel(bp, _tolerance, out Brep[] newBreps))
                {
                    newGeoms = newBreps;
                    return true;
                }
            }
            else if (geom is Curve crv)
            {
                if (MorphCurve(crv, out Curve[] newCrvs))
                {
                    newGeoms = newCrvs;
                    return true;
                }
            }
            else if (geom is Mesh mesh)
            {
                if (MorphMesh(mesh, out Mesh newMesh))
                {
                    newGeoms = new GeometryBase[] { newMesh };
                    return true;
                }
            }
            else if (geom is SubD subd)
            {
                if (MorphSubD(subd, out SubD newSubD))
                {
                    newGeoms = new GeometryBase[] { newSubD };
                    return true;
                }
            }
            else if (geom is Surface srf)
            {
                //对于一般的Surface，先尝试转换为NurbsSurface，如果成功则进行变形，否则返回失败
                var nsrf = srf.ToNurbsSurface();
                if (nsrf != null)
                {
                    Brep tempBrep = Brep.CreateFromSurface(nsrf);
                    if (MorphBrep(tempBrep, _tolerance, out Brep[] newBreps))
                    {
                        newGeoms = newBreps.Select(b => b.Faces[0].DuplicateFace(false)).ToArray();
                        return true;
                    }
                }
            }

            else if (geom is Point pt)
            {
                var newPt = _morphFunc(pt.Location);
                if (newPt.IsValid && newPt != Point3d.Unset)
                {
                    newGeoms = new GeometryBase[] { new Point(newPt) };
                    return true;
                }
            }

            return false;
        }

    }
}