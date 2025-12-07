using System;
using Rhino;
using Rhino.Commands;

namespace MyRhinoSelectTools.Commands.SelArc
{
    public class SelArc : Command
    {

        public override string EnglishName => "SelArc";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.

            var rs = Sel.GetAllArc(doc, out var arcs);
            if (rs != Result.Success)
            {
                return Result.Failure;
            }
            rs = Sel.GetOption();
            if (rs != Result.Success)
            {
                return Result.Failure;
            }

            if (Sel.SelOptions.LabelRadius) LabelArc.RunLabelArc(doc, arcs);


            return Result.Success;
        }
    }
}