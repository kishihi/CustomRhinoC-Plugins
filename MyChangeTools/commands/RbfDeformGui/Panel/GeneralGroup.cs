using Eto.Forms;
using Eto.Drawing;
using Rhino;
namespace MyChangeTools.commands.RbfDeformGui
{
    [System.Runtime.InteropServices.Guid("ab9c4611-3768-4d0e-8fee-f3a3fb346544")]
    public partial class RbfDeformPanel : Panel
    {
        public RbfDeformPanel(RhinoDoc doc)
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
            layout.Add(CreateLastMoveGroup());
            layout.Add(CreateObjectSelectionGroup());
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
    }
}