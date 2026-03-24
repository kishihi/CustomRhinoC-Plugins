using Eto.Forms;
using Eto.Drawing;
using System.Collections.Generic;

namespace MyChangeTools.commands.RbfDeformGui
{
    public class MessageToast : Form
    {
        private TextArea _textArea;

        // 静态实例，全局唯一
        private static MessageToast _instance;

        public static MessageToast Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MessageToast();
                return _instance;
            }
        }
        private HashSet<string> addedmessages = new HashSet<string>();

        private MessageToast()
        {
            Title = "提示";
            ClientSize = new Size(300, 150);
            Resizable = false;
            Topmost = true;
            ShowInTaskbar = false;
            BackgroundColor = Colors.LightYellow;

            _textArea = new TextArea
            {
                ReadOnly = true,
                Wrap = true,
                BackgroundColor = Colors.LightYellow,
                TextColor = Colors.Red,
                Font = new Font(FontFamilies.Sans, 12)
            };

            var scroll = new Scrollable { Content = _textArea };

            Content = scroll;

            Shown += (s, e) => CenterOnScreen();
        }

        private void CenterOnScreen()
        {
            var screen = Screen.PrimaryScreen;
            int x = (int)((screen.Bounds.Width - Width) / 2);
            int y = (int)((screen.Bounds.Height - Height) / 2);
            Location = new Point(x, y);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            this.Visible = false; // 隐藏，不销毁
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visible = false;
        }

        /// <summary>
        /// 显示消息，如果窗口没关闭追加一行，如果关闭了就清空重写
        /// </summary>
        public void ShowMessage(string message)
        {
            if (!Visible)
            {
                _textArea.Text = ""; // 窗口关闭过，清空之前消息
                Show(); // 非模态显示
                addedmessages.Clear();
            }

            if (!addedmessages.Contains(message))
            {// 追加消息
                if (!string.IsNullOrEmpty(_textArea.Text))
                    _textArea.Text += "\n";

                _textArea.Text += message;
                addedmessages.Add(message);
            }

            this.Visible = true;
        }
    }
}