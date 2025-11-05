using Rhino;
using System;

namespace MyChangeTools.Mylib
{
    public class CommandHandler
    {
        public class EscapeKeyEventHandler : IDisposable
        {
            private bool _escapePressed;
            public EscapeKeyEventHandler(string message = "")
            {
                RhinoApp.EscapeKeyPressed += OnEscapeKeyPressed;
                if (!string.IsNullOrEmpty(message))
                    RhinoApp.WriteLine(message);
            }
            private void OnEscapeKeyPressed(object sender, EventArgs e)
            {
                _escapePressed = true;
            }

            public bool EscapeKeyPressed => _escapePressed;


            //离开 using 时 → 自动注销事件。
            public void Dispose()
            {
                RhinoApp.EscapeKeyPressed -= OnEscapeKeyPressed;
            }
        }
    }
}
