using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Commands;

namespace MyChangeTools.ExtendCurves
{
    internal class CurveProcess
    {

        public static Result Extend(Rhino.RhinoDoc doc, ObjRef[] curves, ExtendOptions extendOptions)
        {
            if (curves == null || curves.Length == 0)
            {
                RhinoApp.WriteLine("未选择任何曲线。");
                return Result.Nothing;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (ObjRef crvRef in curves)
            {
                Curve crv = crvRef.Curve();
                if (crv == null || !crv.IsValid)
                {
                    failCount++;
                    continue;
                }


                CurveExtensionStyle style;

                switch (extendOptions.ExtendMethod)
                {
                    case 0:
                        style = CurveExtensionStyle.Line;
                        break;
                    case 1:
                        style = CurveExtensionStyle.Arc;
                        break;
                    case 2:
                        style = CurveExtensionStyle.Smooth;
                        break;
                    default:
                        style = CurveExtensionStyle.Line; // fallback
                        break;
                }



                // 对两端都延伸
                Curve newCrv = crv.Extend(CurveEnd.Both, extendOptions.ExtendLength, style);

                if (newCrv != null && newCrv.IsValid)
                {
                    // 添加到文档
                    var newCrvid = doc.Objects.AddCurve(newCrv);
                    //doc.
                    //
                    successCount++;

                    var oldattr = doc.Objects.Find(crvRef.ObjectId).Attributes.Duplicate();

                    var newo = doc.Objects.Find(newCrvid);

                    newo.Attributes = oldattr;

                    newo.CommitChanges();


                    if (extendOptions.DeleteInput)

                        doc.Objects.Delete(crvRef.ObjectId, true);

                }
                else
                {
                    failCount++;
                }
                doc.Views.Redraw();
            }

            RhinoApp.WriteLine($"Success extend curve: {successCount} , fail : {failCount}");


            return Result.Success;
        }
    }
}
