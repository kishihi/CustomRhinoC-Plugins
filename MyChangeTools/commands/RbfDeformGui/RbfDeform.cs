using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;
using MyChangeTools.commands.RbfDeform;
using Rhino.UI;
namespace MyChangeTools.commands.RbfDeformGui
{
    
    public class RbfDeform : Command
    {
        public RbfDeform()
        {
            Instance = this;
        }

        public static RbfDeform Instance { get; private set; }

        public override string EnglishName => "RbfDeformGui";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            

            var panel_id = typeof(RbfDeformPanel).GUID;
            Panels.OpenPanel(panel_id);

            // if (mode == RunMode.Interactive)
            // {
            //     Panels.OpenPanel(panel_id);
            //     return Result.Success;
            // }

            var panel_visible = Panels.IsPanelVisible(panel_id);

            if(panel_visible)
                RhinoApp.WriteLine("Successfully Open RbfDeformPanel");
            else
                 RhinoApp.WriteLine("Fail to Open RbfDeformPanel");

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}