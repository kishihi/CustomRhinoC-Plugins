using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

// 在 RhinoCommon 中，旋转的 正方向遵循右手法则（Right-Hand Rule）。
// 伸出你的右手
// 大拇指指向旋转轴的正方向（Axis Direction）
// 其余四指卷动方向就是旋转的正向

namespace MyChangeTools.commands.QuickTakePhoto
{

    public partial class QuickTakePhotoForm : Form, INotifyPropertyChanged
    {
        Button btnBottom;
        Button btnLeft;
        Button btnRight;
        Button btnCabinet;
        Button btnAll;

        public event PropertyChangedEventHandler PropertyChanged;

        const int DefaultX = 30;
        const int DefaultY = 45;
        const int DefaultZ = 35;

        public static int _cabinetXangle = DefaultX;
        public static int _cabinetYangle = DefaultY;
        public static int _cabinetZangle = DefaultZ;

        public int CabinetXangle
        {
            get => _cabinetXangle;
            set
            {
                if (_cabinetXangle == value) return;
                _cabinetXangle = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(CabinetXangle)));
            }
        }

        public int CabinetYangle
        {
            get => _cabinetYangle;
            set
            {
                if (_cabinetYangle == value) return;
                _cabinetYangle = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(CabinetYangle)));
            }
        }

        public int CabinetZangle
        {
            get => _cabinetZangle;
            set
            {
                if (_cabinetZangle == value) return;
                _cabinetZangle = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(CabinetZangle)));
            }
        }

        DynamicLayout CanbinetLayout()
        {
            var layout = new DynamicLayout
            {
                Spacing = new Size(5, 5)
            };
            //x
            var cabinetXangleslider = new Slider
            {
                MinValue = -180,
                MaxValue = 180
            };

            cabinetXangleslider.Bind<int>(
                nameof(cabinetXangleslider.Value),
                this,
                nameof(CabinetXangle)
            );

            //y
            var cabinetYangleslider = new Slider
            {
                MinValue = -180,
                MaxValue = 180
            };

            cabinetYangleslider.Bind<int>(
                nameof(cabinetYangleslider.Value),
                this,
                nameof(CabinetYangle)
            );

            //z
            var cabinetZangleslider = new Slider
            {
                MinValue = -180,
                MaxValue = 180
            };

            cabinetZangleslider.Bind<int>(
                nameof(cabinetZangleslider.Value),
                this,
                nameof(CabinetZangle)
            );

            var XangleLanbel = new Label
            {
                Text = DefaultX.ToString(),
            };
            var YangleLanbel = new Label
            {
                Text = DefaultY.ToString(),
            };
            var ZangleLanbel = new Label
            {
                Text = DefaultZ.ToString(),
            };

            cabinetXangleslider.ValueChanged += (s, e) =>
            {
                XangleLanbel.Text = _cabinetXangle.ToString();
            };
            cabinetYangleslider.ValueChanged += (s, e) =>
            {
                YangleLanbel.Text = _cabinetYangle.ToString();
            };
            cabinetZangleslider.ValueChanged += (s, e) =>
            {
                ZangleLanbel.Text = _cabinetZangle.ToString();
            };

            layout.AddRow("CabinetXangle", cabinetXangleslider, XangleLanbel);
            layout.AddRow("CabinetYangle", cabinetYangleslider, YangleLanbel);
            layout.AddRow("CabinetZangle", cabinetZangleslider, ZangleLanbel);

            return layout;
        }

        private void AddProcessedGsToDoc(GeometryBase[] ProcessedGs, RhinoObject[] objs)
        {
            uint undoId = _doc.BeginUndoRecord("QuickTakePhoto");
            RhinoApp.WriteLine($"ProcessedGs {ProcessedGs.Count()} objs {objs.Length}");
            try
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    _doc.Objects.Add(ProcessedGs[i], objs[i].Attributes.Duplicate());
                }
            }
            finally
            {
                _doc.EndUndoRecord(undoId);
            }
            _doc.Views.Redraw();
        }


        void BindAsync(Button btn, Func<Task<(GeometryBase[] ProcessedGs, RhinoObject[] objs)>> asyncHandler)
        {
            btn.Click += async (s, e) =>
            {
                if (!PreCherkParms()) return;

                try
                {
                    // await 并拆包 tuple
                    var (processedGs, objs) = await asyncHandler();
                    AddProcessedGsToDoc(processedGs, objs);
                }
                catch (Exception ex)
                {
                    // 可以打印错误
                    Rhino.RhinoApp.WriteLine(ex.ToString());
                }
            };
        }

        private async Task BtnAll_Click(object sender = null, System.EventArgs e = null)
        {
            if (!PreCherkParms())
                return;

            // 禁用按钮
            btnBottom.Enabled = false;
            btnLeft.Enabled = false;
            btnRight.Enabled = false;
            btnCabinet.Enabled = false;

            try
            {
                // 顺序异步调用每个按钮逻辑，并处理结果
                var (gs1, objs1) = await BtnBottom_Click();
                AddProcessedGsToDoc(gs1, objs1);

                var (gs2, objs2) = await BtnLeft_Click();
                AddProcessedGsToDoc(gs2, objs2);

                var (gs3, objs3) = await BtnRight_Click();
                AddProcessedGsToDoc(gs3, objs3);

                var (gs4, objs4) = await BtnCabinet_Click();
                AddProcessedGsToDoc(gs4, objs4);
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine(ex.ToString());
            }
            finally
            {
                // 恢复按钮
                btnBottom.Enabled = true;
                btnLeft.Enabled = true;
                btnRight.Enabled = true;
                btnCabinet.Enabled = true;
            }
        }



        public QuickTakePhotoForm(Rhino.RhinoDoc doc)
        {
            _doc = doc;
            Title = "QTP";
            Size = new Size(280, 330);
            // Resizable = true;
            var titleLabel = new Label
            {
                Text = "Quick Take Photo",
                Font = new Eto.Drawing.Font(SystemFont.Bold, 12),
                Width = 200,
            };

            btnBottom = new Button { Text = "Bottom", Width = 80 };
            btnLeft = new Button { Text = "Left", Width = 80 };
            btnRight = new Button { Text = "Right", Width = 80 };
            btnCabinet = new Button { Text = "Cabinet", Width = 80 };
            btnAll = new Button { Text = "All" };

            btnAll.Width = 150;


            // 点击事件
            BindAsync(btnBottom, () => BtnBottom_Click());
            BindAsync(btnLeft, () => BtnLeft_Click());
            BindAsync(btnRight, () => BtnRight_Click());
            BindAsync(btnCabinet, () => BtnCabinet_Click());
            btnAll.Click += async (ss, ee) =>
            {
                await BtnAll_Click(ss, ee);
            };
            var layout = new StackLayout
            {
                Orientation = Orientation.Vertical,
                Padding = 10,
                Spacing = 10,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            var row1 = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    btnBottom,
                    btnLeft
                },
            };
            var row2 = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    btnRight,
                    btnCabinet
                }
            };

            var s = CanbinetLayout();


            layout.Items.Add(titleLabel);
            layout.Items.Add(row1);
            layout.Items.Add(row2);
            layout.Items.Add(btnAll);
            // layout.Items.Add("CabinetAngle");
            layout.Items.Add(s);

            var resetBtn = new Button { Text = "Reset Angle", Width = 120 };

            resetBtn.Click += (ss, e) =>
            {
                CabinetXangle = DefaultX;
                CabinetYangle = DefaultY;
                CabinetZangle = DefaultZ;
            };

            layout.Items.Add(resetBtn);

            Content = layout;
        }
    }
}