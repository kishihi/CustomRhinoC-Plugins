//using Rhino.Commands;
//using Rhino.DocObjects;
//using Rhino.Geometry;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace MyChangeTools.LoftPlus
//{
//    //1
//    //用户选择了很多条曲线
//    //按颜色区分成多层
//    //每层中每条曲线都寻找下一层中离他最近的曲线,最后得到一组曲线
//    //同理,可以得到多组放样组曲线




//    //2
//    //处理一组多条曲线的放样


//    internal class CurveProcess
//    {


//        public static Result LoftCurves(ObjRef[] curveRefs)
//        {

//            //List<Curve> curves = curveRefs.Where(o => o != null).Select(o => o.Geometry() as Curve).ToList();
//            List<Curve> curves = curveRefs
//    .Select(o => o?.Geometry())
//    .OfType<Curve>()
//    .Where(c => c.GetLength() > 0.01)
//    .ToList();


//            var breps = Brep.CreateFromLoft(curves, Point3d.Unset, Point3d.Unset, LoftType.Tight, false);

//            return Result.Success;
//        }
//    }
//}
