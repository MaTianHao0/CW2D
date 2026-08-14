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
    public class MarkPointsDistance : GH_Component
    {
        public MarkPointsDistance() : base("标注多点间距", "标注多点间距", "标注一组点在指定方向上的间距", Title.CW2D(), Title.Others())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("标注点", "标注点", "一组被标注的点", GH_ParamAccess.list);
            pManager.AddPointParameter("标注基准点", "标注基准点", "标注线所过一点", GH_ParamAccess.item);
            pManager.AddNumberParameter("标注字高", "标注字高", "标注字高，默认为3.5", GH_ParamAccess.item, 3.5);
            pManager.AddNumberParameter("旋转角度", "旋转角度", "标注自水平方向的旋转角度，默认为0", GH_ParamAccess.item, 0);
            pManager.AddPlaneParameter("标注平面", "标注平面", "标注所在平面，默认为XY平面", GH_ParamAccess.item, Plane.WorldXY);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("标注", "标注", "标注", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var points = new List<Point3d>();
            var dimPoint = new Point3d();
            var textHeight = new double();
            var radian = new double();
            var plane = new Plane();
            if (!DA.GetDataList(0, points)) return;
            if (!DA.GetData(1, ref dimPoint)) return;
            if (!DA.GetData(2, ref textHeight)) return;
            if (!DA.GetData(3, ref radian)) return;
            if (!DA.GetData(4, ref plane)) return;

            if (points.Count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "被标注点数量不能少于2");
                return;
            }

            var dimensions = new List<LinearDimension>();
            Vector3d direction = plane.XAxis;
            var style = Style.SetDimensionStyle(textHeight);
            radian += Math.PI / 2.0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point3d start = points[i];
                Point3d end = points[i + 1];
                var dim = LinearDimension.Create(
                    AnnotationType.Aligned,
                    style, plane, direction,
                    start, end, dimPoint, radian
                );
                if (dim != null)
                {
                    dim.DimensionStyleId = style.Id;
                    dimensions.Add(dim);
                }
            }

            DA.SetDataList(0, dimensions);
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
                    var resourceName = "CW2D.Resources.MarkAxisDistance .png";

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
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public override Guid ComponentGuid => new Guid("07E83718-2AEC-4E81-8BDB-D9D32C6C094F");
    }
}