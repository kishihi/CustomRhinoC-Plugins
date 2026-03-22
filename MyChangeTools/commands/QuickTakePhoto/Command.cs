using Rhino;
using Rhino.Commands;

namespace MyChangeTools.commands.QuickTakePhoto
{ 
    public class QuickTakePhoto : Command
    {
        public QuickTakePhoto()
        {
            Instance=this;
        }
        public static QuickTakePhoto Instance { get; private set; }

         static QuickTakePhotoForm _form;

        public override string EnglishName => "QuickTakePhoto";
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (_form == null)
            {
               _form = new QuickTakePhotoForm(doc);
               _form.Closed += (s, e) =>
               {
                   _form = null;
               };
               _form.Topmost = true;

            }
            _form.Visible = true;
            _form.Show();
            _form.BringToFront();
            return Result.Success;
        }
    }
}