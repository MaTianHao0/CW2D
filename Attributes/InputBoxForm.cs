using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace CW2D
{
    public class InputBoxForm : Form
    {
        private TextBox txtInput;
        private Button btnOk;
        private Button btnCancel;

        /// <summary>
        /// 获取用户在文本框中输入的值。
        /// </summary>
        public string UserInput { get; private set; }

        public InputBoxForm(string title, string prompt)
        {
            InitializeComponent();
            this.Text = title;
            this.Controls.OfType<Label>().First().Text = prompt;
        }

        private void InitializeComponent()
        {
            // --- 控件定义 ---
            var lblPrompt = new Label();
            txtInput = new TextBox();
            btnOk = new Button();
            btnCancel = new Button();

            // --- 窗口设置 ---
            this.ClientSize = new Size(380, 110);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "输入";
            this.ControlBox = false; // 隐藏关闭、最大化、最小化按钮

            // --- 提示标签 (Label) ---
            lblPrompt.Location = new Point(12, 15);
            lblPrompt.Size = new Size(356, 23);
            lblPrompt.Text = "提示信息";

            // --- 输入框 (TextBox) ---
            txtInput.Location = new Point(15, 45);
            txtInput.Size = new Size(353, 20);
            txtInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // --- 确定按钮 (OK Button) ---
            btnOk.Location = new Point(212, 80);
            btnOk.Size = new Size(75, 23);
            btnOk.Text = "确定";
            btnOk.DialogResult = DialogResult.OK; // 设置后，点击会自动关闭窗口并返回OK
            btnOk.Click += (sender, e) => {
                this.UserInput = txtInput.Text; // 保存用户输入
                this.Close();
            };

            // --- 取消按钮 (Cancel Button) ---
            btnCancel.Location = new Point(293, 80);
            btnCancel.Size = new Size(75, 23);
            btnCancel.Text = "取消";
            btnCancel.DialogResult = DialogResult.Cancel; // 点击会自动关闭并返回Cancel
            btnCancel.Click += (sender, e) => { this.Close(); };

            // --- 将控件添加到窗口 ---
            this.Controls.Add(lblPrompt);
            this.Controls.Add(txtInput);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;   // 按回车键时触发确定按钮
            this.CancelButton = btnCancel; // 按Esc键时触发取消按钮
        }
    }
}