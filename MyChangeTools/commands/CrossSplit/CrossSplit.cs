//using Rhino;
//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using Rhino.Geometry.Intersect;
//using Rhino.Input.Custom;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace MyChangeTools.commands.CrossSplit
//{
//    public class CrossSplit : Command
//    {

//        public override string EnglishName => "CrossSplit";

        

//        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
//        {
//            // TODO: complete command.
//            var rc = Mylib.GeometryUtils.SelectGeometries(doc, "选择多条曲线", ObjectType.Curve, out ObjRef[] curverfs);

//            CurveIntersections intersectCurvesEvents;

//            //intersectCurvesEvents = Intersection.CurveCurve()

//            var carlist = curverfs.ToList();
//            var pairs =
//    from i in Enumerable.Range(0, carlist.Count)
//    from j in Enumerable.Range(i + 1, carlist.Count - (i + 1))
//    select (carlist[i], carlist[j]);

//            foreach (var pair in pairs)
//            {
//                Curve c1 = pair.Item1.Curve();
//                Curve c2 = pair.Item2.Curve();
//                intersectCurvesEvents = Intersection.CurveCurve(c1,c2,doc.ModelAbsoluteTolerance, doc.ModelAbsoluteTolerance);

//                //intersectCurvesEvents


//            }




//            return Result.Success;
//        }
//    }
//}