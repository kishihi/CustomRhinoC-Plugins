using Eto.Forms;
using Eto.Drawing;
namespace MyChangeTools.commands.RbfDeformGui
{

    public partial class RbfDeformPanel : Panel
    {
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
    }
}