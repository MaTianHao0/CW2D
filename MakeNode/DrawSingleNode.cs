using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Reflection;

namespace CW2D.MakeNode
{
    public class DrawSingleNode : GH_Component
    {
        public DrawSingleNode()
          : base("读取节点", "读取节点",
              "",
              Title.CW2D(), Title.Node())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("定位点", "定位点", "节点定位点", GH_ParamAccess.list);
            pManager.AddNumberParameter("缩放比例", "缩放比例", "缩放比例", GH_ParamAccess.list, 1.0);
            pManager.AddNumberParameter("旋转角度", "旋转角度", "旋转角度（弧度制）", GH_ParamAccess.list, 0.0);
            pManager.AddTextParameter("节点名称", "节点名称", "节点名称", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("", "", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var targets = new List<Point3d>();
            var scales = new List<double>();
            var rotations = new List<double>();
            string name = string.Empty;
            if (!DA.GetDataList(0, targets)) return;
            if (!DA.GetDataList(1, scales)) return;
            if (!DA.GetDataList(2, rotations)) return;
            if (!DA.GetData(3, ref name)) return;

            IOFile.LoadPathTable();
            if (!IOFile.PathTable.ContainsKey(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "节点名称不存在，请检查输入");
                return;
            }

            var filePath = IOFile.PathTable[name];
            var iofile = new IOFile(filePath);
            iofile.ReadFile();

            var results = new List<Curve>();
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var scale = i < scales.Count ? scales[i] : 1.0;
                var rotation = i < rotations.Count ? rotations[i] : 0.0;
                var center = iofile.Center;
                var vector = targets[i] - center;

                foreach (var geometry in iofile.Geometries)
                {
                    if (geometry == null) continue;
                    if (geometry is Curve curve)
                    {
                        curve.Scale(scale);
                        curve.Rotate(rotation, Vector3d.ZAxis, center);
                        curve.Translate(vector);
                        results.Add(curve);
                    }
                }
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
                    var resourceName = "CW2D.Resources.read node.png";

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
            get { return new Guid("FE2A8D84-F4D2-421D-99B6-45BCBC5E436E"); }
        }
    }
}