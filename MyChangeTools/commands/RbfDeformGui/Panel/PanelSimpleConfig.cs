using Eto.Forms;
using Eto.Drawing;
using Rhino;
using System.Collections.Generic;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui
{

    [System.Runtime.InteropServices.Guid("ab9c4611-3768-4d0e-8fee-f3a3fb346544")]
    public partial class RbfDeformPanel : Panel
    {
        public RbfDeformPanel(Rhino.RhinoDoc doc)
        {
            

            Config.Doc = doc;

            // 配置区（可滚动）
            var scroll = new Scrollable
            {
                Content = CreateSettingsLayout(),
                ExpandContentWidth = true
            };

            var root = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5),
                //Width = 490,
            };

            root.Add(CreateProcessButtons()); // 固定按钮
            root.Add(scroll, yscale: true);   // 滚动区域

            Content = root;
        }

        Control CreateSettingsLayout()
        {
            var layout = new DynamicLayout
            {
                Spacing = new Size(5, 5)
            };
            layout.Add(CreateSelectionGroup());
            layout.Add(CreateGeneralGroup());
            layout.Add(CreateSpaceMorphGroup());
            layout.Add(CreateMyGeomMorphGroup());
            layout.Add(CreateSampleGroup());
            layout.Add(CreateRbfGroup());
            layout.Add(null);
            return layout;
        }
        GroupBox CreateGeneralGroup()
        {

            // tolerance Stepper
            var tolerance = new NumericStepper
            {
                DecimalPlaces = 4,
                Increment = 0.001,
                Value = Config.Tolerance
            };

            tolerance.ValueChanged += (s, e) =>
            {
                Config.Tolerance = (double)tolerance.Value;
            };

            //is copy object CheckBox
            var copy = new CheckBox
            {
                Text = "Copy Object",
                Checked = Config.IsCopy
            };
            copy.CheckedChanged += (s, e) =>
            {
                Config.IsCopy = copy.Checked ?? false;
                RhinoApp.WriteLine($"Config.IsCopy:{Config.IsCopy}");
            };


            // is use UseCustomMorph CheckBox
            var useCustomMorph = new CheckBox
            {
                Text = "UseCustomMorph",
                Checked = Config.IsUseCustomMorph

            };
            useCustomMorph.CheckedChanged += (s, e) =>
            {
                Config.IsUseCustomMorph = useCustomMorph.Checked ?? false;
                RhinoApp.WriteLine($"Config.IsUseCustomMorph:{Config.IsUseCustomMorph}");
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow("Tolerance", tolerance);
            layout.AddRow(copy);
            layout.AddRow(useCustomMorph);

            return new GroupBox
            {
                Text = "General",
                Content = layout
            };
        }

        //SpaceMorphConfig
        GroupBox CreateSpaceMorphGroup()
        {
            var preserveStructure = new CheckBox
            {
                Text = "Preserve Structure",
                Checked = Config.SpaceMorphConfig.PreserveStructure
            };

            preserveStructure.CheckedChanged += (s, e) =>
            {
                Config.SpaceMorphConfig.PreserveStructure =
                    preserveStructure.Checked ?? false;
            };

            var quickPreview = new CheckBox
            {
                Text = "Quick Preview",
                Checked = Config.SpaceMorphConfig.QuickPreview
            };

            quickPreview.CheckedChanged += (s, e) =>
            {
                Config.SpaceMorphConfig.QuickPreview =
                    quickPreview.Checked ?? false;
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow(preserveStructure);
            layout.AddRow(quickPreview);

            return new GroupBox
            {
                Text = "Space Morph",
                Content = layout
            };
        }

        GroupBox CreateMyGeomMorphGroup()
        {
            var shrinkSurface = new CheckBox
            {
                Text = "Shrink Surface To Edge",
                Checked = Config.MyGeomMorphConfig.ShrinkSurfaceToEdge
            };

            shrinkSurface.CheckedChanged += (s, e) =>
            {
                Config.MyGeomMorphConfig.ShrinkSurfaceToEdge =
                    shrinkSurface.Checked ?? false;
            };

            var rebuildU = new NumericStepper
            {
                MinValue = 0,
                MaxValue = 1000,
                Value = Config.MyGeomMorphConfig.RebuildFaceUCount
            };

            rebuildU.ValueChanged += (s, e) =>
            {
                Config.MyGeomMorphConfig.RebuildFaceUCount =
                    (int)rebuildU.Value;
            };

            var rebuildV = new NumericStepper
            {
                MinValue = 0,
                MaxValue = 1000,
                Value = Config.MyGeomMorphConfig.RebuildFaceVCount
            };

            rebuildV.ValueChanged += (s, e) =>
            {
                Config.MyGeomMorphConfig.RebuildFaceVCount =
                    (int)rebuildV.Value;
            };

            var rebuildCurve = new NumericStepper
            {
                MinValue = 0,
                MaxValue = 1000,
                Value = Config.MyGeomMorphConfig.RebuildCurveCount
            };

            rebuildCurve.ValueChanged += (s, e) =>
            {
                Config.MyGeomMorphConfig.RebuildCurveCount =
                    (int)rebuildCurve.Value;
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow(shrinkSurface);
            layout.AddRow("Rebuild Surface U", rebuildU);
            layout.AddRow("Rebuild Surface V", rebuildV);
            layout.AddRow("Rebuild Curve", rebuildCurve);

            return new GroupBox
            {
                Text = "My Geom Morph",
                Content = layout
            };
        }

        GroupBox CreateSampleGroup()
        {
            var curveByParam = new CheckBox
            {
                Text = "Curve Sample By Parameter",
                Checked = Config.SampleConfig.CurveSampleByParameter
            };

            curveByParam.CheckedChanged += (s, e) =>
            {
                Config.SampleConfig.CurveSampleByParameter =
                    curveByParam.Checked ?? false;
            };

            var matchMesh = new CheckBox
            {
                Text = "Match Mesh By Coordinate",
                Checked = Config.SampleConfig.MatchMeshByCoordinate
            };

            matchMesh.CheckedChanged += (s, e) =>
            {
                Config.SampleConfig.MatchMeshByCoordinate =
                    matchMesh.Checked ?? false;
            };

            var sampleDistance = new NumericStepper
            {
                DecimalPlaces = 2,
                Increment = 0.1,
                MinValue = 0,
                MaxValue = 10000,
                Value = Config.SampleConfig.CurveSampleDistance
            };

            sampleDistance.ValueChanged += (s, e) =>
            {
                Config.SampleConfig.CurveSampleDistance =
                    (double)sampleDistance.Value;
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow(curveByParam);
            layout.AddRow(matchMesh);
            layout.AddRow("Curve Sample Distance", sampleDistance);

            return new GroupBox
            {
                Text = "Sampling",
                Content = layout
            };
        }

        GroupBox CreateRbfGroup()
        {
            var linear = new CheckBox
            {
                Text = "Add Linear System",
                Checked = Config.RBFConfig.RBFAddLinearSystem
            };

            linear.CheckedChanged += (s, e) =>
            {
                Config.RBFConfig.RBFAddLinearSystem = linear.Checked ?? false;
            };

            var radius = new NumericStepper
            {
                DecimalPlaces = 2,
                Value = Config.RBFConfig.InfectRadius
            };

            radius.ValueChanged += (s, e) =>
            {
                Config.RBFConfig.InfectRadius = (double)radius.Value;
            };

            var phiFuncstrs = Config.PhiIndexFunNameDict
    .OrderBy(x => x.Key)
    .Select(x => x.Value)
    .ToList();

            var phi = new DropDown
            {
                DataStore = phiFuncstrs,
                SelectedIndex = Config.RBFConfig.PhiFunctionID
            };

            phi.SelectedIndexChanged += (s, e) =>
            {
                RhinoApp.WriteLine($"set phi : {Config.PhiIndexFunNameDict[phi.SelectedIndex]}");
                Config.RBFConfig.PhiFunctionID = phi.SelectedIndex;
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow(linear);
            layout.AddRow("Influence Radius", radius);
            layout.AddRow("Phi Function", phi);

            return new GroupBox
            {
                Text = "RBF",
                Content = layout
            };
        }
    }
}