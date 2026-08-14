using System;
using System.Collections.Generic;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Drawing;
using CW2D.Attributes;
namespace CW2D
{
    public class GeometryWithAttribute : GH_Component
    {
        private List<GH_AttributeData> _cache = new List<GH_AttributeData>();

        public GeometryWithAttribute() : base("绑定属性", "绑定属性", "将几何体与其属性绑定", Title.CW2D(), Title.Attribute())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("开关", "B", "True时执行绑定（可接Button）；False时输出上一次结果", GH_ParamAccess.item, false);
            pManager.AddGeometryParameter("几何体", "G", "要绑定属性的几何体", GH_ParamAccess.list);
            pManager.AddTextParameter("属性", "A", "要绑定的属性值", GH_ParamAccess.item);

            // 未触发时允许不输入属性
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("绑定结果", "结果", "绑定属性后的几何体", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool trigger = false;
            DA.GetData(0, ref trigger);

            var geometries = new List<IGH_GeometricGoo>();
            if (!DA.GetDataList(1, geometries)) return;

            // Button是瞬时True：未触发时输出上一次结果（保持输出）
            if (!trigger)
            {
                DA.SetDataList(0, _cache);
                Message = "未触发";
                return;
            }

            string strings = null;
            if (!DA.GetData(2, ref strings) || string.IsNullOrWhiteSpace(strings))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "触发为True时必须提供属性字符串");
                DA.SetDataList(0, new List<GH_AttributeData>());
                Message = "缺少属性";
                return;
            }

            int len = strings.Length;
            string key = null, value = null;
            bool flag = true;
            var attributes = new Dictionary<string, string>();

            for (int i = 0; i < len; i++)
            {
                switch (strings[i])
                {
                    case '=':
                        flag = false;
                        continue;
                    case ';':
                        if (key != null && value != null)
                            attributes[key] = value;
                        key = value = null;
                        flag = true;
                        continue;
                    default:
                        if (flag) key += strings[i];
                        else value += strings[i];
                        continue;
                }
            }

            // 如果最后一段没有以 ; 结尾，补入一次
            if (key != null && value != null)
                attributes[key] = value;

            var ghAttrDatas = new List<GH_AttributeData>();
            foreach (var geometry in geometries)
            {
                if (geometry == null) continue;
                var geo = geometry.DuplicateGeometry();
                var attrData = new AttributeData(geo, attributes);
                ghAttrDatas.Add(new GH_AttributeData(attrData));
            }

            _cache = ghAttrDatas;
            DA.SetDataList(0, ghAttrDatas);
            Message = "已绑定";
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
                    var resourceName = "CW2D.Resources.Binding Properties.png";

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

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
        }
        public override Guid ComponentGuid => new Guid("65AA0287-4722-4FD9-A868-0D8F6E555AD9");
    }
}