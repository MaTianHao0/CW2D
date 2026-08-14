using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.Geometry;
using Rhino.Render.ChangeQueue;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.MakePlane
{
    public class PlaneDoor : GH_Component
    {
        public PlaneDoor()
            : base("平面图门", "平面图门",
                "在平面图中绘制门",
                Title.CW2D(), Title.Plane())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("门轴", "P", "门轴所在点", GH_ParamAccess.list);
            pManager.AddVectorParameter("起始朝向", "V", "门关闭时的方向", GH_ParamAccess.list);
            pManager.AddNumberParameter("宽度", "W", "门的宽度", GH_ParamAccess.list);
            pManager.AddNumberParameter("厚度", "T", "门的厚度", GH_ParamAccess.list);
            pManager.AddBooleanParameter("开启方向", "B", "门的开启方向， 0代表逆时针，1代表顺时针", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("门", "D", "平面图中的门", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var points = new List<Point3d>();
            var vectors = new List<Vector3d>();
            var width = new List<double>();
            var thickness = new List<double>();
            var flag = new List<bool>();
            if (!DA.GetDataList(0, points)) return;
            if (!DA.GetDataList(1, vectors)) return;
            if (!DA.GetDataList(2, width)) return;
            if (!DA.GetDataList(3, thickness)) return;
            if (!DA.GetDataList(4, flag)) return;

            if (vectors.Count != points.Count || width.Count != points.Count || thickness.Count != points.Count || flag.Count != points.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "输入数据数量不匹配");
                return;
            }

            var doors = new List<Curve>();
            for (int i = 0; i < points.Count; i++)
            {
                vectors[i].Unitize();
                var vec0 = vectors[i] * (thickness[i] / 2);
                var vec1 = vectors[i] * width[i];
                var radian = Math.PI / 2 * (flag[i] ? 1 : -1);
                vec1.Rotate(radian, Vector3d.ZAxis);

                var pt0 = points[i] - vec0;
                var pt1 = points[i] + vec0;
                var pt2 = pt1 + vec1;
                var pt3 = pt0 + vec1;
                var poly = new Polyline(new[] { pt0, pt1, pt2, pt3, pt0 });
                doors.Add(poly.ToNurbsCurve());

                var plane = new Plane(points[i], vec0, vec1);
                var arc = new Arc(plane, width[i], Math.PI / 2);
                doors.Add(arc.ToNurbsCurve());
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
                    var resourceName = "CW2D.Resources.p-door.png";

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
        public override Guid ComponentGuid => new Guid("38F619B4-A701-4BA9-B54C-C38059ADB51B");
    }
}