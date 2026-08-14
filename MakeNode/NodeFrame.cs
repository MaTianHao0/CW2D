using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using TitleBlockBattery;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CW2D.MakeNode
{
    public class NodeFrame : GH_Component
    {
        string _filePath;

        TitleBlockManager _manager = new TitleBlockManager();

        public NodeFrame() : base("节点图框", "节点图框", "节点图框", Title.CW2D(), Title.Node())
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("节点图", "节点图", "节点图", GH_ParamAccess.list);
            pManager.AddTextParameter("节点图索引", "节点图索引", "节点图索引", GH_ParamAccess.item, string.Empty);
            pManager.AddNumberParameter("字高", "字高", "字高", GH_ParamAccess.item, 20.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("节点图框", "节点图框", "节点图框", GH_ParamAccess.list); 
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            var manager = new TitleBlockManager();
            var config = manager.GetConfig();
            _filePath = Path.Combine(config.DefaultTemplatePath, "A3_Frame.dwg");
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geometries = new List<GeometryBase>();
            var index = string.Empty;
            var textHeight = 20.0;
            if (!DA.GetDataList(0, geometries)) return;
            if (!DA.GetData(1, ref index)) return;
            if (!DA.GetData(2, ref textHeight)) return;

            var bbox = BoundingBox.Empty;
            foreach (var geo in geometries)
            {
                if (geo == null) continue;
                bbox.Union(geo.GetBoundingBox(false));
            }

            var center = bbox.Center;
            var frame = GetFrame(center);
            var corner = center + new Vector3d(-2100, 1500, 0);

            var info = _manager.GetConfig().FrameInfo;
            foreach (var geo in frame)
            {
                if (geo is TextEntity textEntity)
                {
                    textEntity.TextHeight = textHeight * 0.08;
                    switch (textEntity.PlainText)
                    {
                        case "XX01": textEntity.PlainText = info.ChiefDesigner; break;
                        case "XX02": textEntity.PlainText = info.Approver; break;
                        case "XX03": textEntity.PlainText = info.Reviewer; break;
                        case "XX04": textEntity.PlainText = info.ProfessionalLead; break;
                        case "XX05": textEntity.PlainText = info.Checker; break;
                        case "XX06": textEntity.PlainText = info.Designer; break;
                        case "XX07": textEntity.PlainText = info.Client; break;
                        case "XX08": textEntity.PlainText = info.ProjectName; break;
                        case "XX09": textEntity.PlainText = info.SubProjectName; break;
                        case "XX10": textEntity.PlainText = info.DrawingName; break;
                        case "XX11": textEntity.PlainText = info.ProjectCode; break;
                        case "XX12": textEntity.PlainText = info.Discipline; break;
                        case "XX13": textEntity.PlainText = info.Version; break;
                        case "XX14": textEntity.PlainText = info.Phase; break;
                        case "XX15": textEntity.PlainText = info.Date; break;
                        case "XX16": textEntity.PlainText = info.DrawingNumber; break;
                        case "XX17": textEntity.PlainText = info.Barcode; break;
                    }
                }
            }

            var plane = new Plane(corner + new Vector3d(textHeight / 2, textHeight * 15, 0), Vector3d.ZAxis);
            var dimStyle = Style.SetDimensionStyle(textHeight);
            var text = TextEntity.Create(index, plane, dimStyle, false, textHeight, 0);
            frame.Add(text);

            DA.SetDataList(0, frame);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            menu.Items.Add("信息设置", null, EditFrameInfo);
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

        List<GeometryBase> GetFrame(Point3d target)
        {
            var frame = new List<GeometryBase>();
            var bbox = BoundingBox.Empty;
            using (var tempDoc = RhinoDoc.CreateHeadless(null))
            {
                tempDoc.ModelUnitSystem = UnitSystem.Millimeters;

                var options = new FileDwgReadOptions()
                {
                    ImportUnreferencedLayers = true,
                    ImportUnreferencedBlocks = true,
                    ImportUnreferencedLinetypes = true,
                    ConvertWidePolylinesToSurfaces = true,
                    IgnoreThickness = true,
                    ConvertRegionsToCurves = true,
                    MeshPrecision = FileDwgReadOptions.MeshPrecisionMode.DoublePrecision,
                    ModelUnits = UnitSystem.Millimeters,
                    LayoutUnits = UnitSystem.Millimeters,
                    SetLayerMaterialToLayerColor = false
                };

                if (!FileDwg.Read(_filePath, tempDoc, options))
                {
                    throw new Exception($"FileDwg.Read 失败: {Path.GetFileName(_filePath)}");
                }

                foreach (var obj in tempDoc.Objects)
                {
                    var geo = obj.Geometry.Duplicate();
                    if (geo != null)
                    {
                        frame.Add(geo);
                        bbox.Union(geo.GetBoundingBox(false));
                    }
                }
            }

            var center = bbox.Center;
            foreach (var geo in frame)
            {
                geo.Translate(target - center);
            }

            return frame;
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
                    var resourceName = "CW2D.Resources.Node-frame.png";

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

        public override Guid ComponentGuid
        {
            get { return new Guid("4D82B869-BA6D-49BA-AB57-064B315ADAD8"); }
        }
    }
}