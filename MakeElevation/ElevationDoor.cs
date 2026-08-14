using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.MakeElevation
{
    public class ElevationDoor : GH_Component
    {
        public ElevationDoor()
            : base("立面图门", "立面图门",
                "在立面图中绘制门",
                Title.CW2D(), Title.Elevation())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("中心点", "P", "", GH_ParamAccess.list);
            pManager.AddNumberParameter("高度", "H", "门的高度", GH_ParamAccess.list);
            pManager.AddNumberParameter("宽度", "W", "门的宽度", GH_ParamAccess.list);
            pManager.AddBooleanParameter("门轴位置", "L", "门轴位置，false代表左边，true代表右边", GH_ParamAccess.list);
            pManager.AddIntegerParameter("开启方向", "F", "", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("", "", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var center = new List<Point3d>();
            var heights = new List<double>();
            var widths = new List<double>();
            var flags = new List<bool>();
            var types = new List<int>();
            if (!DA.GetDataList(0, center)) return;
            if (!DA.GetDataList(1, heights)) return;
            if (!DA.GetDataList(2, widths)) return;
            if (!DA.GetDataList(3, flags)) return;
            if (!DA.GetDataList(4, types)) return;

            var doors = new List<Curve>();
            for (int i = 0; i < center.Count; i++)
            {
                double w = widths[i] / 2.0, h = heights[i] / 2.0;
                var pt0 = center[i] + new Vector3d(-w, h, 0);
                var pt1 = center[i] + new Vector3d(w, h, 0);
                var pt2 = center[i] + new Vector3d(w, -h, 0);
                var pt3 = center[i] + new Vector3d(-w, -h, 0);
                var poly0 = new Polyline(new List<Point3d> { pt0, pt1, pt2, pt3, pt0 });
                var poly1 = new Polyline();
                if (flags[i]) poly1 = new Polyline(new[] { pt0, (pt1 + pt2) / 2, pt3 });
                else poly1 = new Polyline(new[] { pt1, (pt0 + pt3) / 2, pt2 });

                doors.Add(poly0.ToNurbsCurve());
                doors.Add(poly1.ToNurbsCurve());
            }

            DA.SetDataList(0, doors);
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
                    var resourceName = "CW2D.Resources.e-door.png";

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
            get { return new Guid("9CD022AF-C2D9-4503-848D-C82CB8651995"); }
        }
    }
}