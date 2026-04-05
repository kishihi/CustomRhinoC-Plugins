using Eto.Forms;
using Eto.Drawing;
using Rhino;
using System.Linq;
using Rhino.Geometry;
using Rhino.Commands;
namespace MyChangeTools.commands.RbfDeformGui
{

    public partial class RbfDeformPanel : Panel
    {
        
        GroupBox CreateSampleGroup()
        {
            var curveByParamCheckBox = new CheckBox
            {
                Text = "Curve Sample By Parameter",
                Checked = Config.SampleConfig.CurveSampleByParameter
            };

            curveByParamCheckBox.CheckedChanged += (s, e) =>
            {
                Config.SampleConfig.CurveSampleByParameter =
                    curveByParamCheckBox.Checked ?? false;
            };

            var matchMeshCheckBox = new CheckBox
            {
                Text = "Match Mesh By Coordinate",
                Checked = Config.SampleConfig.MatchMeshByCoordinate
            };

            matchMeshCheckBox.CheckedChanged += (s, e) =>
            {
                Config.SampleConfig.MatchMeshByCoordinate =
                    matchMeshCheckBox.Checked ?? false;
            };

            var curveSampleDistanceStepper = new NumericStepper
            {
                DecimalPlaces = 2,
                Increment = 0.1,
                MinValue = 0,
                MaxValue = 10000,
                Value = Config.SampleConfig.CurveSampleDistance
            };

            curveSampleDistanceStepper.ValueChanged += (s, e) =>
            {
                Config.SampleConfig.CurveSampleDistance =
                    (double)curveSampleDistanceStepper.Value;
            };

            // Surface Sample Method
            var surfaceSampleMethodstr = RBFLib.Deform.SurfaceMappingMethod
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToList();

            var surfaceSampleMethodDropDown = new DropDown
            {
                DataStore = surfaceSampleMethodstr,
                SelectedIndex = Config.SampleConfig.SurfaceSampleMethod
            };

            //根据选择的采样方式启用或禁用Pick Direction按钮
            var SurfaceSampleDirectionPickButton = new Button
            {
                Text = "Pick Direction",
                Enabled = surfaceSampleMethodDropDown.SelectedIndex == 5
            };
            //选择采样方式为TwoPtDefineDirection时，启用按钮，允许用户选择方向
            surfaceSampleMethodDropDown.SelectedIndexChanged += (s, e) =>
            {
                RhinoApp.WriteLine($"set Surface Sample Method : {RBFLib.Deform.SurfaceMappingMethod[surfaceSampleMethodDropDown.SelectedIndex]}");

                Config.SampleConfig.SurfaceSampleMethod = surfaceSampleMethodDropDown.SelectedIndex;

                SurfaceSampleDirectionPickButton.Enabled = surfaceSampleMethodDropDown.SelectedIndex == 5;
            };

            SurfaceSampleDirectionPickButton.Click += (s, e) =>
            {
                SelectSurfaceSampleDirection();
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow(curveByParamCheckBox);
            layout.AddRow(matchMeshCheckBox);
            layout.AddRow("Curve Sample Distance", curveSampleDistanceStepper);
            layout.AddRow("Surface Sample Method", surfaceSampleMethodDropDown, SurfaceSampleDirectionPickButton);

            return new GroupBox
            {
                Text = "Sampling",
                Content = layout
            };
        }

        void SelectSurfaceSampleDirection()
        {
            
            var dir = Selection.GetTwoPtVector(out Vector3d dirResult) == Result.Success ? dirResult : Vector3d.Unset;

            if (dir.IsValid)
            {
                Config.SampleConfig.SurfaceSampleDirection = dir;
                RhinoApp.WriteLine("Successfully set Surface Sample Direction");
            }
            else
            {
                MessageBox.Show("Invalid direction. Please try again.");
            }
        }

    }
}