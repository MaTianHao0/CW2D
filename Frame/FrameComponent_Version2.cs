using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TitleBlockBattery
{
    public class ModernTitleBlockComponent : GH_Component
    {
        private TitleBlockManager _manager;
        private ModernTitleBlockReader _reader;

        public ModernTitleBlockComponent()
            : base(name: "图框电池",
                  nickname: "图框",
                description: "Generate title blocks from DWG templates",
                category: "CW2D",
                subCategory: "TB")
        {
            _manager = new TitleBlockManager();
            _reader = new ModernTitleBlockReader();
        }

        public override Guid ComponentGuid => new Guid("87654321-4321-8765-4321-876543218765");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("基准点", "BP", "Reference point for title block placement", GH_ParamAccess.item);
            pManager.AddTextParameter("图框尺寸", "S", "Frame size (A0, A1, A2, A3, A4)", GH_ParamAccess.item, "A4");
            pManager.AddTextParameter("文件路径", "P", "Path to DWG template folder (optional)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("运行", "G", "Generate title block", GH_ParamAccess.item, false);
            pManager.AddTextParameter("质量", "Q", "Import quality: Fast, Normal, High", GH_ParamAccess.item, "Normal");

            pManager[2].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("曲线", "C", "Title block frame curves", GH_ParamAccess.list);
            pManager.AddTextParameter("文字", "T", "Text content from title block", GH_ParamAccess.list);
            pManager.AddGeometryParameter("图框", "G", "All geometry from title block", GH_ParamAccess.list);
            pManager.AddTextParameter("状态", "I", "Processing information and status", GH_ParamAccess.item);
            pManager.AddTextParameter("调试", "D", "Debug information", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d basePoint = Point3d.Origin;
            string frameSize = "A3";
            string templatePath = "";
            bool generate = false;
            string quality = "Normal";

            if (!DA.GetData(0, ref basePoint)) return;
            if (!DA.GetData(1, ref frameSize)) return;
            DA.GetData(2, ref templatePath);
            if (!DA.GetData(3, ref generate)) return;
            DA.GetData(4, ref quality);

            DA.SetDataList(0, new List<Curve>());
            DA.SetDataList(1, new List<string>());
            DA.SetDataList(2, new List<GeometryBase>());

            if (!generate)
            {
                DA.SetData(3, "Set Generate to True to create title block using FileDwg API");
                DA.SetData(4, "Ready to generate");
                return;
            }

            try
            {
                var debugInfo = $"Using FileDwg.Read API at {DateTime.Now:HH:mm:ss}\n";

                _manager = new TitleBlockManager(); // 重新加载最新配置
                var config = _manager.GetConfig();

                if (string.IsNullOrEmpty(templatePath))
                {
                    templatePath = config.DefaultTemplatePath;

                    if (string.IsNullOrEmpty(templatePath))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            "No template path provided and no default path set");
                        DA.SetData(3, "Please configure template path in settings");
                        DA.SetData(4, debugInfo + "ERROR: No template path configured");
                        return;
                    }
                    debugInfo += $"Using default path: {templatePath}\n";
                }
                else
                {
                    debugInfo += $"Using provided path: {templatePath}\n";
                }

                debugInfo += $"Frame size: {frameSize}, Quality: {quality}\n";

                var frameInfo = config.FrameInfo ?? new TitleFrameInfo();
                config.FrameInfo = frameInfo;
                _manager.UpdateConfig(config);

                var result = _reader.ReadTitleBlock(templatePath, frameSize, basePoint, frameInfo);

                DA.SetDataList(0, result.Curves);
                DA.SetDataList(1, result.TextObjects);
                DA.SetDataList(2, result.AllGeometry);
                DA.SetData(3, result.Info);

                debugInfo += $"Success: {result.AllGeometry.Count} objects imported\n";
                debugInfo += $"Curves: {result.Curves.Count}, Texts: {result.TextObjects.Count}";
                DA.SetData(4, debugInfo);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                DA.SetData(3, $"Error: {ex.Message}");
                DA.SetData(4, $"Exception at {DateTime.Now:HH:mm:ss}: {ex.Message}");
            }
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            menu.Items.Add("Settings...", null, OpenSettings);
            menu.Items.Add("Edit Frame Info...", null, EditFrameInfo);
            menu.Items.Add("History Presets...", null, OpenHistoryPresets);
            menu.Items.Add("Test DWG Import", null, TestDwgImport);
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            using (var form = new TitleBlockForm())
            {
                form.ShowDialog();
            }
        }

        private void EditFrameInfo(object sender, EventArgs e)
        {
            try
            {
                var config = _manager.GetConfig();
                if (config.FrameInfo == null)
                    config.FrameInfo = new TitleFrameInfo();

                // 通过反射创建 TitleBlockInfoForm，避免“类型未找到”报错阻塞编译
                var formType = Type.GetType("TitleBlockBattery.TitleBlockInfoForm");
                if (formType == null)
                {
                    MessageBox.Show(
                        "未找到 TitleBlockInfoForm 类型。请确认该窗体已包含在项目中。\n（已优雅降级：继续使用当前配置）",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var form = (Form)Activator.CreateInstance(formType, new object[] { config.FrameInfo }))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        // 反射拿回更新后的 FrameInfo
                        var getMethod = formType.GetMethod("GetFrameInfo", BindingFlags.Public | BindingFlags.Instance);
                        if (getMethod != null)
                        {
                            var newInfo = getMethod.Invoke(form, null) as TitleFrameInfo;
                            if (newInfo != null)
                            {
                                config.FrameInfo = newInfo;
                                _manager.UpdateConfig(config);

                                string info = $"图框信息已更新！\n" +
                                              $"设计人: {config.FrameInfo.Designer}\n" +
                                              $"日期: {config.FrameInfo.Date}";
                                MessageBox.Show(info, "信息更新成功",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                                this.ExpireSolution(true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑图框信息时出错: {ex.Message}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenHistoryPresets(object sender, EventArgs e)
        {
            try
            {
                using (var form = new TitleBlockHistoryForm(_manager))
                {
                    form.ShowDialog();
                    this.ExpireSolution(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开历史记录时出错: {ex.Message}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TestDwgImport(object sender, EventArgs e)
        {
            try
            {
                var config = _manager.GetConfig();
                if (string.IsNullOrEmpty(config.DefaultTemplatePath))
                {
                    MessageBox.Show("Please configure default template path first.");
                    return;
                }

                var testFile = System.IO.Path.Combine(config.DefaultTemplatePath, "A3_Frame.dwg");
                if (System.IO.File.Exists(testFile))
                {
                    MessageBox.Show($"DWG file found and accessible:\n{testFile}");
                }
                else
                {
                    MessageBox.Show($"Test file not found:\n{testFile}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Test failed: {ex.Message}");
            }
        }
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
                    var resourceName = "CW2D.Resources.FRAME.png";

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

    }

}
