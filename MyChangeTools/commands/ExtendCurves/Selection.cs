using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;

namespace MyChangeTools.commands.ExtendCurves
{
    internal class ExtendOptions
    {
        public double ExtendLength { get; set; } = 20.0;
        public int ExtendMethod { get; set; } = 0; // 0=Line,1=Arc,2=Smooth
        public bool DeleteInput { get; set; } = true;
    }

    internal class Selection
    {

        public static ExtendOptions extendOptions = new ExtendOptions();

        public static Result GetCurves(out ObjRef[] curves)
        {
            var go = new GetObject();
            go.SetCommandPrompt("选择要延伸的曲线");
            go.GeometryFilter = ObjectType.Curve;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);
            go.GroupSelect = true;

            curves = null;

            OptionDouble opExtendLength = new OptionDouble(extendOptions.ExtendLength);

            OptionInteger opExtendMethod = new OptionInteger(extendOptions.ExtendMethod, lowerLimit:0,upperLimit:2);

            OptionToggle opDeleteInput = new OptionToggle(extendOptions.DeleteInput, "No", "Yes");

            go.AddOptionDouble("extendLength", ref opExtendLength,"延伸长度选择");

            go.AddOptionInteger("extendMethod", ref opExtendMethod, prompt: "延伸方式(0直线 1圆弧  2smooth)");

            go.AddOptionToggle("deleteInput", ref opDeleteInput);

            while (true)
            {
                go.EnableClearObjectsOnEntry(false);
                var rc = go.GetMultiple(1, 0);

                if (rc == GetResult.Option)
                {

                    extendOptions.ExtendLength = opExtendLength.CurrentValue;
                    extendOptions.ExtendMethod = opExtendMethod.CurrentValue; ;
                    extendOptions.DeleteInput = opDeleteInput.CurrentValue; ;
                    RhinoApp.WriteLine($"extendLength{extendOptions.ExtendLength},opExtendMethod{extendOptions.ExtendMethod},deleteInput:{extendOptions.DeleteInput}");
                    continue;
                }
                else if (rc == GetResult.Object)
                {
                    break;
                }
                else if (rc == GetResult.Cancel)
                {
                    return Result.Cancel;
                }

            }
            if (go.ObjectCount < 1)
            {
                return Result.Nothing;
            }

            curves = go.Objects();

            return Result.Success;

        }

        
    }
}
