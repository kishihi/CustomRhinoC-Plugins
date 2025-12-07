using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
namespace MyRhinoSelectTools.Commands.SelectAboveSurface2
{

    internal class SurfaceCompute
    {

        public static Result ComputeDirection(
    RhinoDoc doc,
    ObjRef baseBrepRef,
    ObjRef[] objRefs,
    out ConcurrentBag<Guid> abovedObjIds,
    out ConcurrentBag<Guid> belowObjIds,
    out ConcurrentBag<Guid> onObjids,
    out ConcurrentBag<(Point3d pt, string text)> textDots)
        {
            Brep baseBrep = CustomQuickClass.QuickGeometry.ToBrepSafe(baseBrepRef.Geometry());
            double tol = doc.ModelAbsoluteTolerance;

            // 临时局部变量，不是 out 参数
            var _abovedObjIds = new ConcurrentBag<Guid>();
            var _belowObjIds = new ConcurrentBag<Guid>();
            var _onObjids = new ConcurrentBag<Guid>();
            var _textDots = new ConcurrentBag<(Point3d pt, string text)>();

            if (baseBrep == null || baseBrep.Faces.Count == 0)
            {
                abovedObjIds = _abovedObjIds;
                belowObjIds = _belowObjIds;
                onObjids = _onObjids;
                textDots = _textDots;
                return Result.Failure;
            }

            Parallel.ForEach(objRefs, objRef =>
            {
                if (objRef.ObjectId == baseBrepRef.ObjectId)
                    return;

                var geom = objRef.Geometry();
                if (geom == null)
                    return;

                //pts
                int aboveCount = 0;
                int belowCount = 0;
                int onCount = 0;
                int sampleNumber = 5;


                foreach (var samplept in PopulateGeom.PopulateGeom3D(geom, sampleNumber))

                {
                    // _textDots.Add((samplept, $""));
                    baseBrep.ClosestPoint(samplept, out Point3d pInBrepCloest, out ComponentIndex ci, out double s, out double t, double.MaxValue, out Vector3d normal);

                    if (samplept == Point3d.Unset || pInBrepCloest == Point3d.Unset)
                        return;

                    Vector3d vec = samplept - pInBrepCloest;
                    if (vec.IsTiny())
                    {
                        _onObjids.Add(objRef.ObjectId);
                        return;
                    }

                    vec.Unitize();
                    normal.Unitize();

                    double dot = vec * normal;
                    if (dot > 0)
                        aboveCount++;
                    else
                        belowCount++;
                }

                int[] values = { aboveCount, belowCount, onCount };
                int maxCount = values.Max();
                if (aboveCount == maxCount || aboveCount > belowCount) 
                    _abovedObjIds.Add(objRef.ObjectId);
                else if (belowCount == maxCount || belowCount > aboveCount) 
                    _belowObjIds.Add(objRef.ObjectId);
                else if (aboveCount == belowCount && aboveCount == 0 && onCount > 0) 
                    _onObjids.Add(objRef.ObjectId);



            });

            // 最后再赋值给 out 参数
            abovedObjIds = _abovedObjIds;
            belowObjIds = _belowObjIds;
            onObjids = _onObjids;
            textDots = _textDots;

            return Result.Success;
        }
    }
}
