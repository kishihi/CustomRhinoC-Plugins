using Eto.Forms;
using Eto.Drawing;
namespace MyChangeTools.commands.RbfDeformGui
{

    public partial class RbfDeformPanel : Panel
    {
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
    }
}