using Eto.Forms;
using Eto.Drawing;
using Rhino.Commands;
using Rhino.Geometry;
namespace MyChangeTools.commands.RbfDeformGui
{
    public partial class RbfDeformPanel : Panel
    {

        Label moveDirectionlabel;
        ComboBox moveComboBox;
        Button pickMoveButton;
        GroupBox CreateLastMoveGroup()
        {
            moveComboBox = new ComboBox
            {
                Items =
        {
            "Unset",
            "X轴",
            "Y轴",
            "Z轴",
            "XY底面",
            "YZ侧面",
            "XZ前面"
        },
                SelectedIndex = 0,
            };
            moveDirectionlabel = new Label
            {
                Text = "最终移动向量",
                Font = new Eto.Drawing.Font(SystemFont.Bold),
            };
            pickMoveButton = new Button { Text = "两点定义" };

            moveComboBox.SelectedIndexChanged += (s, e) => OnMovePresetSelected();
            pickMoveButton.Click += (s, e) => SelectMoveDirection();

            var layout = new DynamicLayout
            {
                Padding = 5,
                Spacing = new Size(5, 5)
            };
            layout.AddRow(moveDirectionlabel, moveComboBox, pickMoveButton);

            return new GroupBox
            {
                Text = "Last Move",
                Content = layout
            };

        }

        //两点向量
        void SelectMoveDirection()
        {
            var dir = Selection.GetTwoPtVector(out Vector3d vector) == Result.Success ? vector : Vector3d.Unset;

            Config.MoveVectors.Clear();
            if (dir.IsValid)
            {
                Config.MoveVectors.Add(dir);
                moveDirectionlabel.Text = "最终在自定义向量移动";
            }
            else
                MessageBox.Show("Invalid direction. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
        }

        //moveComboBox改变事件
        private void OnMovePresetSelected()
        {
            if (moveComboBox.SelectedIndex < 0) return;
            string option = moveComboBox.Items[moveComboBox.SelectedIndex].Text;
            Config.MoveVectors.Clear();
            Vector3d dir = Vector3d.Unset;
            switch (option)
            {
                case "X轴":
                    dir = Vector3d.XAxis;
                    moveDirectionlabel.Text = "最终移动在X轴";
                    break;
                case "Y轴":
                    dir = Vector3d.YAxis;
                    moveDirectionlabel.Text = "最终移动在Y轴";
                    break;
                case "Z轴":
                    dir = Vector3d.ZAxis;
                    moveDirectionlabel.Text = "最终移动在Z轴";
                    break;
                case "XY底面":
                    Config.MoveVectors.Add(Vector3d.XAxis);
                    Config.MoveVectors.Add(Vector3d.YAxis);
                    moveDirectionlabel.Text = "最终移动在XY底面";
                    return;
                case "YZ侧面":
                    Config.MoveVectors.Add(Vector3d.ZAxis);
                    Config.MoveVectors.Add(Vector3d.YAxis);
                    moveDirectionlabel.Text = "最终移动在YZ侧面";
                    return;
                case "XZ前面":
                    Config.MoveVectors.Add(Vector3d.XAxis);
                    Config.MoveVectors.Add(Vector3d.ZAxis);
                    moveDirectionlabel.Text = "最终移动在XZ前面";
                    return;
                case "Unset":
                    moveDirectionlabel.Text = "Unset (default)";
                    return;
            }
            Config.MoveVectors.Add(dir);
        }

    }
}