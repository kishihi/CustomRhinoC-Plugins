using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using System.Collections.Generic;

namespace MyChangeTools.commands.AutoDistinctColor
{
    public class AutoDistinctColor : Command
    {
        public override string EnglishName => "AutoDistinctColor";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.
            // 让用户选择对象（支持预选）
            var go = new GetObject();
            go.SetCommandPrompt("选择任意对象");
            go.GroupSelect = true;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);

            var res = go.GetMultiple(1, 0);
            if (res == Rhino.Input.GetResult.Cancel) return Result.Cancel;

            var selObjs = new List<RhinoObject>();
            for (int i = 0; i < go.ObjectCount; i++)
                selObjs.Add(go.Object(i).Object());

            if (selObjs.Count == 0)
            {
                RhinoApp.WriteLine("没有选择对象。");
                return Result.Nothing;
            }
            else
            {
                RhinoApp.WriteLine($"选中{selObjs.Count}个对象");
            }

            Process.AutoDistinctColor(doc, selObjs);
            doc.Views.Redraw();
            return Result.Success;
        }
    }
    }
