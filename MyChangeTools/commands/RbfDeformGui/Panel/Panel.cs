using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Commands;
using Rhino.Input.Custom;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace MyChangeTools.commands.RbfDeformGui
{
    [System.Runtime.InteropServices.Guid("9fdd9b4a-5eee-4196-a9b0-30a4a4138240")]
    public class RbfDeformPanel : Panel
    {
        Label objLabel;
        Label baseLabel;
        Label targetLabel;
        Label limitedLabel;

        Config Config => ConfigManager.Current;

        public RbfDeformPanel(RhinoDoc doc)
        {   
            Config.Doc = doc;
            Content = new Scrollable
            {
                Content = CreateLayout()
            };
        }

        Control CreateLayout()
        {
            var layout = new DynamicLayout
            {
                Padding = 10,
                Spacing = new Size(10, 10)
            };

            // layout.MinimumSize = new Size(300, 400);

            layout.Add(CreateSelectionGroup());
            layout.Add(CreateGeneralGroup());
            layout.Add(CreateSpaceMorphGroup());
            layout.Add(CreateMyGeomMorphGroup());
            layout.Add(CreateSampleGroup());
            layout.Add(CreateRbfGroup());
            layout.Add(CreateProcessButtons());

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
            };


            // is use UseCustomMorph CheckBox
            var useCustomMorph = new CheckBox
            {
                Text = "UseCustomMorph",
                Checked = Config.UseCustomMorph

            };
            useCustomMorph.CheckedChanged += (s, e) =>
            {
                Config.UseCustomMorph = copy.Checked ?? false;
            };

            var layout = new DynamicLayout();

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

        Control CreateProcessButtons()
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
            // 2. 启动后台任务
            Task.Run(() =>
            {
                var processor = new GeometryProcessorSync(
                    Config.Doc,
                    Config.ObjRfs,
                    Config.BaseObjRfs,
                    Config.TargetObjRfs,
                    Config.LimitedObjRfs,
                    Config.MoveVectors,
                    Config);
                return processor.ProcessSync();
            })
            // 3. 指定回到主线程执行后续操作
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    // 主线程处理异常
                    // MessageBox.Show($"错误：{task.Exception?.InnerException?.Message}");
                    return;
                }

                // 主线程处理 smgs 结果
                List<MorphedGeom> smgs = task.Result;
                ProcessSmgsInMainThread(smgs);

                // 恢复UI
                // btnProcess.IsEnabled = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void ProcessSmgsInMainThread(List<MorphedGeom> smgs)
        {
            List<Guid> newIds = new List<Guid>(smgs.Count);

            foreach (var mg in smgs)
            {
                foreach (var g in mg.GeometryBases)
                {

                    //添加对象同时把属性加过去
                    Guid id = Config.Doc.Objects.Add(g, mg.Attributes.Duplicate());

                    if (id != Guid.Empty)
                        newIds.Add(id);
                }
            }

            Config.Doc.Views.Redraw();
        }
    }
}