using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CW2D.Others
{
    public class MarkCenter : GH_Component
    {
        public MarkCenter()
          : base("MarkCenter", "MarkCenter",
              "标记节点图例的中心点",
              Title.CW2D(), Title.Others())
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("G", "G", "G", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("P", "P", "P", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var goos = new List<IGH_GeometricGoo>();
            if (!DA.GetDataList(0, goos)) return;

            var bbox = BoundingBox.Empty;
            foreach(var goo in goos)
                bbox = BoundingBox.Union(bbox, goo.Boundingbox);
            
            DA.SetData(0, bbox.Center);
        }

        public override void BakeGeometry(RhinoDoc doc, ObjectAttributes att, List<Guid> obj_ids)
        {
            var attr = new ObjectAttributes();
            var layer = new Layer() { Name = "CenterPoint" };
            int index = doc.Layers.Add(layer);
            if (index != -1) attr.LayerIndex = index;

            foreach (IGH_Param item in Params.Output)
            {
                if ((!(item is IGH_PreviewObject) || !((IGH_PreviewObject)item).Hidden) && item is IGH_BakeAwareObject)
                {
                    ((IGH_BakeAwareObject)item).BakeGeometry(doc, attr, obj_ids);
                }
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
                    var resourceName = "CW2D.Resources.makecenter.png";

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

        public override Guid ComponentGuid => new Guid("5F11354D-3735-4DD3-8A8A-BB5133B8E144");
    }
}