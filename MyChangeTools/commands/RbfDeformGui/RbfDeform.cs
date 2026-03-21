using Rhino;
using Rhino.Commands;
using Eto.Forms;
using Eto.Drawing;
using Rhino.UI;
namespace MyChangeTools.commands.RbfDeformGui
{

    public class MyRbfDeformForm : Form
    {
        public MyRbfDeformForm(RhinoDoc doc)
        {

            Title = "RbfDeform";
            Content = new RbfDeformPanel(doc);
            Width = 450;
            Height = 400;
            Closing += (sender, e) =>
            {
                e.Cancel = true;
                this.Visible = false;
            };
        }
    }

    public class RbfDeform : Rhino.Commands.Command
    {
        public RbfDeform()
        {
            Instance = this;
        }

        //static MyRbfDeformForm _form;
        public static RbfDeform Instance { get; private set; }

        public override string EnglishName => "RbfDeformGui";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            //if (_form == null)
            //{
            //    _form = new MyRbfDeformForm(doc);
            //    _form.Closed += (s, e) =>
            //    {
            //        _form = null;
            //    };
            //    _form.Topmost = true;

            //}
            //_form.Visible = true;
            //_form.Show();
            //_form.BringToFront();



            var panel_id = typeof(RbfDeformPanel).GUID;
            Panels.OpenPanel(panel_id);

            // if (mode == RunMode.Interactive)
            // {
            //     Panels.OpenPanel(panel_id);
            //     return Result.Success;
            // }

            var panel_visible = Panels.IsPanelVisible(panel_id);

            if (panel_visible)
                RhinoApp.WriteLine("Successfully Open RbfDeformPanel");
            else
                RhinoApp.WriteLine("Fail to Open RbfDeformPanel");


            //var panel = Rhino.UI.Panels.GetPanel<RbfDeformPanel>(doc);

            return Result.Success;
        }
    }
}

// var panel_id = typeof(RbfDeformPanel).GUID;
// Panels.OpenPanel(panel_id);

// // if (mode == RunMode.Interactive)
// // {
// //     Panels.OpenPanel(panel_id);
// //     return Result.Success;
// // }

// var panel_visible = Panels.IsPanelVisible(panel_id);

// if(panel_visible)
//     RhinoApp.WriteLine("Successfully Open RbfDeformPanel");
// else
//      RhinoApp.WriteLine("Fail to Open RbfDeformPanel");


// var panel = Rhino.UI.Panels.GetPanel<RbfDeformPanel>(doc);

// if (panel != null)
// {
//     panel.MinimumSize = new Eto.Drawing.Size(200, 400);
// }