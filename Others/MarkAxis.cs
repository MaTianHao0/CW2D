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
    public class MarkAxis : GH_Component
    {
        public MarkAxis() : base("轴线标注", "轴线标注", "标注轴线之间的距离", Title.CW2D(), Title.Plane())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("轴线", "轴线", "一组平行轴线", GH_ParamAccess.list);
            pManager.AddPointParameter("标注基准点", "标注基准点", "标注线所过一点", GH_ParamAccess.item);
            pManager.AddNumberParameter("标注字高", "标注字高", "标注字高，默认为3.5", GH_ParamAccess.item, 3.5);
            pManager.AddPlaneParameter("标注平面", "标注平面", "标注所在平面，默认为XY平面", GH_ParamAccess.item, Plane.WorldXY);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("标注", "标注", "轴线距离标注", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var axis = new List<Curve>();
            var point = new Point3d();
            var textHeight = new double();
            var plane = new Plane();
            if (!DA.GetDataList(0, axis)) return;
            if (!DA.GetData(1, ref point)) return;
            if (!DA.GetData(2, ref textHeight)) return;
            if (!DA.GetData(3, ref plane)) return;

            if (axis.Count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "轴线数量不能少于2");
                return;
            }

            var dimensions = new List<LinearDimension>();
            Vector3d direction = plane.XAxis;
            Vector3d crvDir = axis[0].PointAtEnd - axis[0].PointAtStart;
            int flag = crvDir.IsParallelTo(direction);
            double radian = flag == 0 ? 0.0 : Math.PI / 2.0;
            var style = Style.SetDimensionStyle(textHeight);
            for (int i = 0; i < axis.Count - 1; i++)
            {
                Curve curve1 = axis[i], curve2 = axis[i + 1];
                curve1.ClosestPoint(point, out double t1);
                curve2.ClosestPoint(point, out double t2);
                Point3d start = curve1.PointAt(t1);
                Point3d end = curve2.PointAt(t2);
                var dim = LinearDimension.Create(
                    AnnotationType.Rotated,
                    style, plane, direction,
                    start, end, point, radian
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
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
        }

        public override Guid ComponentGuid => new Guid("86397C44-F503-4020-BB55-D0B0317297CC");
    }
}