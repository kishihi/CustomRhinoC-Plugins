using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.Input;
using System.Collections.Generic;

namespace MyChangeTools.commands.RbfDeformGui
{
    [System.Runtime.InteropServices.Guid("f81ee8d0-ed63-485f-8d53-892e0052aef1")]
    public class RbfDeformPanel : Panel
    {   
        Label objLabel;
        Label baseLabel;
        Label targetLabel;
        Label limitedLabel;

        Config Config => ConfigManager.Current;

        public RbfDeformPanel(RhinoDoc doc)
        {
            Content = CreateLayout();
            Config.Doc = doc;
        }

        Control CreateLayout()
        {
            var layout = new DynamicLayout
            {
                Padding = 10,
                Spacing = new Size(5, 5)
            };

            layout.Add(CreateSelectionGroup());
            layout.Add(CreateGeneralGroup());
            layout.Add(CreateRbfGroup());

            layout.Add(CreateButtons());

            layout.Add(null);

            return new Scrollable { Content = layout };
        }

        GroupBox CreateSelectionGroup()
        {
            objLabel = new Label { Text = "None" };
            baseLabel = new Label { Text = "None" };
            targetLabel = new Label { Text = "None" };
            limitedLabel = new Label { Text = "None" };

            var btnObj = new Button { Text = "Select" };
            btnObj.Click += (s, e) => SelectObjects();

            var btnBase = new Button { Text = "Select" };
            btnBase.Click += (s, e) => SelectBase();

            var btnTarget = new Button { Text = "Select" };
            btnTarget.Click += (s, e) => SelectTarget();

            var btnLimited = new Button { Text = "Select" };
            btnLimited.Click += (s, e) => SelectLimited();

            var btnMove = new Button { Text = "Pick Direction" };
            btnMove.Click += (s, e) => SelectMoveDirection();

            var layout = new DynamicLayout();

            layout.AddRow("Objects to Deform", objLabel, btnObj);
            layout.AddRow("Base Objects", baseLabel, btnBase);
            layout.AddRow("Target Objects", targetLabel, btnTarget);
            layout.AddRow("Limited Objects", limitedLabel, btnLimited);
            layout.AddRow("Move Direction", btnMove);

            return new GroupBox
            {
                Text = "Object Selection",
                Content = layout
            };
        }

        GroupBox CreateGeneralGroup()
        {
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

            var copy = new CheckBox
            {
                Text = "Copy Object",
                Checked = Config.IsCopy
            };

            copy.CheckedChanged += (s, e) =>
            {
                Config.IsCopy = copy.Checked ?? false;
            };

            var layout = new DynamicLayout();

            layout.AddRow("Tolerance", tolerance);
            layout.AddRow(copy);

            return new GroupBox
            {
                Text = "General",
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

            var phi = new DropDown
            {
                DataStore = new[]
                {
                    "TPS",
                    "CSRBFW2",
                },
                SelectedIndex = Config.RBFConfig.PhiFunctionID
            };

            phi.SelectedIndexChanged += (s, e) =>
            {
                Config.RBFConfig.PhiFunctionID = phi.SelectedIndex;
            };

            var layout = new DynamicLayout();

            layout.AddRow(linear);
            layout.AddRow("Influence Radius", radius);
            layout.AddRow("Phi Function", phi);

            return new GroupBox
            {
                Text = "RBF",
                Content = layout
            };
        }

        Control CreateButtons()
        {

            var apply = new Button { Text = "Apply" };
            apply.Click += (s, e) => RunDeform();

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = { apply }
            };
        }

        void SelectObjects()
        {
            var types = ObjectType.AnyObject;

            var rc = Selection.SelectGeometries(
                RhinoDoc.ActiveDoc,
                "Select objects to deform",
                types,
                out ObjRef[] objs);

            if (rc != Result.Success) return;

            Config.ObjRfs = objs;

            objLabel.Text = $"{objs.Length} objects";
        }

        void SelectBase()
        {
            var types = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;

            var rc = Selection.SelectGeometries(
                RhinoDoc.ActiveDoc,
                "Select base objects",
                types,
                out ObjRef[] objs);

            if (rc != Result.Success) return;

            Config.BaseObjRfs = objs;

            baseLabel.Text = $"{objs.Length} objects";
        }

        void SelectTarget()
        {
            var types = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;

            var rc = Selection.SelectGeometries(
                RhinoDoc.ActiveDoc,
                "Select target objects",
                types,
                out ObjRef[] objs);

            if (rc != Result.Success) return;

            Config.TargetObjRfs = objs;

            targetLabel.Text = $"{objs.Length} objects";
        }

        void SelectLimited()
        {
            var types = ObjectType.Curve | ObjectType.Mesh | ObjectType.Point | ObjectType.Surface;

            var rc = Selection.SelectGeometries(
                RhinoDoc.ActiveDoc,
                "Select limited objects",
                types,
                out ObjRef[] objs);

            if (rc != Result.Success) return;

            Config.LimitedObjRfs = objs;

            limitedLabel.Text = $"{objs.Length} objects";
        }

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
                Config.MoveVectors.Add(dir);
            else
                MessageBox.Show("Invalid direction. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
        }


        void RunDeform()
        {
            RhinoApp.WriteLine("Apply deformation...");
            var processor = new GeometryProcessorSync(Config.Doc,
            Config.ObjRfs,
            Config.BaseObjRfs,
            Config.TargetObjRfs,
            Config.LimitedObjRfs,
            Config.MoveVectors,
            Config);
            var rc = processor.ProcessSync();
            // if (rc != Result.Success)
            // {
            //     //弹出错误窗口
            //     MessageBox.Show(
            //     "Deformation failed. Please check the input and try again.",
            //     "Error",
            //     MessageBoxButtons.OK,
            //     MessageBoxType.Error);
            // }
        }
    }
}