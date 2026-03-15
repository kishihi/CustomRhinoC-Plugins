using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyChangeTools.commands.RemoveCurveOverlaps
{


    // 待处理的曲线保存id
    class WaitProcessCurve
    {
        public Guid id { get; set; }
        public Rhino.DocObjects.ObjectAttributes attr { get; set; }
        public Curve curve { get; set; }
        public WaitProcessCurve(Guid id, Rhino.DocObjects.ObjectAttributes attr, Curve curve)
        {
            this.id = id;
            this.attr = attr;
            this.curve = curve;
        }
    }
    //处理后的曲线保存属性
    class ProcessedCurve
    {
        public List<Curve> curves { get; set; } = new List<Curve>();
        public Rhino.DocObjects.ObjectAttributes attr { get; set; }
    }

    public class RemoveCurveOverlaps : Command
    {
        public RemoveCurveOverlaps()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RemoveCurveOverlaps Instance { get; private set; }

        public override string EnglishName => "RemoveCurveOverlaps";

        private readonly int ReparameterizeUpperInt = 100;

        Curve ReparameterizeCurve(Curve c, bool toFit = false)
        {
            // c=c.Reparameterize();
            Curve ca = c.ToNurbsCurve();
            ca.Domain = new Interval(0, ReparameterizeUpperInt);
            if (toFit)
                ca = ca.Fit(ca.Degree, 0, 0);
            return ca;
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.

            var go = new Rhino.Input.Custom.GetObject();
            go.GroupSelect = true;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);
            go.SetCommandPrompt("select curves that need remove overlaps");
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Curve;

            var res = go.GetMultiple(1, 0);
            if (res != Rhino.Input.GetResult.Object) return Result.Failure;
            var curverfs = go.Objects().Where(o => o != null && o.Object() != null).ToList();

            double intersection_tolerance = 0.01;
            double overlap_tolerance = 0.01;
            double small_tolerance = 0.01;
            double join_tolerance = 0.01;

            var goOpt = new Rhino.Input.Custom.GetOption();
            goOpt.SetCommandPrompt("设置容差参数 (Enter确认)");

            var optIntersection = new Rhino.Input.Custom.OptionDouble(intersection_tolerance, 0, 1e6);
            var optOverlap = new Rhino.Input.Custom.OptionDouble(overlap_tolerance, 0, 1e6);
            var optSmall = new Rhino.Input.Custom.OptionDouble(small_tolerance, 0, 1e6);
            var optJoin = new Rhino.Input.Custom.OptionDouble(join_tolerance, 0, 1e6);

            goOpt.AddOptionDouble("IntersectionTolerance", ref optIntersection);
            goOpt.AddOptionDouble("OverlapTolerance", ref optOverlap);
            goOpt.AddOptionDouble("SmallTolerance", ref optSmall);
            goOpt.AddOptionDouble("JoinTolerance", ref optJoin);

            using (var escHandler = new Mylib.CommandHandler.EscapeKeyEventHandler("（按 ESC 取消）"))
            {
                while (true)
                {
                    var resOpt = goOpt.Get();

                    if (escHandler.EscapeKeyPressed)
                    {
                        RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
                        return Result.Cancel;
                    }

                    if (resOpt == Rhino.Input.GetResult.Option)
                        continue;

                    if (resOpt == Rhino.Input.GetResult.Nothing)
                        break;

                    if (resOpt == Rhino.Input.GetResult.Cancel)
                        break;
                }
            }

            intersection_tolerance = optIntersection.CurrentValue;
            overlap_tolerance = optOverlap.CurrentValue;
            small_tolerance = optSmall.CurrentValue;
            join_tolerance = optJoin.CurrentValue;

            RhinoApp.WriteLine($"Intersection Tolerance: {intersection_tolerance}");
            RhinoApp.WriteLine($"Overlap Tolerance: {overlap_tolerance}");
            RhinoApp.WriteLine($"Small Tolerance: {small_tolerance}");
            RhinoApp.WriteLine($"Join Tolerance: {join_tolerance}");

            //descending
            curverfs.Sort((a, b) => b.Curve().GetLength().CompareTo(a.Curve().GetLength()));


            //通过字典联系最开始的曲线和处理后的曲线们和属性
            var processDic = new Dictionary<Guid, ProcessedCurve>();

            // var newCurverfs = new List<Rhino.DocObjects.ObjRef>();
            var waitProcessCurves = new List<WaitProcessCurve>();

            //split
            for (int i = 0; i < curverfs.Count(); i++)
            {
                // Reparameterize
                Curve ca = curverfs[i].Curve();
                ca.EnsurePrivateCopy();
                ca = ReparameterizeCurve(ca);

                var cabox = ca.GetBoundingBox(true);

                var splitTs = new HashSet<double>();

                var icsSelf =
                    Rhino.Geometry
                    .Intersect.Intersection
                    .CurveSelf(ca, overlap_tolerance);

                foreach (var ic in icsSelf)
                {
                    if (ic.IsPoint) continue;
                    if (ic.IsOverlap)
                    {
                        var oa = ic.OverlapA;
                        splitTs.Add(oa.T0);
                        splitTs.Add(oa.T1);
                    }
                }

                for (int ii = 0; ii < curverfs.Count(); ii++)
                {
                    //skip original. itself
                    if (i == ii) continue;
                    Curve cb = curverfs[ii].Curve();


                    //bbox相交过滤
                    var cbbox = cb.GetBoundingBox(true);
                    var bbi = BoundingBox.Intersection(cbbox, cabox);
                    if (!bbi.IsValid) { continue; }

                    // cb=ReparameterizeCurve(cb);
                    var ics =
                    Rhino.Geometry
                    .Intersect.Intersection
                    .CurveCurve(ca, cb, intersection_tolerance, overlap_tolerance);

                    foreach (var ic in ics)
                    {
                        if (ic.IsPoint) continue;
                        if (ic.IsOverlap)
                        {
                            var oa = ic.OverlapA;
                            splitTs.Add(oa.T0);
                            splitTs.Add(oa.T1);
                        }
                    }
                }

                Curve[] splitCas = ca.Split(splitTs);

                if (splitCas == null || splitCas.Length == 0)
                {
                    splitCas = new Curve[] { ca };
                }

                //过滤
                var splitCas2 = splitCas
                .Where(c => c != null && c.GetLength() > small_tolerance).ToList();

                //如果没有分割,添加自身
                if (splitCas2.Count() == 0)
                {
                    splitCas2.Add(ca);
                }


                var oldattr = doc.Objects.Find(curverfs[i].ObjectId).Attributes.Duplicate();
                var oldid = curverfs[i].ObjectId;
                foreach (var ci in splitCas2)
                {
                    var wpc = new WaitProcessCurve(oldid, oldattr, ci);
                    waitProcessCurves.Add(wpc);
                }
                doc.Objects.Delete(oldid, true);

            }


            int originalSegCount = waitProcessCurves.Count();
            int lastAddSegCount = 0;

            //incresing
            waitProcessCurves.Sort((a, b) => a.curve.GetLength().CompareTo(b.curve.GetLength()));

            for (int i = 0; i < waitProcessCurves.Count(); i++)
            {
                // Reparameterize
                Curve ca = waitProcessCurves[i].curve.DuplicateCurve();
                ca.EnsurePrivateCopy();
                ca = ReparameterizeCurve(ca);

                var caLength = ca.GetLength();
                var cabox = ca.GetBoundingBox(true);

                var allTrimedInterval = new List<Interval>();

                for (int ii = 0; ii < waitProcessCurves.Count(); ii++)
                {
                    //skip original. itself
                    if (i == ii) continue;
                    if (ca == null) break;
                    if (!ca.IsValid) break;
                    if (waitProcessCurves[ii].curve == null) continue;
                    Curve cb = waitProcessCurves[ii].curve;
                    if (!cb.IsValid) continue;

                    //bbox相交过滤
                    
                    var cbbox = cb.GetBoundingBox(true);
                    var bbi = BoundingBox.Intersection(cbbox, cabox);
                    if (!bbi.IsValid) { continue; }
 
                    var cbLength = cb.GetLength();

                    //重叠度计算                                      

                    bool rc = Curve.GetDistancesBetweenCurves(ca, cb, overlap_tolerance, out double maxDistance, out double maxDistanceParameterA, out double maxDistanceParameterB, out double minDistance, out double minDistanceParameterA, out double minDistanceParameterB);
                    if (maxDistance <= overlap_tolerance && System.Math.Abs(caLength - cbLength) <= small_tolerance) //两条曲线几乎一样
                    {
                        allTrimedInterval.Add(ca.Domain);
                        ca = null;//ca需要被完全剪掉,后面没有必要继续了
                        break;
                    }

                    var ics =
                    Rhino.Geometry
                    .Intersect.Intersection
                    .CurveCurve(ca, cb, intersection_tolerance, overlap_tolerance);
                    foreach (var ic in ics)
                    {
                        if (ic.IsPoint) continue;
                        if (ic.IsOverlap)
                        {
                            var oa = ic.OverlapA;
                            allTrimedInterval.Add(oa);
                        }
                    }
                }
                if (ca != null)
                {
                    double alltrimedlenth = 0.0;
                    foreach (var it in allTrimedInterval)
                    {
                        var itl = ca.GetLength(it);
                        alltrimedlenth += itl;
                    }
                    if (alltrimedlenth >= caLength || System.Math.Abs(alltrimedlenth - caLength) <= small_tolerance)
                    {
                        ca = null;//ca要剪掉的距离等于自己长度,全被减去
                    }
                    else
                    {
                        foreach (var it in allTrimedInterval)
                        {
                            if (ca == null || !ca.IsValid) break;
                            ca = ca.Trim(it.T1, it.T0);
                            if (ca.GetLength() < small_tolerance)
                            {
                                ca = null;
                                break;
                            }
                        }
                    }
                }

                // renew
                //有可能ca完全被裁剪掉了
                if (ca == null || !ca.IsValid)
                {
                    waitProcessCurves[i].curve = null;
                }
                else
                {
                    if (processDic.ContainsKey(waitProcessCurves[i].id))
                    {
                        processDic[waitProcessCurves[i].id].curves.Add(ca);
                    }
                    else
                    {
                        var pcdc = new ProcessedCurve();
                        pcdc.curves.Add(ca);
                        pcdc.attr = waitProcessCurves[i].attr;
                        processDic[waitProcessCurves[i].id] = pcdc;
                    }

                    lastAddSegCount++;
                }

            }

            foreach (var kvp in processDic)
            {
                var originalId = kvp.Key;
                var segments =
                kvp.Value.curves
                .Where(s => s != null && s != null && s.IsValid).ToList();
                // 尝试 Join
                var joinedCurves =
                Curve.JoinCurves(
                    segments, join_tolerance, true);  // preserveDirection = true
                foreach (var c in joinedCurves)
                {
                    var newid = doc.Objects.Add(c);
                    var newo = doc.Objects.Find(newid);
                    newo.Attributes = kvp.Value.attr;
                    newo.CommitChanges();
                }
            }

            RhinoApp.WriteLine($"Removed {originalSegCount - lastAddSegCount} overlaps");

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}