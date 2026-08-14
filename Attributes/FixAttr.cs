using CW2D;
using CW2D.Attributes;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Microsoft.VisualBasic; // <--- 已添加
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace CW2D.Attributes
{
    public class FixAttr : GH_Component
    {
        public Dictionary<string, string> StoredAttributes { get; set; } = new Dictionary<string, string>();

        // 用于“记住”上一次从输入端加载的数据的哈希值
        private int _lastLoadedInputHash = 0;
        /// <summary>
        /// Initializes a new instance of the FixAttr class.
        /// </summary>
        public FixAttr()
          : base("属性修改", "属性修改",
          "获取属性并修改、添加或删除属性",
          Title.CW2D(), Title.Attribute())
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("输入属性", "输入属性", "输入属性数据（可以是绑定属性后的数据，也可以是文本）", GH_ParamAccess.item);
            pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("属性列表", "属性列表", "输出格式为 'key=value' 的属性列表", GH_ParamAccess.list);
            pManager.AddTextParameter("属性文本", "属性文本", "格式为 'key1=value1;key2=value2' 的单一字符串", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_Goo goo = null;

            // 我们先尝试获取输入数据，但不立即处理
            DA.GetData(0, ref goo);

            // --- 核心修改逻辑 ---
            // 计算当前输入数据的哈希值。如果无输入，则为0。
            int currentInputHash = goo?.ToString().GetHashCode() ?? 0;

            // 只有当“当前输入”和“上一次加载的输入”不同时，才从输入端更新内存
            if (currentInputHash != _lastLoadedInputHash)
            {
                // 清空旧属性，准备从新输入加载
                var loadedAttributes = new Dictionary<string, string>();
                if (goo != null)
                {
                    if (goo is GH_AttributeData attrGoo && attrGoo.IsValid)
                    {
                        loadedAttributes = new Dictionary<string, string>(attrGoo.Value.Attribute);
                    }
                    else
                    {
                        var strGoo = goo.ToString();
                        // 确保字符串格式为 key:value;key:value
                        string[] pairs = strGoo.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string pair in pairs)
                        {
                            string[] parts = pair.Split(new[] { '=' }, 2);
                            string key = parts[0];
                            if (!loadedAttributes.ContainsKey(key))
                            {
                                string value = (parts.Length == 2) ? parts[1] : null;
                                loadedAttributes.Add(key, value);
                            }
                        }
                    }
                }
                // 用新加载的数据覆盖内存，并“记住”这次加载
                this.StoredAttributes = loadedAttributes;
                _lastLoadedInputHash = currentInputHash;
            }
            // 如果哈希值相同（意味着输入没变，很可能是UI刷新），我们则跳过加载步骤，
            // 从而保留 StoredAttributes 中由UI修改过的数据。
            // 使用内存中的 StoredAttributes 来生成输出
            var attributeList = this.StoredAttributes.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
            var combinedAttributes = string.Join(";", attributeList);

            DA.SetDataList(0, attributeList);
            DA.SetData(1, combinedAttributes);
        }
        public override void CreateAttributes()
        {
            m_attributes = new ModifyAttributesComponentAttributes(this);
        }
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetInt32("AttributeCount", StoredAttributes.Count);
            writer.SetInt32("LastHash", _lastLoadedInputHash); // 同时保存哈希值
            int i = 0;
            foreach (var kvp in StoredAttributes)
            {
                writer.SetString("key", i, kvp.Key);
                writer.SetString("value", i, kvp.Value);
                i++;
            }
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            StoredAttributes.Clear();
            _lastLoadedInputHash = reader.GetInt32("LastHash"); // 读取哈希值
            int count = reader.GetInt32("AttributeCount");
            for (int i = 0; i < count; i++)
            {
                string key = reader.GetString("key", i);
                string value = reader.GetString("value", i);
                StoredAttributes.Add(key, value);
            }
            return base.Read(reader);
        }
        public class ModifyAttributesComponentAttributes : GH_ComponentAttributes
    {
   
        public ModifyAttributesComponentAttributes(FixAttr owner) : base(owner) { }

        private RectangleF _buttonBounds;

        protected override void Layout()
        {
            base.Layout();
            Rectangle rec = GH_Convert.ToRectangle(Bounds);
            rec.Height += 25;
            Bounds = rec;
            _buttonBounds = new RectangleF(rec.Left+10, rec.Bottom - 25, rec.Width-20, 25);
        }

        protected override void Render(GH_Canvas canvas, Graphics g, GH_CanvasChannel channel)
        {
            base.Render(canvas, g, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                var capsule = GH_Capsule.CreateTextCapsule(_buttonBounds, _buttonBounds, GH_Palette.Grey, "编辑属性");
                capsule.Render(g, Selected, Owner.Locked, true);
                capsule.Dispose();
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _buttonBounds.Contains(e.CanvasLocation))
            {
                var owner = (FixAttr)this.Owner;
                var editor = new AttributeEditorForm(owner);

                if (editor.ShowDialog() == DialogResult.OK)
                {
                    owner.RecordUndoEvent("Modify Attributes");
                    // 3. 将窗口返回的数据赋值给 StoredAttributes
                    owner.StoredAttributes = editor.GetAttributes();
                    owner.ExpireSolution(true);
                }
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }
    }

    // --- 属性编辑器窗口 ---
    public class AttributeEditorForm : Form
    {
        private readonly FixAttr _owner;
        private Dictionary<string, string> _workingAttributes;

        private ListBox listBoxKeys;
        private TextBox textBoxValue;

        public AttributeEditorForm(FixAttr owner)
        {
            _owner = owner;
            // 从 StoredAttributes 初始化
            _workingAttributes = new Dictionary<string, string>(owner.StoredAttributes);

            InitializeComponent();
            LoadAttributesToListBox();
        }

        public Dictionary<string, string> GetAttributes() => _workingAttributes;

        private Button btnAdd;
        private Button btnRemove;
        private Button btnOk;
        private Button btnCancel;

        private void InitializeComponent()
        {
            // --- 窗口基本设置 ---
            this.Text = "属性编辑器";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;

            // --- 控件定义 ---
            var splitContainer = new SplitContainer { Dock = DockStyle.Fill, BorderStyle = BorderStyle.Fixed3D };
            listBoxKeys = new ListBox { Dock = DockStyle.Fill };
            textBoxValue = new TextBox { Dock = DockStyle.Fill, Multiline = true };

            btnAdd = new Button { Text = "增加属性", Dock = DockStyle.Top,Height = 40 };
            btnRemove = new Button { Text = "删除属性", Dock = DockStyle.Top, Height = 40 };

            btnOk = new Button { Text = "保存并确定", Dock = DockStyle.Right, Size = new Size(150, 50) };
            btnCancel = new Button { Text = "关闭", Dock = DockStyle.Right, Size = new Size(100, 40) };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(5) };
            var leftPanel = new Panel { Dock = DockStyle.Fill };

            // --- 布局 ---
            splitContainer.Panel1.Controls.Add(leftPanel);
            splitContainer.Panel2.Controls.Add(textBoxValue);
            leftPanel.Controls.Add(listBoxKeys);
            leftPanel.Controls.Add(btnRemove);
            leftPanel.Controls.Add(btnAdd);

            bottomPanel.Controls.Add(btnOk);
            bottomPanel.Controls.Add(btnCancel);

            this.Controls.Add(splitContainer);
            this.Controls.Add(bottomPanel);

            // --- 事件绑定 ---
            listBoxKeys.SelectedIndexChanged += ListBoxKeys_SelectedIndexChanged;
            textBoxValue.TextChanged += TextBoxValue_TextChanged;
            btnAdd.Click += BtnAdd_Click;
            btnRemove.Click += BtnRemove_Click;
            btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK;  this.Close(); };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        }

        private void LoadAttributesToListBox()
        {
            listBoxKeys.Items.Clear();
            foreach (var key in _workingAttributes.Keys)
            {
                listBoxKeys.Items.Add(key);
            }
            if (listBoxKeys.Items.Count > 0)
            {
                listBoxKeys.SelectedIndex = 0;
            }
        }

        private void ListBoxKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxKeys.SelectedItem == null)
            {
                textBoxValue.Text = "";
                textBoxValue.Enabled = false;
                return;
            }

            textBoxValue.Enabled = true;
            string selectedKey = listBoxKeys.SelectedItem.ToString();
            textBoxValue.Text = _workingAttributes[selectedKey];
        }



        private void TextBoxValue_TextChanged(object sender, EventArgs e)
        {
            if (listBoxKeys.SelectedItem != null)
            {
                string selectedKey = listBoxKeys.SelectedItem.ToString();
                _workingAttributes[selectedKey] = textBoxValue.Text;
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string newKey = ""; // 先声明一个变量来存储结果
            using (var inputBox = new InputBoxForm("增加属性", "请输入新的属性名称："))
            {
                // ShowDialog() 会打开窗口并等待用户操作
                if (inputBox.ShowDialog() == DialogResult.OK)
                {
                    // 如果用户点击了“确定”，就从窗口获取输入值
                    newKey = inputBox.UserInput;
                }
                else
                {
                    // 如果用户点击了“取消”或关闭窗口，则直接返回，不执行任何操作
                    return;
                }
            }

            // 后续逻辑保持不变
            if (!string.IsNullOrWhiteSpace(newKey) && !_workingAttributes.ContainsKey(newKey))
            {
                _workingAttributes.Add(newKey, ""); // 添加新键和空值
                LoadAttributesToListBox();
                listBoxKeys.SelectedItem = newKey; // 自动选中新添加的项
            }
            else if (string.IsNullOrWhiteSpace(newKey))
            {
                // 可以选择不提示，或者提示用户未输入内容
            }
            else if (_workingAttributes.ContainsKey(newKey))
            {
                MessageBox.Show("该属性名称已存在！");
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (listBoxKeys.SelectedItem != null)
            {
                string selectedKey = listBoxKeys.SelectedItem.ToString();
                if (MessageBox.Show($"确定要删除属性 '{selectedKey}' 吗？", "确认删除", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _workingAttributes.Remove(selectedKey);
                    LoadAttributesToListBox();
                }
            }
        }
    }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override Bitmap Icon
        {
            get
            {
                try
                {
                    // 1. 获取当前鼠标悬停状态（配合Attributes类）
                    bool isHovering = false;
                    if (this.Attributes is GH_ComponentAttributes attributes)
                    {
                        // 通过反射获取私有字段判断悬停状态（Grasshopper内部实现方式）
                        var field = typeof(GH_ComponentAttributes).GetField("m_mouseOver",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        isHovering = (bool)(field?.GetValue(attributes) ?? false);
                    }

                    // 2. 加载原始图标
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "CW2D.Resources.attri modify.png";

                    // 或动态构建名称

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var originalIcon = new Bitmap(stream);

                            // 3. 根据悬停状态处理图标
                            if (isHovering)
                            {
                                return ResizeIcon(originalIcon, 48, 48); // 悬停时强制48x48
                            }
                            else if (originalIcon.Width != 24 || originalIcon.Height != 24)
                            {
                                return ResizeIcon(originalIcon, 24, 24); // 非标准尺寸调整为24x24
                            }
                            return originalIcon; // 标准尺寸直接返回
                        }
                        else
                        {
                            // 调试信息
                            var availableResources = assembly.GetManifestResourceNames();
                            Rhino.RhinoApp.WriteLine("找不到资源，可用资源:\n" +
                                string.Join("\n", availableResources));
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Rhino.RhinoApp.WriteLine($"图标处理失败: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// 高质量缩放图标（保持宽高比）
        /// </summary>
        private Bitmap ResizeIcon(Bitmap source, int width, int height)
        {
            var dest = new Bitmap(width, height);
            using (var g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 计算保持比例的缩放
                float scale = Math.Min(
                    (float)width / source.Width,
                    (float)height / source.Height);
                int scaledWidth = (int)(source.Width * scale);
                int scaledHeight = (int)(source.Height * scale);
                int x = (width - scaledWidth) / 2;
                int y = (height - scaledHeight) / 2;

                g.Clear(Color.Transparent); // 透明背景
                g.DrawImage(source, x, y, scaledWidth, scaledHeight);
            }
            return dest;
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("EEFC0925-21B4-4639-9584-34F1CC6B750B"); }
        }
    }
}