using Eto.Forms;
using Eto.Drawing;
using Rhino;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui
{

    public partial class RbfDeformPanel : Panel
    {
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

            var phiFuncstrs = RBFLib.RBFPhiFunctions.PhiIndexFunNameDict
    .OrderBy(x => x.Key)
    .Select(x => x.Value)
    .ToList();

            var phiFucDropDown = new DropDown
            {
                DataStore = phiFuncstrs,
                SelectedIndex = Config.RBFConfig.PhiFunctionID
            };

            phiFucDropDown.SelectedIndexChanged += (s, e) =>
            {
                RhinoApp.WriteLine($"set phi : {RBFLib.RBFPhiFunctions.PhiIndexFunNameDict[phiFucDropDown.SelectedIndex]}");
                Config.RBFConfig.PhiFunctionID = phiFucDropDown.SelectedIndex;
            };

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };

            layout.AddRow(linear);
            layout.AddRow("Influence Radius", radius);
            layout.AddRow("Phi Function", phiFucDropDown);

            return new GroupBox
            {
                Text = "RBF",
                Content = layout
            };
        }
    }
}