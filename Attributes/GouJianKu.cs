using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
//using Ed.Eto;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
namespace cw2d1
{
    public class cw2dComponent1 : GH_Component
    {
        private Dictionary<string, object> _attributes = new Dictionary<string, object>();
        private DataTable _propertyTable = new DataTable(); // 存储属性表格数据
        private bool _isDataLoaded = false;
        private int _quantity = 1;
        private Dictionary<string, string> _categoryFileMap = new Dictionary<string, string>
        {
            //面板
            {"玻璃", "玻璃属性.csv"},
            {"金属板", "金属板属性.csv"},
            {"石材", "石材属性.csv"},
            {"石膏板", "石膏板属性.csv"},
            {"钢材", "钢材属性.csv"},
            {"型材", "型材属性.csv"},
            {"铝板", "铝板属性.csv"},
            {"面板材料自定义", "面板材料自定义材料属性.csv"}, 

            //龙骨
            {"铝龙骨", "铝龙骨属性.csv"},
            {"钢龙骨", "钢龙骨属性.csv"},
            {"木龙骨", "木龙骨属性.csv"},
            {"龙骨材料自定义", "龙骨材料自定义属性.csv"},

            //五金
            {"门五金", "门五金属性.csv"},
            {"窗五金", "窗五金属性.csv"},
            {"点驳件", "点驳件属性.csv"},
            {"转接件", "转接件属性.csv"},
            {"挂件", "挂件属性.csv"},
            {"螺栓", "螺栓属性.csv"},
            {"螺钉", "螺钉属性.csv"},
            {"拉铆钉", "拉铆钉属性.csv"},
            {"埋件", "埋件属性.csv"},
            {"锚栓", "锚栓属性.csv"},
            {"五金及配件自定义", "五金及配件自定义.csv"},

            //辅材
            {"胶", "胶属性.csv"},
            {"胶条", "胶条属性.csv"},
            {"泡沫棒", "泡沫棒属性.csv"},
            {"单面胶带", "单面胶带属性.csv"},
            {"双面胶带", "双面胶带属性.csv"},
            {"垫块", "垫块属性.csv"},
            {"岩棉", "岩棉属性.csv"},
            {"辅材自定义", "辅材自定义属性.csv"},

            //标准件
            {"标准件自定义", "标准件自定义属性.csv"},
             //组件

            {"门组件", "门组件属性.csv"},
            {"窗组件", "窗组件属性.csv"},
            {"百叶组件", "百叶组件属性.csv"},
            {"单元体组件", "单元体属性.csv"},
            {"组件自定义", "组件自定义属性.csv"},
        };

        private string _currentCategory;
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }
        public cw2dComponent1()
        : base("构件库属性", "构件库属性",
           "用于设置材料属性，并绑定几何信息，“类型”名称应与csv材料名称对应",
            Title.CW2D(), Title.Attribute())
        {
            _propertyTable.Columns.Add("选中", typeof(bool));
            // 初始化表格结构，修改
            _propertyTable.Columns.Add("类型", typeof(string));
            _propertyTable.Columns.Add("类型代号", typeof(string));
            _propertyTable.Columns.Add("排序", typeof(string));
            _propertyTable.Columns.Add("物料名称", typeof(string));
            _propertyTable.Columns.Add("物料编码", typeof(string));
            _propertyTable.Columns.Add("工程属性", typeof(string));
            _propertyTable.Columns.Add("表面处理方式", typeof(string));
            _propertyTable.Columns.Add("色号", typeof(string));
            _propertyTable.Columns.Add("模号", typeof(string));
            _propertyTable.Columns.Add("颜色", typeof(string));
            _propertyTable.Columns.Add("材质", typeof(string));
            _propertyTable.Columns.Add("剖面填充样式", typeof(string));
            _propertyTable.Columns.Add("立面填充样式", typeof(string));
            _propertyTable.Columns.Add("线型", typeof(string));
            _propertyTable.Columns.Add("图样表示", typeof(string));
            _propertyTable.Columns.Add("标注", typeof(string));
            _propertyTable.Columns.Add("门窗", typeof(string));
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("属性", "属性", "选中行的属性", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Boolean bool_name = true;
            //DA.GetData(0, ref bool_name);
            DataRow selectedRow = null;
            //var selectedRows = new List<string>();
            foreach (System.Data.DataRow row in _propertyTable.Rows)
            {
                if (_propertyTable.Columns.Contains("选中") && row["选中"] != DBNull.Value && (bool)row["选中"])
                {
                    selectedRow = row;
                    break; // 找到第一个选中的行就停止
                }
            }
            // 构建输出字符串
            string selectedRowString = string.Empty;
            foreach (DataRow row in _propertyTable.Rows)
            {
                if (_propertyTable.Columns.Contains("选中") && row["选中"] is bool selected && selected)
                {
                    var parts = new List<string>();
                    foreach (DataColumn col in _propertyTable.Columns)
                    {
                        if (col.ColumnName != "选中")
                            parts.Add($"{col.ColumnName}={row[col]}");
                    }
                    selectedRowString += string.Join(";", parts);
                    break;
                }
            }
            if (selectedRowString.Length > 0)
                selectedRowString += ";";
            DA.SetData(0, selectedRowString);
        }
        public override void CreateAttributes()
        {
            m_attributes = new MaterialPropertyAttributes(this);
        }

        private class PropertyForm : Form
        {
            private DataGridView _dataGridView;
            private DataTable _table;
            private cw2dComponent1 _owner;
            private TreeView _categoryTreeView;

            public MaterialPropertyAttributes MaterialPropertyAttributes { get; }
            public PropertyForm(ref cw2dComponent1 owner)
            {
                _owner = owner;
                _table = owner._propertyTable;
                _table.Columns["选中"].SetOrdinal(0);

                this.Text = "Material Properties Editor";
                this.Width = 2500;
                this.Height = 900;

                this.StartPosition = FormStartPosition.CenterScreen;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.MaximizeBox = true;
                this.MinimizeBox = true;

                // 主布局面板
                var mainPanel = new TableLayoutPanel
                //创建一个TableLayoutPanel对象mainPanel，用于作为窗体的主要布局容器。设置其停靠方式为填充整个窗体，行数为2，列数为1。
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 2
                };
                //第一行按百分比分配空间且占100%
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                //第二行固定高度为40像素。
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
                mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15)); // 左侧窄一些，占20%
                mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 85)); // 右侧宽一些，占80%
                // // 创建并配置 DataGridView 控件（用于显示表格数据）
                _dataGridView = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    DataSource = _table,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = true,
                    BackgroundColor = SystemColors.Window,
                    BorderStyle = BorderStyle.None,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells
                };

                _dataGridView.CellValueChanged += DataGridView_CellValueChanged;
                _dataGridView.CurrentCellDirtyStateChanged += DataGridView_CurrentCellDirtyStateChanged;
                // 添加DataError事件，防止布尔类型转换异常弹窗
                _dataGridView.DataError += (s, e) => { e.Cancel = true; };
                mainPanel.Controls.Add(_dataGridView, 1, 0); // DataGridView 在第1列，第0行
                // 新增：TreeView 的初始化和配置
                _categoryTreeView = new TreeView
                {
                    Dock = DockStyle.Fill,
                    BackColor = System.Drawing.Color.WhiteSmoke, // 浅色背景，与DataGridView区分
                    BorderStyle = BorderStyle.FixedSingle,
                    HideSelection = false, // 选中节点后不隐藏选中状态
                    //ImageList = CreateCategoryImageList()
                };
                // 替换为下面这两行
                _categoryTreeView.BeforeExpand += CategoryTreeView_BeforeExpand; // 用于懒加载子节点
                _categoryTreeView.AfterSelect += CategoryTreeView_AfterSelect;    // 用于处理最终节点的点击

                PopulateCategories();
                // 将右侧面板和 DataGridView 放置到 mainPanel 的第一行
                mainPanel.Controls.Add(_categoryTreeView, 0, 0); // 左侧面板在第0列，第0行


                if (_dataGridView.Columns["选中"] is DataGridViewCheckBoxColumn checkBoxColumn)
                {
                    checkBoxColumn.HeaderText = "选中";
                    checkBoxColumn.TrueValue = true;
                    checkBoxColumn.FalseValue = false;
                }
                // 按钮面板
                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(5),
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true
                };

                var btnExcel = new Button
                {
                    Text = "构件库",
                    Size = new Size(120, 35),
                    Anchor = AnchorStyles.None
                };
                btnExcel.Click += BtnExcel_Click;

                var btnSync = new Button
                {
                    Text = "更新数据",
                    Size = new Size(140, 35),
                    Anchor = AnchorStyles.None
                };
                btnSync.Click += BtnSync_Click;

                var btnExport = new Button
                {
                    Text = "确定",
                    Size = new Size(160, 35),
                    Anchor = AnchorStyles.None
                };
                btnExport.Click += BtnExport_Click;

                buttonPanel.Controls.Add(btnExcel);
                buttonPanel.Controls.Add(btnSync);
                buttonPanel.Controls.Add(btnExport);

                //mainPanel.Controls.Add(_dataGridView, 0, 0);

                mainPanel.Controls.Add(buttonPanel, 0, 1);
                mainPanel.SetColumnSpan(buttonPanel, 2);

                this.Controls.Add(mainPanel);
            }
            public void PopulateCategories()
            {
                _categoryTreeView.Nodes.Clear(); // 清空现有节点

                // 创建根节点
                TreeNode rootNode = new TreeNode("材料库", 0, 0);
                _categoryTreeView.Nodes.Add(rootNode);

                // 一级分类
                string[] categories = { "面板", "龙骨", "五金及配件", "辅材", "标准件", "组件" };

                foreach (string category in categories)
                {
                    // 创建一级节点（使用图标索引1）
                    TreeNode node = new TreeNode(category, 1, 1);

                    // 添加二级占位节点（实际内容在选中时加载）
                    node.Nodes.Add(new TreeNode("加载中...", 2, 2));

                    rootNode.Nodes.Add(node);
                }

                // 默认展开根节点
                rootNode.Expand();
            }

            /// <summary>
            /// 在节点展开之前，动态加载其子节点。
            /// </summary>
            private void CategoryTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
            {
                // 确保有选中的节点
                if (e.Node == null) return;

                // 只处理一级分类节点，并且确保是第一次加载（通过检查占位节点）
                if (e.Node.Level == 1 && e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "加载中...")
                {
                    // 移除"加载中..."这个占位节点
                    e.Node.Nodes.Clear();

                    // 根据选中的一级节点文本，加载对应的二级子分类
                    string[] subCategories = Array.Empty<string>();

                    switch (e.Node.Text)
                    {
                        case "面板":
                            subCategories = new[] { "玻璃", "金属板", "钢材", "型材", "铝板", "石材", "石膏板", "面板材料自定义" };
                            break;
                        case "龙骨":
                            subCategories = new[] { "铝龙骨", "钢龙骨", "木龙骨", "龙骨材料自定义" };
                            break;
                        case "五金及配件":
                            subCategories = new[] { "门五金", "窗五金", "点驳件", "转接件", "挂件", "螺栓", "螺钉", "拉铆钉", "埋件", "锚栓", "五金及配件自定义" };
                            break;
                        case "辅材":
                            subCategories = new[] { "胶", "胶条", "泡沫棒", "单面胶带", "双面胶带", "垫块", "岩棉", "辅材自定义" };
                            break;
                        case "标准件":
                            subCategories = new[] { "标准件自定义" };
                            break;
                        case "组件":
                            subCategories = new[] { "门组件", "窗组件", "百叶组件", "单元体组件", "组件自定义" };
                            break;
                    }

                    // 将真实的子分类节点添加到当前节点下
                    foreach (string subCategory in subCategories)
                    {
                        //e.Node.Nodes.Add(new TreeNode(subCategory, 2, 2));
                        e.Node.Nodes.Add(new TreeNode(subCategory));
                    }

                    e.Node.Expand(); // 展开当前节点以显示子节点
                }
            }
            /// <summary>
            /// 处理 TreeView 节点选中事件，加载二级标题。
            /// </summary>
            private void CategoryTreeView_AfterSelect(object sender, TreeViewEventArgs e)
            {
                // 确保有选中的节点
                if (e.Node == null) return;
                if (e.Node.Level == 2)
                {
                    // 这里实现根据选择的二级分类筛选右侧表格
                    //FilterDataGridView(e.Node.Text);
                    _owner._currentCategory = e.Node.Text;
                    //FilterDataGridView(_currentCategory);
                    LoadCategoryData(_owner._currentCategory);
                    //_currentCategory = e.Node.Text; // 记录当前选中的分类
                    //LoadCategoryData(_currentCategory); // 加载分类数据
                }
            }

            private void LoadCategoryData(string category)
            {
                try
                {
                    if (!_owner._categoryFileMap.TryGetValue(category, out string fileName))
                    {
                        MessageBox.Show($"未找到分类 '{category}' 对应的数据文件", "错误");
                        return;
                    }

                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file", fileName);

                    if (!File.Exists(filePath))
                    {
                        CreateEmptyCsv(filePath);
                        MessageBox.Show($"已创建新的数据文件: {fileName}", "提示");
                    }

                    LoadCsvData(filePath);
                    FilterDataGridView(category);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载分类数据失败: {ex.Message}", "错误");
                }
            }

            private void CreateEmptyCsv(string filePath)
            {
                try
                {

                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        var headers = new List<string>();
                        foreach (DataColumn col in _owner._propertyTable.Columns)
                        {
                            headers.Add(col.ColumnName);
                        }
                        writer.WriteLine(string.Join(",", headers));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建文件失败: {ex.Message}", "错误");
                }
            }

            private void LoadCsvData(string filePath)
            {
                var newTable = ImportDataTableFromCsv(filePath);
                if (newTable == null || newTable.Rows.Count == 0) return;

                _owner._propertyTable.Rows.Clear();
                foreach (DataRow rawRow in newTable.Rows)
                {
                    DataRow newRow = _owner._propertyTable.NewRow();
                    // 始终将"选中"设置为false，忽略CSV中的值
                    newRow["选中"] = false;

                    foreach (DataColumn col in newTable.Columns)
                    {
                        string colName = col.ColumnName;
                        // 跳过"选中"列
                        if (colName == "选中") continue;

                        if (_owner._propertyTable.Columns.Contains(colName))
                        {
                            newRow[colName] = rawRow[colName];
                        }

                    }
                    _owner._propertyTable.Rows.Add(newRow);
                }
                _dataGridView.Refresh();
            }

            private void FilterDataGridView(string category)
            {
                try
                {
                    if (_table != null)
                    {
                        // 说明：
                        // - 旧逻辑按[类型]=当前分类筛选，会导致“***自定义”分类里一旦把“类型”改成自定义内容就被过滤掉，看起来就像不能自定义。
                        // - 新逻辑：自定义分类不做筛选（显示该CSV内全部记录），并允许“类型”任意输入。
                        bool isCustom = !string.IsNullOrEmpty(category) && category.EndsWith("自定义", StringComparison.Ordinal);

                        _table.DefaultView.RowFilter = (isCustom || string.IsNullOrEmpty(category))
                            ? ""
                            : $@"[类型] = '{category.Replace("'", "''")}'";

                        // 自定义分类：允许编辑“类型”；非自定义分类：锁定“类型”以避免误改导致数据被筛掉
                        if (_dataGridView != null && _dataGridView.Columns.Contains("类型"))
                            _dataGridView.Columns["类型"].ReadOnly = !isCustom;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"筛选数据时出错: {ex.Message}", "错误");
                }
            }

            private void BtnExcel_Click(object sender, EventArgs e) //构件库按钮功能
            {
                // 1. 检查是否选择了分类
                if (string.IsNullOrEmpty(_owner._currentCategory))
                {
                    // 如果 _currentCategory 为空或 null，表示用户尚未在左侧的 TreeView 中选择任何二级分类
                    MessageBox.Show("请先在左侧选择一个分类", "提示"); // 弹出提示框，要求用户先选择一个分类
                    return; // 结束方法执行
                }
                // 2. 尝试从分类映射中获取对应的文件名
                // _owner._categoryFileMap 是一个字典（Dictionary），存储了二级分类名称和对应的 CSV 文件名。
                // TryGetValue 方法尝试获取 _owner._currentCategory 对应的值（即文件名），并将其赋值给 fileName 变量。
                // 如果找不到对应的键（分类），则返回 false。
                if (!_owner._categoryFileMap.TryGetValue(_owner._currentCategory, out string fileName))
                {
                    MessageBox.Show($"未找到分类 '{_owner._currentCategory}' 对应的数据文件", "错误");
                    return;
                }
                // 3. 构建 CSV 文件的完整路径
                // AppDomain.CurrentDomain.BaseDirectory 获取当前应用程序的基目录（通常是可执行文件所在的目录）。
                // Path.Combine 将基目录和 "file" 文件夹以及获取到的 fileName 组合成完整的 CSV 文件路径。
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file", fileName);
                try
                {
                    // 4. 检查文件是否存在
                    if (!File.Exists(filePath))
                    {
                        // 如果文件不存在，则调用 CreateEmptyCsv 方法创建一个新的空 CSV 文件。
                        // 这个方法会根据预定义的表头写入第一行，确保文件结构正确。
                        CreateEmptyCsv(filePath);
                    }
                    // 5. 使用系统默认程序打开文件
                    // Process.Start 用于启动一个新进程。
                    // ProcessStartInfo 包含了启动进程所需的信息。
                    // FileName = filePath: 指定要打开的文件路径。
                    // UseShellExecute = true: 表示使用操作系统外壳（shell）来启动进程，这样系统会根据文件类型自动选择关联的程序（例如，.csv 文件通常会用 Excel 打开）。
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    // 6. 弹出操作提示
                    MessageBox.Show("请在Excel中编辑并保存后，点击'更新数据'按钮刷新表格", "提示");
                }
                catch (Exception ex)
                {
                    // 7. 捕获并处理异常
                    // 如果在尝试打开文件或执行其他操作时发生任何错误（例如文件被占用，或没有关联程序等），
                    // 则捕获异常并显示错误信息。
                    MessageBox.Show($"Excel/CSV Error: {ex}", "错误");
                }
            }

            private void BtnSync_Click(object sender, EventArgs e)
            {
                if (string.IsNullOrEmpty(_owner._currentCategory))
                {
                    MessageBox.Show("请先在左侧选择一个分类", "提示");
                    return;
                }

                if (!_owner._categoryFileMap.TryGetValue(_owner._currentCategory, out string fileName))
                {
                    MessageBox.Show($"未找到分类 '{_owner._currentCategory}' 对应的数据文件", "错误");
                    return;
                }

                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file", fileName);
                try
                {
                    LoadCsvData(filePath);
                    MessageBox.Show($"已成功更新 '{_owner._currentCategory}' 数据", "成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"同步失败: {ex}", "错误");
                }
            }



            private void BtnExport_Click(object sender, EventArgs e)
            {
                // 设置对话框结果为OK，表示用户确认操作
                this.DialogResult = DialogResult.OK;
                // 关闭当前窗口
                this.Close();
            }

            // 此事件处理程序在用户点击复选框后立即提交更改
            private void DataGridView_CurrentCellDirtyStateChanged(object sender, EventArgs e)
            {
                if (_dataGridView.IsCurrentCellDirty) //
                {
                    _dataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit); //
                }
            }

            // 此事件在复选框的值更改后触发
            private void DataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
            {
                // 确保事件是针对“选中”列和有效行
                if (e.RowIndex < 0 || e.ColumnIndex != _table.Columns["选中"].Ordinal) //
                    return;

                // 临时分离事件处理程序以防止递归调用
                _dataGridView.CellValueChanged -= DataGridView_CellValueChanged;
                try
                {
                    DataRow changedRow = _table.Rows[e.RowIndex];
                    // 如果此复选框刚刚被选中，则取消选中所有其他行
                    if ((bool)changedRow["选中"]) //
                    {
                        foreach (DataRow row in _table.Rows)
                        {
                            if (row != changedRow)
                            {
                                row["选中"] = false; //
                            }
                        }
                    }
                }
                finally
                {
                    // 重新附加事件处理程序
                    _dataGridView.CellValueChanged += DataGridView_CellValueChanged;

                }
            }
            private DataTable ImportDataTableFromCsv(string filePath)
            {
                var dt = new DataTable();
                try
                {
                    using (var sr = new System.IO.StreamReader(filePath, System.Text.Encoding.UTF8))
                    {
                        string line;
                        bool isFirst = true;
                        string[] headers = null;
                        while ((line = sr.ReadLine()) != null)
                        {
                            var parts = ParseCsvLine(line);
                            if (isFirst)
                            {
                                headers = parts;
                                foreach (var header in headers)
                                {
                                    dt.Columns.Add(header, typeof(string));
                                }
                                isFirst = false;
                            }
                            else
                            {
                                // 若列数不足，补空字符串
                                var row = dt.NewRow();
                                for (int i = 0; i < dt.Columns.Count; i++)
                                {
                                    row[i] = i < parts.Length ? parts[i] : "";
                                }
                                dt.Rows.Add(row);
                            }
                        }
                    }
                    // 检查是否有乱码
                    foreach (DataRow row in dt.Rows)
                    {
                        foreach (var obj in row.ItemArray)
                        {
                            if (obj?.ToString().Contains("�") == true)
                                return TryImportWithOtherEncodings(filePath);
                        }
                    }
                }
                catch
                {
                    return TryImportWithOtherEncodings(filePath);
                }
                return dt;

            }
            // 若UTF-8失败，尝试GB2312和系统默认编码
            private DataTable TryImportWithOtherEncodings(string filePath)
            {
                var encodings = new[] { System.Text.Encoding.GetEncoding("GB2312"), System.Text.Encoding.Default };
                foreach (var enc in encodings)
                {
                    var dt = new DataTable();
                    try
                    {
                        using (var sr = new System.IO.StreamReader(filePath, enc))
                        {
                            string line;
                            bool isFirst = true;
                            string[] headers = null;
                            while ((line = sr.ReadLine()) != null)
                            {
                                var parts = ParseCsvLine(line);
                                if (isFirst)
                                {
                                    headers = parts;
                                    foreach (var header in headers)
                                        dt.Columns.Add(header, typeof(string));
                                    isFirst = false;
                                }
                                else
                                {
                                    var row = dt.NewRow();
                                    for (int i = 0; i < dt.Columns.Count; i++)
                                        row[i] = i < parts.Length ? parts[i] : "";
                                    dt.Rows.Add(row);
                                }
                            }
                        }
                        // 检查乱码
                        foreach (DataRow row in dt.Rows)
                        {
                            foreach (var obj in row.ItemArray)
                            {
                                if (obj?.ToString().Contains("�") == true)
                                    goto NextEncoding;
                            }
                        }
                        return dt;
                    }
                    catch { }
                NextEncoding:;
                }
                return new DataTable();
            }


            // 简单CSV解析，支持逗号和引号
            private string[] ParseCsvLine(string line)
            {
                var result = new List<string>();
                bool inQuotes = false;
                var value = "";
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == '\"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                        {
                            value += '\"';
                            i++;
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        result.Add(value);
                        value = "";
                    }
                    else
                    {
                        value += c;
                    }
                }
                result.Add(value);
                return result.ToArray();
            }


            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                base.OnFormClosing(e);

                // 移除所有空行
                for (int i = _table.Rows.Count - 1; i >= 0; i--)
                {
                    var row = _table.Rows[i];
                    bool isEmpty = true;
                    foreach (var item in row.ItemArray)
                    {
                        if (item != null && !string.IsNullOrWhiteSpace(item.ToString()))
                        {
                            isEmpty = false;
                            break;
                        }
                    }
                    if (isEmpty)
                        _table.Rows.RemoveAt(i);
                }

                // 只需刷新组件
                _owner.ExpireSolution(true);
            }



        }

        // 组件UI属性
        private class MaterialPropertyAttributes : GH_ComponentAttributes
        {
            public bool InvokeRequired { get; internal set; }

            public MaterialPropertyAttributes(cw2dComponent1 owner) : base(owner)
            {
                _dataRowBounds = new List<RectangleF>();
            }
            private RectangleF _bottomButtonBounds; // 底部“选取属性”按钮的区域
            private List<RectangleF> _dataRowBounds; // 存储每一行数据的矩形
            private RectangleF _bottomLabelBounds; // 底部文本标签的区域 (新增)

            private cw2dComponent1 Component => (cw2dComponent1)Owner;

            // 按钮区域定义
            //private Rectangle ButtonRect => new Rectangle(
            //    (int)Bounds.Right - 100,  // X坐标
            //    (int)Bounds.Bottom - 25,  // Y坐标
            //    90,  // 宽度
            //    20); // 高度

            // 重新计算电池的布局和边界
            protected override void Layout()
            {
                base.Layout();
                this.Bounds = new System.Drawing.Rectangle
                    (
                     (int)this.Bounds.X - 108,//中间图标 右移动- 左移动+ 
                     (int)this.Bounds.Y - 90,//中间图标 下移动- 上移动+ 
                     (int)this.Bounds.Width + 108,//增加50像素 电池整体右侧增加50 后面的位置也变了
                     (int)this.Bounds.Height + 120);
            }

            //电池外观设计
            protected override void Render(GH_Canvas canvas, System.Drawing.Graphics graphics, GH_CanvasChannel channel)
            {
                // 首先，调用基类的 Render 方法绘制电池的基础部分（背景、名称、输入输出端口等）。
                base.Render(canvas, graphics, channel);

                // 我们只在 "Objects" 通道上进行绘制，这是绘制组件主体内容的标准通道。
                if (channel == GH_CanvasChannel.Objects)
                {
                    // 绘制按钮
                    // 定义按钮的矩形区域。这个区域的坐标是相对于画布(canvas)的绝对坐标
                    var buttonRect = new System.Drawing.Rectangle(
                        (int)(this.Bounds.X),
                        (int)(this.Bounds.Bottom - 25),
                        170,
                        20);
                    // 设置文本格式，使其在矩形内居中对齐。
                    // StringFormat 对象：它负责文本的布局和对齐方式
                    //var textFormat = new StringFormat
                    //{
                    //    // 文本格式：左对齐，垂直居中
                    //    Alignment = StringAlignment.Near,
                    //    LineAlignment = StringAlignment.Center
                    //};

                    //    // 我们只在 "Objects" 通道上进行绘制，这是绘制组件主体内容的标准通道。
                    //    if (channel == GH_CanvasChannel.Objects)
                    //    {
                    //        // 绘制按钮
                    //        // 定义按钮的矩形区域。这个区域的坐标是相对于画布(canvas)的绝对坐标
                    //        var buttonRect = new System.Drawing.Rectangle(
                    //            (int)(this.Bounds.X),
                    //            (int)(this.Bounds.Bottom - 25),
                    //            170,
                    //            20);
                    //        // 设置文本格式，使其在矩形内居中对齐。
                    //        // StringFormat 对象：它负责文本的布局和对齐方式
                    //        //var textFormat = new StringFormat
                    //        //{
                    //        //    // 文本格式：左对齐，垂直居中
                    //        //    Alignment = StringAlignment.Near,
                    //        //    LineAlignment = StringAlignment.Center
                    //        //};

                    var buttonFormat = new StringFormat
                    {
                        // 文本格式：中间对齐，垂直居中
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    // 统一的字体
                    var font = new System.Drawing.Font("宋体", 6, System.Drawing.FontStyle.Bold); ;//6f与6 的区别
                    // 边框画笔
                    var borderPen = System.Drawing.Pens.DarkGray;
                    // 标签背景画刷
                    var labelBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(210, 210, 210));
                    // 底部按钮背景画刷 (图1中的洋红色/粉色)
                    var bottomButtonFillBrush = new System.Drawing.SolidBrush(System.Drawing.Color.DeepPink);

                    var valueBrush = System.Drawing.Brushes.White;

                    var dataToShow = new List<Tuple<string, string>>();
                    DataRow selectedRow = null;

                    //        var dataToShow = new List<Tuple<string, string>>();
                    //        DataRow selectedRow = null;




                    // 在 Component._propertyTable 中查找选中的行
                    if (Component._propertyTable != null && Component._propertyTable.Rows.Count > 0)
                    {
                        foreach (DataRow row in Component._propertyTable.Rows)
                        {
                            if (row.Table.Columns.Contains("选中") && row["选中"] is bool selected && selected)
                            {
                                selectedRow = row;
                                break;
                            }
                        }
                    }
                    // 根据选中的行或默认值填充 dataToShow
                    if (selectedRow != null)
                    {
                        // 将 DataTable 列映射到显示标签
                        dataToShow.Add(Tuple.Create("物料名称:", GetValueOrNA(selectedRow, "物料名称")));
                        dataToShow.Add(Tuple.Create("物料编码:", GetValueOrNA(selectedRow, "物料编码")));
                        dataToShow.Add(Tuple.Create("工程属性:", GetValueOrNA(selectedRow, "工程属性")));
                        dataToShow.Add(Tuple.Create("类型代号:", GetValueOrNA(selectedRow, "类型代号")));
                    }
                    else
                    {
                        // 如果没有选中行，显示默认数据
                        dataToShow.Add(Tuple.Create("物料名称:", "/"));
                        dataToShow.Add(Tuple.Create("物料编码:", "/"));
                        dataToShow.Add(Tuple.Create("工程属性:", "/"));
                        dataToShow.Add(Tuple.Create("类型代号:", "/"));
                    }
                    // ------ 4. 循环绘制每一行数据 ------ 展示列表的信息
                    int rowHeight = 20; // 每行的高度
                    int labelWidth = 170; // 左侧标签的宽度
                    float currentY = this.Bounds.Y + 5;//+ 24; // 从标题栏下方开始绘制
                    foreach (var item in dataToShow)
                    {
                        // 定义标签和值的矩形区域
                        var labelRect = new System.Drawing.Rectangle(
                            (int)this.Bounds.X,
                            (int)currentY,
                            labelWidth,
                            rowHeight);

                        var valueRect = new System.Drawing.Rectangle(
                            (int)this.Bounds.X + labelWidth,
                            (int)currentY,
                            (int)this.Bounds.Width - labelWidth,
                            rowHeight);

                        // 绘制背景
                        graphics.FillRectangle(labelBrush, labelRect);
                        graphics.FillRectangle(valueBrush, valueRect);

                        // 绘制边框
                        graphics.DrawRectangle(borderPen, labelRect);
                        graphics.DrawRectangle(borderPen, valueRect);

                        // 绘制文字 (给文字区域留一点边距)
                        labelRect.Inflate(-5, 0);
                        valueRect.Inflate(-5, 0);

                        graphics.DrawString(item.Item1 + item.Item2, font, System.Drawing.Brushes.Black, labelRect, buttonFormat);
                        // Y坐标下移，为下一行做准备
                        currentY += rowHeight;

                    }


                    // 释放GDI资源
                    labelBrush.Dispose();
                    var myFont = new System.Drawing.Font("微软雅黑", 6, System.Drawing.FontStyle.Bold); // 6磅的粗体 宋体字体
                    //// 使用 GDI+ 的 graphics 对象进行绘制：
                    //// 1. 填充一个浅灰色矩形作为按钮背景。
                    graphics.FillRectangle(bottomButtonFillBrush, buttonRect);
                    //// 2. 绘制一个深灰色边框。
                    graphics.DrawRectangle(System.Drawing.Pens.DarkGray, buttonRect);

                    graphics.DrawString("添加属性", myFont, System.Drawing.Brushes.Black, buttonRect, buttonFormat);
                    myFont.Dispose();
                }
            }
            //电池交互响应设计
            public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
            {

                // 如果按下的是鼠标左键
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    // 转换坐标到组件局部坐标
                    // // 将画布的绝对坐标 (e.CanvasLocation) 转换为相对于电池左上角的局部坐标 (pt)。
                    System.Drawing.Point pt = GH_Convert.ToPoint(e.CanvasLocation);
                    pt.X -= (int)this.Bounds.X;
                    pt.Y -= (int)this.Bounds.Y;

                    // 重新计算按钮位置（相对于组件边界）
                    //// 定义按钮的点击区域（矩形范围）。这个矩形的坐标是相对于电池的。
                    // 注意：这个矩形必须和你之后在 Render 方法中绘制的按钮位置完全一致。
                    var buttonRect = new System.Drawing.Rectangle(
                        (int)(this.Bounds.Width - 170),
                        (int)(this.Bounds.Height - 20),
                        170,
                        20);//选中

                    // 检查鼠标点击的局部坐标 (pt) 是否在按钮的矩形区域内
                    if (buttonRect.Contains(pt))
                    {
                        // 先同步一次数据
                        //SyncDataFromCsv();

                        // 再弹出属性编辑窗口
                        var ownerComponent = (cw2dComponent1)this.Owner;
                        var form = new PropertyForm(ref ownerComponent);
                        Grasshopper.Instances.ActiveCanvas.Invoke(new Action(() => form.ShowDialog()));

                        Owner.ExpireSolution(true);
                        // 4. 返回 Handled，告诉Grasshopper这个鼠标事件已经被我们处理了，不需要再做其他响应。

                        return GH_ObjectResponse.Handled;
                    }
                }
                // 如果没点击按钮或不是左键，则调用基类的方法进行默认处理（比如拖动电池）。
                return base.RespondToMouseDown(sender, e);
            }
            private void SyncDataFromCsv()
            {
                try
                {
                    // 获取组件实例
                    var component = (cw2dComponent1)Owner;
                    if (component._isDataLoaded)
                    {
                        //只有首次打开时会自动同步数据
                        return;
                    }
                    // 构建CSV文件路径
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file", "goujian.csv");

                    // 从CSV导入数据
                    var newTable = ImportDataTableFromCsv(filePath);

                    if (newTable != null && newTable.Rows.Count > 0)
                    {
                        component._propertyTable.Rows.Clear();
                        foreach (DataRow rawRow in newTable.Rows)
                        {
                            DataRow newRow = component._propertyTable.NewRow();
                            // 忽略CSV中的"选中"值
                            newRow["选中"] = false;

                            foreach (DataColumn col in newTable.Columns)
                            {
                                string colName = col.ColumnName;
                                // 跳过"选中"列
                                if (colName == "选中") continue;

                                if (component._propertyTable.Columns.Contains(colName))
                                {
                                    newRow[colName] = rawRow[colName];
                                }
                                //if (component._propertyTable.Columns.Contains(col.ColumnName))
                                //{
                                //    newRow[col.ColumnName] = rawRow[col.ColumnName];
                                //}
                            }
                            component._propertyTable.Rows.Add(newRow);
                        }
                        ;
                        component._isDataLoaded = true;
                        // 显示成功消息
                        component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                            "已从CSV文件同步数据");
                    }
                    else
                    {
                        // 显示警告消息
                        component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "CSV文件内容无效或为空");
                    }
                }
                catch (Exception ex)
                {
                    // 显示错误消息
                    Owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"同步失败: {ex.Message}");
                }
            }
            private DataTable ImportDataTableFromCsv(string filePath)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        Owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                            $"CSV文件未找到: {filePath}");
                        return null;
                    }

                    DataTable table = new DataTable();

                    using (var reader = new StreamReader(filePath, Encoding.UTF8))
                    {
                        // 读取表头
                        string headerLine = reader.ReadLine();
                        if (headerLine == null) return null;

                        var headers = ParseCsvLine(headerLine);
                        foreach (var header in headers)
                        {
                            table.Columns.Add(header.Trim('"'), typeof(string));
                        }

                        // 读取数据行
                        while (!reader.EndOfStream)
                        {
                            string dataLine = reader.ReadLine();
                            if (string.IsNullOrWhiteSpace(dataLine)) continue;

                            var fields = ParseCsvLine(dataLine);
                            DataRow row = table.NewRow();

                            for (int i = 0; i < Math.Min(fields.Length, table.Columns.Count); i++)
                            {
                                row[i] = fields[i].Trim('"');
                            }

                            table.Rows.Add(row);
                        }
                    }
                    return table;
                }
                catch (Exception ex)
                {
                    Owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"CSV导入错误: {ex.Message}");
                    return null;
                }
            }

            // CSV行解析方法
            private string[] ParseCsvLine(string line)
            {
                var result = new List<string>();
                bool inQuotes = false;
                var currentField = new StringBuilder();

                foreach (char c in line)
                {
                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        result.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }

                // 添加最后一个字段
                result.Add(currentField.ToString());
                return result.ToArray();
            }

            private string GetValueOrNA(DataRow row, string columnName)
            {
                return row.Table.Columns.Contains(columnName) && !string.IsNullOrEmpty(row[columnName]?.ToString())
                    ? row[columnName].ToString()
                    : "N/A";
            }
        }
        internal void Invoke(Action action)
        {
            throw new NotImplementedException();
        }


        protected override Bitmap Icon
        {
            get
            {
                try
                {
                    // 1. 获取当前鼠标悬停状态（配合Attributes类）
                    bool isHovering = false;
                    if (Attributes is GH_ComponentAttributes attributes)
                    {
                        // 通过反射获取私有字段判断悬停状态（Grasshopper内部实现方式）
                        var field = typeof(GH_ComponentAttributes).GetField("m_mouseOver",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        isHovering = (bool)(field?.GetValue(attributes) ?? false);
                    }

                    // 2. 加载原始图标
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "CW2D.Resources.BoLi.png";

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




        public override Guid ComponentGuid => new Guid("e61fec3c-ce77-4ea9-bc07-5ea41a494767");
    }
}