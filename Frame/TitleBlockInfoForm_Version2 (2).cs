using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TitleBlockBattery
{
    /// <summary>
    /// 编辑 TitleFrameInfo（17 字段）的窗体
    /// 满足以下调用契约：
    /// - 构造函数：TitleBlockInfoForm(TitleFrameInfo initial)
    /// - 方法：TitleFrameInfo GetFrameInfo()
    /// </summary>
    public class TitleBlockInfoForm : Form
    {
        private TitleFrameInfo _info; // 内部可编辑副本

        private readonly Dictionary<string, TextBox> _boxes = new Dictionary<string, TextBox>();

        private Button _btnOk;
        private Button _btnCancel;
        private Button _btnToday;
        private Button _btnResetXX;

        public TitleBlockInfoForm(TitleFrameInfo initial)
        {
            // 深拷贝一份，避免在点击“取消”时污染传入对象
            _info = initial != null ? Clone(initial) : new TitleFrameInfo();

            InitializeComponent();
            LoadFromInfo(_info);
        }

        public TitleFrameInfo GetFrameInfo()
        {
            // 将 UI 值写回到 _info 并返回
            SaveToInfo();
            return _info;
        }

        private void InitializeComponent()
        {
            this.Text = "图框信息设置（17字段）";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(720, 520);
            this.MinimumSize = new Size(700, 480);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            var labelFont = new Font("微软雅黑", 9F, FontStyle.Regular);
            var inputFont = new Font("微软雅黑", 9F, FontStyle.Regular);
            var buttonFont = new Font("微软雅黑", 9F, FontStyle.Regular);

            // 使用 TableLayoutPanel 布局：两列（标签+输入），17 行字段，底部工具栏一行
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 19,
                Padding = new Padding(10),
                AutoScroll = true
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 字段定义（顺序与 TitleFrameInfo 一致）
            var fields = new (string Key, string Label)[]
            {
                ("ChiefDesigner",   "设计总负责人"),
                ("Approver",        "审定人"),
                ("Reviewer",        "审核人"),
                ("ProfessionalLead","专业负责人"),
                ("Checker",         "校对人"),
                ("Designer",        "设计人"),
                ("Client",          "建设单位"),
                ("ProjectName",     "工程名称"),
                ("SubProjectName",  "子项名称"),
                ("DrawingName",     "图名"),
                ("ProjectCode",     "工程编号"),
                ("Discipline",      "专业"),
                ("Version",         "版本"),
                ("Phase",           "阶段"),
                ("Date",            "日期"),
                ("DrawingNumber",   "图号"),
                ("Barcode",         "条形码"),
            };

            int row = 0;
            foreach (var f in fields)
            {
                var lbl = new Label
                {
                    Text = f.Label + "：",
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    Dock = DockStyle.Fill,
                    Font = labelFont,
                    Margin = new Padding(0, 6, 6, 6)
                };
                var txt = new TextBox
                {
                    Name = "txt" + f.Key,
                    Dock = DockStyle.Fill,
                    Font = inputFont,
                    Margin = new Padding(0, 4, 0, 4)
                };

                _boxes[f.Key] = txt;

                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                table.Controls.Add(lbl, 0, row);
                table.Controls.Add(txt, 1, row);
                row++;
            }

            // 操作按钮行
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Height = 40,
                AutoSize = true,
                WrapContents = false
            };

            _btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Font = buttonFont,
                AutoSize = true,
                Margin = new Padding(6)
            };
            _btnOk.Click += (s, e) => { SaveToInfo(); this.DialogResult = DialogResult.OK; };

            _btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Font = buttonFont,
                AutoSize = true,
                Margin = new Padding(6)
            };

            _btnToday = new Button
            {
                Text = "填入今天日期",
                Font = buttonFont,
                AutoSize = true,
                Margin = new Padding(6)
            };
            _btnToday.Click += (s, e) =>
            {
                if (_boxes.TryGetValue("Date", out var t))
                    t.Text = DateTime.Now.ToString("yyyy-MM-dd");
            };

            _btnResetXX = new Button
            {
                Text = "重置为“XX”",
                Font = buttonFont,
                AutoSize = true,
                Margin = new Padding(6)
            };
            _btnResetXX.Click += (s, e) =>
            {
                foreach (var tb in _boxes.Values)
                    tb.Text = "XX";
            };

            panel.Controls.Add(_btnOk);
            panel.Controls.Add(_btnCancel);
            panel.Controls.Add(_btnToday);
            panel.Controls.Add(_btnResetXX);

            // 在表格底部添加一个分隔行和按钮行
            var hr = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Height = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 8)
            };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
            table.Controls.Add(hr, 0, row);
            table.SetColumnSpan(hr, 2);
            row++;

            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(panel, 0, row);
            table.SetColumnSpan(panel, 2);

            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            this.Controls.Add(table);
        }

        private void LoadFromInfo(TitleFrameInfo info)
        {
            Set("ChiefDesigner",    info.ChiefDesigner);
            Set("Approver",         info.Approver);
            Set("Reviewer",         info.Reviewer);
            Set("ProfessionalLead", info.ProfessionalLead);
            Set("Checker",          info.Checker);
            Set("Designer",         info.Designer);
            Set("Client",           info.Client);
            Set("ProjectName",      info.ProjectName);
            Set("SubProjectName",   info.SubProjectName);
            Set("DrawingName",      info.DrawingName);
            Set("ProjectCode",      info.ProjectCode);
            Set("Discipline",       info.Discipline);
            Set("Version",          info.Version);
            Set("Phase",            info.Phase);
            Set("Date",             info.Date);
            Set("DrawingNumber",    info.DrawingNumber);
            Set("Barcode",          info.Barcode);
        }

        private void SaveToInfo()
        {
            _info.ChiefDesigner    = Get("ChiefDesigner");
            _info.Approver         = Get("Approver");
            _info.Reviewer         = Get("Reviewer");
            _info.ProfessionalLead = Get("ProfessionalLead");
            _info.Checker          = Get("Checker");
            _info.Designer         = Get("Designer");
            _info.Client           = Get("Client");
            _info.ProjectName      = Get("ProjectName");
            _info.SubProjectName   = Get("SubProjectName");
            _info.DrawingName      = Get("DrawingName");
            _info.ProjectCode      = Get("ProjectCode");
            _info.Discipline       = Get("Discipline");
            _info.Version          = Get("Version");
            _info.Phase            = Get("Phase");
            _info.Date             = Get("Date");
            _info.DrawingNumber    = Get("DrawingNumber");
            _info.Barcode          = Get("Barcode");
        }

        private void Set(string key, string value)
        {
            if (_boxes.TryGetValue(key, out var tb))
                tb.Text = value ?? "XX";
        }

        private string Get(string key)
        {
            return _boxes.TryGetValue(key, out var tb) ? (tb.Text ?? "").Trim() : "XX";
        }

        private static TitleFrameInfo Clone(TitleFrameInfo src)
        {
            return new TitleFrameInfo
            {
                ChiefDesigner    = src.ChiefDesigner,
                Approver         = src.Approver,
                Reviewer         = src.Reviewer,
                ProfessionalLead = src.ProfessionalLead,
                Checker          = src.Checker,
                Designer         = src.Designer,
                Client           = src.Client,
                ProjectName      = src.ProjectName,
                SubProjectName   = src.SubProjectName,
                DrawingName      = src.DrawingName,
                ProjectCode      = src.ProjectCode,
                Discipline       = src.Discipline,
                Version          = src.Version,
                Phase            = src.Phase,
                Date             = src.Date,
                DrawingNumber    = src.DrawingNumber,
                Barcode          = src.Barcode
            };
        }
    }
}