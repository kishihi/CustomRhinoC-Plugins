using Rhino;
using Rhino.Commands;
using System.Collections.Generic;

namespace MyRhinoSelectTools.Commands.SelArc
{
    internal class SelOption
    {
        public bool LabelRadius { get; set; } = false;
    }
    internal class Sel
    {
        static public SelOption SelOptions = new SelOption();

        static public Result GetAllArc(RhinoDoc doc, out List<Rhino.DocObjects.RhinoObject> arcs)
        {
            arcs = new List<Rhino.DocObjects.RhinoObject>();

            foreach (var ro in doc.Objects)   // ObjectTable 可直接 foreach
            {
                if (ro?.Geometry is Rhino.Geometry.ArcCurve)
                {
                    arcs.Add(ro);
                }
            }
            if(arcs.Count==0)
            {
                RhinoApp.WriteLine("文档中没有找到弧线对象。");
                return Result.Nothing;
            }
            foreach (var item in arcs)
            {
                doc.Objects.Select(item.Id);
            }

            return Result.Success;
        }

        static public Result GetOption()
        {
            var go = new Rhino.Input.Custom.GetOption();
            go.SetCommandPrompt("Select Arc Options");
            var labelRadiusOption = new Rhino.Input.Custom.OptionToggle(SelOptions.LabelRadius, "No", "Yes");
            int labelRadiusIndex = go.AddOptionToggle("LabelRadius", ref labelRadiusOption);
            using (var escHandler = new MyRhinoSelectTools.CustomQuickClass.QuickGetObject.EscapeKeyEventHandler("选择要计算的对象（按 ESC 取消）"))
            {
                Rhino.Input.GetResult get_rc = go.Get();
                if (escHandler.EscapeKeyPressed)
                {
                    RhinoApp.WriteLine("用户按下 ESC，命令已取消。");
                    return Result.Cancel;
                }
                if (get_rc == Rhino.Input.GetResult.Option)
                {
                    if (go.OptionIndex() == labelRadiusIndex)
                    {
                        SelOptions.LabelRadius = labelRadiusOption.CurrentValue;
                        RhinoApp.Write($"LabelRadius : {SelOptions.LabelRadius}");
                    }
                }
                else if (get_rc == Rhino.Input.GetResult.Cancel)
                {
                    
                }

            }
            return Result.Success;
        }


    }
}
