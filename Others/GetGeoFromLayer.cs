using CW2D.Attributes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Microsoft.VisualBasic;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.Others
{
    public class GetGeoFromLayer : GH_Component
    {
        public GetGeoFromLayer()
          : base("根据图层赋予属性", "Nickname",
              "Description",
              Title.CW2D(), Title.Others())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("图层名", "", "", GH_ParamAccess.list);
            pManager.AddTextParameter("属性", "A", "要绑定的属性值", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("", "", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var names = new List<string>();
            string strings = null;
            if (!DA.GetDataList(0, names)) return;
            if (!DA.GetData(1, ref strings)) return;

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
                            attributes.Add(key, value);
                        key = value = null;
                        flag = true;
                        continue;
                    default:
                        if (flag) key += strings[i];
                        else value += strings[i];
                        continue;
                }
            }

            var layers = RhinoDoc.ActiveDoc.Layers;
            var objects = RhinoDoc.ActiveDoc.Objects;
            var targets = new List<Rhino.DocObjects.RhinoObject>();
            var results = new List<GH_AttributeData>();
            foreach (var name in names)
            {
                var layer = layers.FindName(name);
                targets.AddRange(objects.FindByLayer(layer));
            }
            foreach (var obj in targets)
            {
                IGH_GeometricGoo goo = GH_Convert.ToGeometricGoo(obj.DuplicateGeometry());
                var attrData = new AttributeData(goo, attributes);
                results.Add(new GH_AttributeData(attrData));
            }

            DA.SetDataList(0, results);
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
                    var resourceName = "CW2D.Resources.GetGeoFromLayer.png";

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
            get { return new Guid("ABE45157-DED6-4FA3-8CB6-C9E883943662"); }
        }
    }
}