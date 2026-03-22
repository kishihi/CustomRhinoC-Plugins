using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MyChangeTools.commands.QuickTakePhoto
{
    public partial class QuickTakePhotoForm : Form
    {
        private readonly Rhino.RhinoDoc _doc;

        private RhinoObject[] SelectedObjs
        => _doc.Objects.GetSelectedObjects(true, false)
        .ToArray();

        private RhinoObject[] AllGeometrySelectedObjs
        => SelectedObjs
        .Where(t => t.ObjectType != ObjectType.Annotation
        && t.ObjectType != ObjectType.Light
        ).ToArray();

        private BoundingBox AllGeometryBox =>
        Mylib.GeometryUtils.GetAllBox(AllGeometrySelectedObjs.Select(g => g.Geometry).ToList());

        private static Func<Point3d, Transform> GetBottomRotateTransform = (center) =>
        {
            return Transform.Rotation(Math.PI / 2, Vector3d.ZAxis, center)
                * Transform.Rotation(Math.PI, Vector3d.YAxis, center);
        };

        private static Func<Point3d, Transform> GetLeftRotateTransform = (center) =>
        {
            var rotateYTransform = Transform.Rotation(Math.PI / 2, Vector3d.YAxis, center);
            var rotateZTransform = Transform.Rotation(Math.PI / 2, Vector3d.ZAxis, center);
            var rotateTransform = rotateZTransform * rotateYTransform;
            return rotateTransform;
        };

        private static Func<Point3d, Transform> GetRightRotateTransform = (center) =>
        {
            var rotateYTransform = Transform.Rotation(-Math.PI / 2, Vector3d.YAxis, center);
            var rotateZTransform = Transform.Rotation(-Math.PI / 2, Vector3d.ZAxis, center);
            var rotateTransform = rotateZTransform * rotateYTransform;
            return rotateTransform;
        };

        private Func<Point3d, Transform> GetCabinetRotateTransform = (center) =>
        {
            var rotX = Transform.Rotation(Math.PI * _cabinetXangle / 180, Vector3d.XAxis, center);
            var rotY = Transform.Rotation(Math.PI * _cabinetYangle / 180, Vector3d.YAxis, center);
            var rotZ = Transform.Rotation(Math.PI * _cabinetZangle / 180, Vector3d.ZAxis, center);
            var rotateTransform = rotZ * rotY * rotX * GetLeftRotateTransform(center);
            return rotateTransform;
        };

        private bool PreCherkParms()
        {  

            if (SelectedObjs.Any(t => !t.IsValid) || AllGeometrySelectedObjs.Length == 0 || !AllGeometryBox.IsValid)
            {
                MessageBox.Show($"Please Select Valid Geometries.",
                    "Error", MessageBoxButtons.OK, MessageBoxType.Error);

                // RhinoApp.WriteLine($"SelectedObjs {SelectedObjs.Count()}");
                // foreach(var ro in SelectedObjs)
                // {
                //     RhinoApp.WriteLine(ro.ObjectType.ToString());
                // }
                return false;
            }
            return true;
        }

        // private Vector3d AllBox
        public static (double x, double y, double z) GetWDH(BoundingBox bbox)
        {
            double x_len = bbox.Max.X - bbox.Min.X;
            double y_len = bbox.Max.Y - bbox.Min.Y;
            double z_len = bbox.Max.Z - bbox.Min.Z;

            return (x_len, y_len, z_len);
        }



        private async Task<(GeometryBase[] ProcessedGs, RhinoObject[] objs)> BtnBottom_Click(object sender = null, EventArgs e = null)
        {
            var objs = AllGeometrySelectedObjs;
            var allBox = AllGeometryBox;
            var center = allBox.Center;

            var ProcessedGs = await Task.Run(() =>
            {
                var Rotatedresult = new GeometryBase[objs.Length];

                var rotateTransform =
                GetBottomRotateTransform(center);

                Parallel.For(0, objs.Length, i =>
                {
                    var geo = objs[i].Geometry.Duplicate();

                    geo.Transform(rotateTransform);

                    Rotatedresult[i] = geo;
                });

                var box2 = Mylib.GeometryUtils.GetAllBox(Rotatedresult.ToList());
                var (width, depth, height) = GetWDH(AllGeometryBox);

                var cs1 = allBox.GetCorners();
                var cs2 = box2.GetCorners();

                var offset_vect1 = cs1[0] - cs2[0];
                var offset_vect2 = -Vector3d.XAxis * width * 3;
                var offset = offset_vect1 + offset_vect2;

                Parallel.For(0, Rotatedresult.Length, i =>
                {
                    Rotatedresult[i].Translate(offset);
                });

                return Rotatedresult;
            });

            return (ProcessedGs, objs);

        }

        private async Task<(GeometryBase[] ProcessedGs, RhinoObject[] objs)> BtnLeft_Click(object sender = null, System.EventArgs e = null)
        {
            var objs = SelectedObjs; // 包括标注
            var allBox = AllGeometryBox;
            var center = allBox.Center;
            var (width, depth, height) = GetWDH(AllGeometryBox);
            var ProcessedGs = await Task.Run(() =>
                {
                    var Rotatedresult = new GeometryBase[objs.Length];
                    var rotateTransform = GetLeftRotateTransform(center);
                    Parallel.For(0, objs.Length, i =>
                    {
                        // RhinoApp.WriteLine($"{objs[i]}, {objs[i].ObjectType.ToString()}");
                        var geo = objs[i].Geometry.Duplicate();
                        geo.Transform(rotateTransform);
                        Rotatedresult[i] = geo;
                    });

                    // 計算新的包圍盒
                    var box2 = Mylib.GeometryUtils.GetAllBox(Rotatedresult.Where(t => t.ObjectType != ObjectType.Annotation).ToList());

                    var cs1 = allBox.GetCorners();
                    var cs2 = box2.GetCorners();

                    var offset_vect1 = cs1[0] - cs2[0];
                    var offset_vect2 = -Vector3d.XAxis * width * 3;
                    var offset_vect3 = Vector3d.YAxis * width * 2;
                    var resultoffset = offset_vect1 + offset_vect2 + offset_vect3;

                    Parallel.For(0, Rotatedresult.Length, i =>
                    {
                        Rotatedresult[i].Translate(resultoffset);
                    });

                    return Rotatedresult;
                }
            );
            return (ProcessedGs, objs);
        }

        private async Task<(GeometryBase[] ProcessedGs, RhinoObject[] objs)> BtnRight_Click(object sender = null, System.EventArgs e = null)
        {
            var objs = SelectedObjs; // 包括标注
            var allBox = AllGeometryBox;
            var center = allBox.Center;
            var (width, depth, height) = GetWDH(AllGeometryBox);

            var ProcessedGs = await Task.Run(() =>
                {
                    var Rotatedresult = new GeometryBase[objs.Length];
                    var rotateTransform = GetRightRotateTransform(center);

                    Parallel.For(0, objs.Length, i =>
                    {
                        var geo = objs[i].Geometry.Duplicate();
                        geo.Transform(rotateTransform);
                        Rotatedresult[i] = geo;
                    });

                    // 計算新的包圍盒
                    var box2 = Mylib.GeometryUtils.GetAllBox(Rotatedresult.Where(t => t.ObjectType != ObjectType.Annotation).ToList());


                    var cs1 = allBox.GetCorners();
                    var cs2 = box2.GetCorners();

                    var offset_vect1 = cs1[0] - cs2[0];
                    var offset_vect2 = -Vector3d.XAxis * width * 3;
                    var offset_vect3 = Vector3d.YAxis * width * 3;
                    var resultoffset = offset_vect1 + offset_vect2 + offset_vect3;

                    Parallel.For(0, Rotatedresult.Length, i =>
                    {
                        Rotatedresult[i].Translate(resultoffset);
                    });

                    return Rotatedresult;
                }
            );
            return (ProcessedGs, objs);
        }

        private async Task<(GeometryBase[] ProcessedGs, RhinoObject[] objs)> BtnCabinet_Click(object sender = null, System.EventArgs e = null)
        {
            var objs = AllGeometrySelectedObjs;
            var allBox = AllGeometryBox;
            var center = allBox.Center;
            var (width, depth, height) = GetWDH(AllGeometryBox);

            var ProcessedGs = await Task.Run(() =>
                {
                    var Rotatedresult = new GeometryBase[objs.Length];
                    var rotateTransform = GetCabinetRotateTransform(center);

                    Parallel.For(0, objs.Length, i =>
                    {
                        var geo = objs[i].Geometry.Duplicate();
                        geo.Transform(rotateTransform);
                        Rotatedresult[i] = geo;
                    });

                    // 計算新的包圍盒
                    var box2 = Mylib.GeometryUtils.GetAllBox(Rotatedresult.ToList());


                    var cs1 = allBox.GetCorners();
                    var cs2 = box2.GetCorners();

                    var offset_vect1 = cs1[0] - cs2[0];
                    var offset_vect2 = -Vector3d.XAxis * width * 3;
                    var offset_vect3 = Vector3d.YAxis * 4 * width;
                    var resultoffset = offset_vect1 + offset_vect2 + offset_vect3;

                    Parallel.For(0, Rotatedresult.Length, i =>
                    {
                        Rotatedresult[i].Translate(resultoffset);
                    });

                    return Rotatedresult;
                }
            );
            return (ProcessedGs, objs);
        }


    }
}