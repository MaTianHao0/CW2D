using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.Others
{
    public class Bake : GH_Component
    {
        public Bake()
          : base("烘焙图纸", "烘焙图纸",
              "将图纸烘焙至Rhino",
              Title.CW2D(), Title.Others())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("曲线", "曲线", "要被烘焙的曲线", GH_ParamAccess.list);
            pManager.AddBooleanParameter("是否烘焙", "是否烘焙", "是否将平面图烘焙至Rhino，默认为否", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("曲线", "曲线", "原曲线", GH_ParamAccess.list);
        }

        protected override void BeforeSolveInstance()
        {
            if (!Style.InitializeOrNot)
            {
                Style.Initialize();
                Style.InitializeOrNot = true;
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geometries = new List<GeometryBase>();
            var flag = false;
            if (!DA.GetDataList(0, geometries)) return;
            if (!DA.GetData(1, ref flag)) return;

            if (geometries.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "输入为零");
                return;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (flag)
            {
                foreach (var geometry in geometries)
                {
                    if (geometry != null)
                    {
                        var type = geometry.GetUserString("类型");
                        var linetypeName = geometry.GetUserString("线型");
                        var colorNum = geometry.GetUserString("色号");
                        var layerName = string.Empty;
                        switch (type)
                        {
                            case "钢材":
                                layerName = "02B2-钢材";
                                break;
                            case "型材":
                                layerName = "03B2-型材";
                                break;
                            case "铝板":
                                layerName = "04B2-铝板";
                                break;
                            case "玻璃":
                                layerName = "05B2-玻璃";
                                break;
                            case "石材":
                                layerName = "06B2-石材";
                                break;
                            case "附件":
                                layerName = "07B2-附件";
                                break;
                            case "填充":
                                layerName = "08B2-填充";
                                break;
                            case "结构":
                                layerName = "09B2-结构";
                                break;
                            case "虚线":
                                layerName = "10B2-虚线";
                                break;
                            case "轴线":
                                layerName = "11B2-轴线";
                                break;
                            case "边界线":
                                layerName = "12B2-边界线";
                                break;
                            case "辅助线":
                                layerName = "13B2-辅助线";
                                break;
                            case "图元":
                                layerName = "14B2-图元";
                                break;
                            case "图框":
                                layerName = "15B2-图框";
                                break;
                            case "标注":
                                layerName = "16B2-标注";
                                break;
                            case "轮廓线":
                                layerName = "17B2-轮廓线";
                                break;
                            case "双点划线":
                                layerName = "18B2-双点划线";
                                break;
                            default:
                                layerName = "Default";
                                break;
                        }

                        var style = new Style();
                        var layerIndex = Style.FindLayer(layerName);
                        style.SetLayer(layerIndex);

                        if (linetypeName != null)
                        {
                            var linetypeIndex = Style.FindLinetype(linetypeName);
                            style.SetLinetype(linetypeIndex);
                        }

                        if (colorNum != null)
                        {
                            string[] nums = colorNum.Split('，');
                            int.TryParse(nums[0], out var R);
                            int.TryParse(nums[1], out var G);
                            int.TryParse(nums[2], out var B);
                            var color = Color.FromArgb(R, G, B);
                            style.SetColor(color);
                        }

                        var attribute = style.Attributes;
                        doc.Objects.Add(geometry, attribute);
                    }
                }
            }

            doc.Views.Redraw();
            DA.SetDataList(0, geometries);
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
                    var resourceName = "CW2D.Resources.ShiGaoBan.png";

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
                            RhinoApp.WriteLine("找不到资源，可用资源:\n" +
                                string.Join("\n", availableResources));
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"图标处理失败: {ex.Message}");
                    return null;
                }
            }
        }

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


        public override Guid ComponentGuid => new Guid("B4334E38-ACB3-4067-964A-6960112DA147");
    }
}