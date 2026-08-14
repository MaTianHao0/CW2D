using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TitleBlockBattery
{
    public class TitleBlockHistoryForm : Form
    {
        private readonly TitleBlockManager _manager;
        private TitleBlockConfig _config;

        private ListView list;
        private Button btnApply;
        private Button btnAddFromCurrent;
        private Button btnRename;
        private Button btnDelete;
        private Button btnClose;

        public TitleBlockHistoryForm(TitleBlockManager manager)
        {
            _manager = manager ?? new TitleBlockManager();
            _config = _manager.GetConfig();
            InitializeComponent();
            LoadPresets();
        }

        private void InitializeComponent()
        {
            this.Text = "历史记录（17字段预设）";
            this.Size = new Size(700, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = true;
            this.MaximizeBox = true;

            list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Dock = DockStyle.Top,
                Height = 300
            };
            list.Columns.Add("名称", 260);
            list.Columns.Add("创建时间", 180);
            list.Columns.Add("最近使用", 180);

            btnApply = new Button
            {
                Text = "应用到当前项目",
                Width = 130,
                Height = 30
            };
            btnApply.Click += (s, e) => ApplySelected();

            btnAddFromCurrent = new Button
            {
                Text = "保存当前为预设",
                Width = 130,
                Height = 30
            };
            btnAddFromCurrent.Click += (s, e) => AddFromCurrent();

            btnRename = new Button
            {
                Text = "重命名",
                Width = 90,
                Height = 30
            };
            btnRename.Click += (s, e) => RenameSelected();

            btnDelete = new Button
            {
                Text = "删除",
                Width = 90,
                Height = 30
            };
            btnDelete.Click += (s, e) => DeleteSelected();

            btnClose = new Button
            {
                Text = "关闭",
                Width = 90,
                Height = 30,
                DialogResult = DialogResult.OK
            };

            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10),
                AutoSize = false
            };

            panel.Controls.Add(btnApply);
            panel.Controls.Add(btnAddFromCurrent);
            panel.Controls.Add(btnRename);
            panel.Controls.Add(btnDelete);
            panel.Controls.Add(btnClose);

            this.Controls.Add(list);
            this.Controls.Add(panel);
        }

        private void LoadPresets()
        {
            list.Items.Clear();
            var presets = _manager.GetPresets();
            foreach (var p in presets)
            {
                var item = new ListViewItem(p.Name) { Tag = p.Id };
                item.SubItems.Add(p.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(p.LastUsedAt.ToString("yyyy-MM-dd HH:mm"));
                list.Items.Add(item);
            }
        }

        private Guid? GetSelectedId()
        {
            if (list.SelectedItems.Count == 0) return null;
            var tag = list.SelectedItems[0].Tag;
            if (tag is Guid g) return g;
            return null;
        }

        private void ApplySelected()
        {
            var id = GetSelectedId();
            if (id == null)
            {
                MessageBox.Show("请先选择一条预设。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_manager.ApplyPreset(id.Value))
            {
                _config = _manager.GetConfig();
                MessageBox.Show("已应用到当前项目（写入 FrameInfo）。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadPresets(); // 更新时间戳
            }
            else
            {
                MessageBox.Show("应用失败：未找到该预设或预设无效。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddFromCurrent()
        {
            _config = _manager.GetConfig();
            if (_config.FrameInfo == null) _config.FrameInfo = new TitleFrameInfo();

            string name = Prompt("为该预设起一个名称：", "保存为预设");
            if (string.IsNullOrWhiteSpace(name)) return;

            _manager.AddPreset(_config.FrameInfo, name.Trim());
            LoadPresets();
        }

        private void RenameSelected()
        {
            var id = GetSelectedId();
            if (id == null)
            {
                MessageBox.Show("请先选择一条预设。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = Prompt("输入新的名称：", "重命名");
            if (string.IsNullOrWhiteSpace(name)) return;

            if (_manager.RenamePreset(id.Value, name.Trim()))
            {
                LoadPresets();
            }
        }

        private void DeleteSelected()
        {
            var id = GetSelectedId();
            if (id == null)
            {
                MessageBox.Show("请先选择一条预设。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("确定要删除该预设吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (_manager.DeletePreset(id.Value))
                {
                    LoadPresets();
                }
                else
                {
                    MessageBox.Show("删除失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Prompt(string text, string title)
        {
            using (var form = new Form())
            using (var lbl = new Label())
            using (var txt = new TextBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                form.Text = title;
                lbl.Text = text;
                lbl.SetBounds(9, 20, 372, 13);
                txt.SetBounds(12, 50, 372, 20);
                ok.Text = "确定";
                cancel.Text = "取消";
                ok.SetBounds(228, 80, 75, 23);
                cancel.SetBounds(309, 80, 75, 23);
                ok.DialogResult = DialogResult.OK;
                cancel.DialogResult = DialogResult.Cancel;

                form.ClientSize = new Size(396, 120);
                form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog() == DialogResult.OK ? txt.Text : null;
            }
        }
    }
}
