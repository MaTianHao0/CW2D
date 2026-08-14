using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace TitleBlockBattery
{
    public partial class TitleBlockForm : Form
    {
        private readonly TitleBlockManager _manager;
        private TitleBlockConfig _config;

        private Button btnOK;
        private Button btnCancel;
        private TextBox txtInfo;
        private Button btnFrameInfo;
        private Button btnHistory;   // 新增：历史预设按钮

        public TitleBlockForm()
        {
            InitializeComponent();
            _manager = new TitleBlockManager();
            _config = _manager.GetConfig();
            LoadSettings();

            EnsureFrameInfoButtonVisible();
            EnsureHistoryButtonVisible(); // 新增
        }

        private void EnsureFrameInfoButtonVisible()
        {
            if (btnFrameInfo == null || !Controls.Contains(btnFrameInfo))
            {
                var buttonFont = new Font("微软雅黑", 9F, FontStyle.Regular);

                btnFrameInfo = new Button
                {
                    Text = "图框信息设置",
                    Location = new Point(360, 113),
                    Size = new Size(120, 30),
                    Font = buttonFont,
                    UseVisualStyleBackColor = true,
                    BackColor = Color.LightSkyBlue,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left
                };
                btnFrameInfo.Click += BtnFrameInfo_Click;

                this.Controls.Add(btnFrameInfo);
                btnFrameInfo.BringToFront();
            }
        }

        private void EnsureHistoryButtonVisible()
        {
            if (btnHistory == null || !Controls.Contains(btnHistory))
            {
                var buttonFont = new Font("微软雅黑", 9F, FontStyle.Regular);
                btnHistory = new Button
                {
                    Text = "历史记录",
                    Location = new Point(485, 113),
                    Size = new Size(90, 30),
                    Font = buttonFont,
                    UseVisualStyleBackColor = true,
                    BackColor = Color.PaleGreen,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left
                };
                btnHistory.Click += BtnHistory_Click;

                this.Controls.Add(btnHistory);
                btnHistory.BringToFront();
            }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(600, 480);
            this.MinimumSize = new Size(580, 450);
            this.Text = "图框电池设置";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            var labelFont = new Font("微软雅黑", 9F, FontStyle.Regular);
            var buttonFont = new Font("微软雅黑", 9F, FontStyle.Regular);

            var lblTemplatePath = new Label
            {
                Text = "默认模板路径:",
                Location = new Point(15, 25),
                Size = new Size(120, 25),
                Font = labelFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            var txtTemplatePath = new TextBox
            {
                Name = "txtTemplatePath",
                Location = new Point(145, 25),
                Size = new Size(320, 25),
                Font = new Font("微软雅黑", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var btnBrowse = new Button
            {
                Text = "浏览",
                Location = new Point(475, 23),
                Size = new Size(80, 30),
                Font = buttonFont,
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowse.Click += BtnBrowse_Click;

            var lblCurrentPath = new Label
            {
                Text = "当前状态:",
                Location = new Point(15, 70),
                Size = new Size(120, 25),
                Font = labelFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            var lblPathStatus = new Label
            {
                Name = "lblPathStatus",
                Location = new Point(145, 70),
                Size = new Size(410, 25),
                ForeColor = Color.Gray,
                Text = "未设置路径",
                Font = labelFont,
                TextAlign = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Info,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblDefaultSize = new Label
            {
                Text = "默认图纸大小:",
                Location = new Point(15, 115),
                Size = new Size(120, 25),
                Font = labelFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            var cmbDefaultSize = new ComboBox
            {
                Name = "cmbDefaultSize",
                Location = new Point(145, 115),
                Size = new Size(100, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("微软雅黑", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            var btnTest = new Button
            {
                Text = "测试路径",
                Location = new Point(260, 113),
                Size = new Size(90, 30),
                Font = buttonFont,
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnTest.Click += BtnTest_Click;

            // 这里会在构造函数里追加：btnFrameInfo（图框信息）、btnHistory（历史记录）

            var separator = new Label
            {
                Location = new Point(15, 160),
                Size = new Size(540, 2),
                BorderStyle = BorderStyle.Fixed3D,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblInfo = new Label
            {
                Text = "测试信息:",
                Location = new Point(15, 175),
                Size = new Size(120, 25),
                Font = labelFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            txtInfo = new TextBox
            {
                Name = "txtInfo",
                Location = new Point(15, 205),
                Size = new Size(540, 180),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Control,
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.Fixed3D,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnOK = new Button
            {
                Text = "确定",
                Location = new Point(395, 400),
                Size = new Size(75, 35),
                DialogResult = DialogResult.OK,
                Font = buttonFont,
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(480, 400),
                Size = new Size(75, 35),
                DialogResult = DialogResult.Cancel,
                Font = buttonFont,
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.AddRange(new Control[]
            {
                lblTemplatePath, txtTemplatePath, btnBrowse,
                lblCurrentPath, lblPathStatus,
                lblDefaultSize, cmbDefaultSize, btnTest,
                separator, lblInfo, txtInfo,
                btnOK, btnCancel
            });

            txtTemplatePath.TabIndex = 0;
            btnBrowse.TabIndex = 1;
            cmbDefaultSize.TabIndex = 2;
            btnTest.TabIndex = 3;

            this.Resize += TitleBlockForm_Resize;
            this.Load += (s, e) =>
            {
                EnsureFrameInfoButtonVisible();
                EnsureHistoryButtonVisible();
            };
        }

        private void TitleBlockForm_Resize(object sender, EventArgs e)
        {
            try
            {
                if (btnOK != null && btnCancel != null)
                {
                    var bottomMargin = 50;
                    var newY = Math.Max(this.ClientSize.Height - bottomMargin, 350);
                    btnOK.Location = new Point(btnOK.Location.X, newY);
                    btnCancel.Location = new Point(btnCancel.Location.X, newY);
                }

                if (txtInfo != null && btnOK != null)
                {
                    var textBoxBottomMargin = 100;
                    var maxHeight = Math.Max(this.ClientSize.Height - txtInfo.Location.Y - textBoxBottomMargin, 100);
                    txtInfo.Size = new Size(txtInfo.Width, maxHeight);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"窗体大小调整时发生错误: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            var txtTemplatePath = this.Controls["txtTemplatePath"] as TextBox;
            var cmbDefaultSize = this.Controls["cmbDefaultSize"] as ComboBox;

            if (txtTemplatePath != null)
                txtTemplatePath.Text = _config.DefaultTemplatePath ?? "";

            if (cmbDefaultSize != null)
            {
                cmbDefaultSize.Items.Clear();
                foreach (var size in _config.SupportedSizes)
                    cmbDefaultSize.Items.Add(size);
                cmbDefaultSize.SelectedItem = _config.DefaultSize;
            }

            UpdatePathStatus();
        }

        private void UpdatePathStatus()
        {
            var lblPathStatus = this.Controls["lblPathStatus"] as Label;
            if (lblPathStatus == null) return;

            var path = _config.DefaultTemplatePath;

            if (string.IsNullOrEmpty(path))
            {
                lblPathStatus.Text = "未设置默认路径";
                lblPathStatus.ForeColor = Color.Red;
                lblPathStatus.BackColor = Color.FromArgb(255, 240, 240);
            }
            else if (Directory.Exists(path))
            {
                lblPathStatus.Text = $"路径有效: {path}";
                lblPathStatus.ForeColor = Color.Green;
                lblPathStatus.BackColor = Color.FromArgb(240, 255, 240);
            }
            else
            {
                lblPathStatus.Text = $"路径无效: {path}";
                lblPathStatus.ForeColor = Color.Red;
                lblPathStatus.BackColor = Color.FromArgb(255, 240, 240);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择包含DWG模板文件的文件夹";
                dialog.SelectedPath = _config.DefaultTemplatePath;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var txtTemplatePath = this.Controls["txtTemplatePath"] as TextBox;
                    if (txtTemplatePath != null)
                    {
                        txtTemplatePath.Text = dialog.SelectedPath;
                        _config.DefaultTemplatePath = dialog.SelectedPath;
                        UpdatePathStatus();
                    }
                }
            }
        }

        private void BtnFrameInfo_Click(object sender, EventArgs e)
        {
            try
            {
                if (_config.FrameInfo == null)
                    _config.FrameInfo = new TitleFrameInfo();

                // 通过反射优雅加载 TitleBlockInfoForm，避免强类型依赖与参数类型误传
                var formType = Type.GetType("TitleBlockBattery.TitleBlockInfoForm");
                if (formType == null)
                {
                    MessageBox.Show(
                        "未找到 TitleBlockInfoForm 类型。请确认该窗体已包含在项目中。\n（已优雅降级：继续使用当前配置）",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var form = (Form)Activator.CreateInstance(formType, new object[] { _config.FrameInfo }))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        var getMethod = formType.GetMethod("GetFrameInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (getMethod != null)
                        {
                            var newInfo = getMethod.Invoke(form, null) as TitleFrameInfo;
                            if (newInfo != null)
                            {
                                _config.FrameInfo = newInfo;
                                if (txtInfo != null)
                                {
                                    txtInfo.AppendText("\r\n图框信息已更新。这些信息将在生成图框时替换模板中的占位符。\r\n");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开图框信息设置时出错: {ex.Message}\r\n\r\n详细信息: {ex.StackTrace}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnHistory_Click(object sender, EventArgs e)
        {
            try
            {
                using (var form = new TitleBlockHistoryForm(_manager))
                {
                    form.ShowDialog(this);
                    // 历史应用可能更新了 FrameInfo，这里持久化一下以同步 config
                    _config = _manager.GetConfig();
                    _manager.UpdateConfig(_config);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开历史记录时出错: {ex.Message}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            var txtTemplatePath = this.Controls["txtTemplatePath"] as TextBox;
            if (txtTemplatePath == null || txtInfo == null) return;

            var path = txtTemplatePath.Text;

            txtInfo.Clear();
            txtInfo.AppendText($"正在测试路径: {path}\r\n");
            txtInfo.AppendText($"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
            txtInfo.AppendText(new string('=', 50) + "\r\n");

            if (string.IsNullOrEmpty(path))
            {
                txtInfo.AppendText("错误: 未指定路径\r\n");
                txtInfo.AppendText("请先选择模板文件夹路径。\r\n");
                return;
            }

            if (!Directory.Exists(path))
            {
                txtInfo.AppendText("错误: 目录不存在\r\n");
                txtInfo.AppendText($"路径 '{path}' 无法访问。\r\n");
                return;
            }

            txtInfo.AppendText("✓ 目录存在且可访问\r\n");

            int foundCount = 0;
            int totalCount = _config.SupportedSizes.Count;

            txtInfo.AppendText("\r\n检查模板文件:\r\n");
            txtInfo.AppendText(new string('-', 30) + "\r\n");

            foreach (var size in _config.SupportedSizes)
            {
                var fileName = $"{size}_Frame.dwg";
                var filePath = Path.Combine(path, fileName);

                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    txtInfo.AppendText($"✓ {fileName} - 大小: {fileInfo.Length:N0} 字节\r\n");
                    foundCount++;
                }
                else
                {
                    txtInfo.AppendText($"✗ {fileName} - 文件未找到\r\n");
                }
            }

            txtInfo.AppendText(new string('=', 50) + "\r\n");
            txtInfo.AppendText($"测试结果: 找到 {foundCount}/{totalCount} 个模板文件\r\n");

            if (foundCount == totalCount)
            {
                txtInfo.AppendText("所有模板文件都已找到，路径配置正确！\r\n");
            }
            else if (foundCount > 0)
            {
                txtInfo.AppendText("部分模板文件缺失，建议检查文件完整性。\r\n");
            }
            else
            {
                txtInfo.AppendText("未找到任何模板文件，请检查路径是否正确。\r\n");
            }

            txtInfo.AppendText($"\r\n测试完成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            var txtTemplatePath = this.Controls["txtTemplatePath"] as TextBox;
            var cmbDefaultSize = this.Controls["cmbDefaultSize"] as ComboBox;

            if (txtTemplatePath == null || cmbDefaultSize == null) return;

            if (string.IsNullOrWhiteSpace(txtTemplatePath.Text))
            {
                MessageBox.Show("请先选择模板路径！", "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.DefaultTemplatePath = txtTemplatePath.Text;
            _config.DefaultSize = cmbDefaultSize.SelectedItem?.ToString() ?? "A4";

            try
            {
                _manager.UpdateConfig(_config);
                MessageBox.Show("设置已成功保存！", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置时发生错误：{ex.Message}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Resize -= TitleBlockForm_Resize;
                this.Load -= (s, e) =>
                {
                    EnsureFrameInfoButtonVisible();
                    EnsureHistoryButtonVisible();
                };
            }
            base.Dispose(disposing);
        }
    }
}
