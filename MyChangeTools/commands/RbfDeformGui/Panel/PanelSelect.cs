using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Commands;
using Rhino.Input.Custom;
using System.Collections.Generic;
using Rhino.Geometry;
using System;
using System.Linq;
namespace MyChangeTools.commands.RbfDeformGui
{
    public partial class RbfDeformPanel : Panel
    {
        // At class level (fields) – initialize them here or in constructor
        private readonly ListBox objListBox = new ListBox { Height = 110 };
        private readonly ListBox baseListBox = new ListBox { Height = 110 };
        private readonly ListBox targetListBox = new ListBox { Height = 110 };
        private readonly ListBox limitedListBox = new ListBox { Height = 110 };

        Label moveDirectionlabel;
        ComboBox moveComboBox;
        Button pickMoveButton;

        // 内部列表（用于实时管理，Config 保持原 ObjRef[] 类型）
        private readonly List<ObjRef> _objRfs = new List<ObjRef>();
        private readonly List<ObjRef> _baseRfs = new List<ObjRef>();
        private readonly List<ObjRef> _targetRfs = new List<ObjRef>();
        private readonly List<ObjRef> _limitedRfs = new List<ObjRef>();


        Config Config => ConfigManager.Current;

        GroupBox CreateSelectionGroup()
        {
            var selectionLayout = new DynamicLayout
            {
                //Padding = 5,
                Spacing = new Size(5, 5),
            };

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

            //1 row
            selectionLayout.AddSeparateRow(4, new Size(5, 5), true, false, new Control[] { moveDirectionlabel, moveComboBox, pickMoveButton });

            moveComboBox.SelectedIndexChanged += (s, e) => OnMovePresetSelected();
            pickMoveButton.Click += (s, e) => SelectMoveDirection();


            StackLayout cob = CreateCategorySection(
                "Deform Objects",
                objListBox,
                _objRfs,
                AddSelectObjects,
                 () => Config.ObjRfs = _objRfs.ToArray());
            StackLayout cbb = CreateCategorySection(
                "Base Objects",
                baseListBox,
                _baseRfs,
                AddSelectBase,
                () => Config.BaseObjRfs = _baseRfs.ToArray());
            StackLayout ctb = CreateCategorySection(
                "Target Objects",
                targetListBox,
                _targetRfs,
                AddSelectTarget,
                () => Config.TargetObjRfs = _targetRfs.ToArray());
            StackLayout clb = CreateCategorySection("Limit Objects", limitedListBox, _limitedRfs, AddSelectLimited, () => Config.LimitedObjRfs = _limitedRfs.ToArray());

            //2 row
            var row3 = selectionLayout.
                AddSeparateRow(null, new Size(5, 5), true, false, new Control[] { cob, cbb, ctb, clb });


            selectionLayout.SizeChanged += (s, e) =>
            {
                var maxwidth = selectionLayout.Width;
                var unitwidth = (int)(maxwidth / 4.0 - 5);
                objListBox.Width = unitwidth;
                baseListBox.Width = unitwidth;
                targetListBox.Width = unitwidth;
                limitedListBox.Width = unitwidth;
            };

            return new GroupBox
            {
                Text = "Object Selection",
                Content = selectionLayout
            };
        }

        private StackLayout CreateCategorySection(
     string title,
     ListBox listBox,
     List<ObjRef> refList,
     Action selectAction,
     Action updateConfig)
        {
            var section = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            section.Items.Add(new Label
            {
                Text = title,
                //Font = new Eto.Drawing.Font(SystemFont.Bold)
            });
            //鼠标单击事件
            listBox.MouseDown += (s, e) =>
            {
                if (e.Buttons == MouseButtons.Primary)
                    HighlightObject(refList, listBox.SelectedIndex);
                //updateConfig?.Invoke();
            };
            //索引改变事件
            listBox.SelectedIndexChanged += (s, e) =>
            {

                HighlightObject(refList, listBox.SelectedIndex);
                //updateConfig?.Invoke();
            };
            //双击事件删除
            listBox.MouseDoubleClick += (s, e) =>
            {
                int idx = listBox.SelectedIndex;
                if (idx < 0 || idx >= refList.Count) return;
                refList.RemoveAt(idx);
                RefreshListBox(listBox, refList);
                updateConfig?.Invoke();
                MessageToast.Instance.Hide();
            };

            var selectBtn = new Button { Text = "Select" };
            selectBtn.Click += (s, e) => selectAction?.Invoke();


            //清理操作
            var clearBtn = new Button { Text = "Clear" };
            clearBtn.Click += (s, e) =>
            {
                listBox.Items.Clear();
                refList.Clear();
                updateConfig?.Invoke();
            };
            section.Items.Add(listBox);
            section.Items.Add(selectBtn);
            section.Items.Add(clearBtn);
            return section;
        }
        private string GetDisplayName(ObjRef orf, int index)
        {
            if (orf == null) return $"{index + 1}. Unknown";
            var ro = orf.Object();
            if (ro == null) return $"{index + 1}. Unknown";
            string name = string.IsNullOrEmpty(ro.Name) ? ro.ObjectType.ToString() : ro.Name;
            return $"{index + 1}.{name}";
        }

        private void RefreshListBox(ListBox lb, List<ObjRef> refs)
        {
            lb.Items.Clear();
            for (int i = 0; i < refs.Count; i++)
            {
                lb.Items.Add(new ListItem { Text = GetDisplayName(refs[i], i) });
            }
        }

        private void HighlightObject(List<ObjRef> refs, int index)
        {
            if (index < 0 || index >= refs.Count) return;
            Config.Doc.Objects.UnselectAll();
            if (!Config.Doc.Objects.Select(refs[index].ObjectId))
                MessageToast.Instance.ShowMessage($"List Item {index + 1} is a invaild obj, please remove it");
            Config.Doc.Views.Redraw();
        }

        void AddSelectObjects()
        {
            var types = ObjectType.AnyObject;
            var rc = Selection.SelectGeometries(RhinoDoc.ActiveDoc, "Select objects to deform", types, out ObjRef[] objs);
            if (rc != Result.Success) return;
            _objRfs.AddRange(objs.Where(obj => !_objRfs.Any(x => x.ObjectId == obj.ObjectId)));
            Config.ObjRfs = _objRfs.ToArray();
            RefreshListBox(objListBox, _objRfs);
        }
        void AddSelectBase()
        {
            var types = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;
            var rc = Selection.SelectGeometries(RhinoDoc.ActiveDoc, "Select base objects", types, out ObjRef[] objs);
            if (rc != Result.Success) return;
            _baseRfs.AddRange(objs.Where(obj => !_baseRfs.Any(x => x.ObjectId == obj.ObjectId)));
            Config.BaseObjRfs = _baseRfs.ToArray();
            RefreshListBox(baseListBox, _baseRfs);
        }

        void AddSelectTarget()
        {
            var types = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;
            var rc = Selection.SelectGeometries(RhinoDoc.ActiveDoc, "Select target objects", types, out ObjRef[] objs);
            if (rc != Result.Success) return;
            _targetRfs.AddRange(objs.Where(obj => !_targetRfs.Any(x => x.ObjectId == obj.ObjectId)));
            Config.TargetObjRfs = _targetRfs.ToArray();
            RefreshListBox(targetListBox, _targetRfs);
        }

        void AddSelectLimited()
        {
            var types = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;
            var rc = Selection.SelectGeometries(RhinoDoc.ActiveDoc, "Select limited objects", types, out ObjRef[] objs);
            if (rc != Result.Success) return;
            // _limitedRfs.Clear();
            _limitedRfs.AddRange(objs.Where(obj => !_limitedRfs.Any(x => x.ObjectId == obj.ObjectId)));
            Config.LimitedObjRfs = _limitedRfs.ToArray();
            RefreshListBox(limitedListBox, _limitedRfs);
        }

        //两点向量
        void SelectMoveDirection()
        {
            var gp = new GetPoint();
            gp.SetCommandPrompt("Pick two points to define move direction");

            gp.Get();
            if (gp.CommandResult() != Result.Success) return;

            var p1 = gp.Point();

            gp.SetCommandPrompt("Second point");
            gp.Get();
            if (gp.CommandResult() != Result.Success) return;

            var p2 = gp.Point();
            var dir = p2 - p1;

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