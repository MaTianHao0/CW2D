using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
//using Ed.Eto;
using System.Reflection;


namespace CW2D.Others
{
    public class AddHatchComponent : GH_Component
    {
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
        }
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public AddHatchComponent()
          : base("填充图案", "填充",
              "为闭合曲线或区域添加填充图案",
              Title.CW2D(), "功能电池")
        {
        }
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("边界曲线", "B", "用于填充的闭合边界曲线", GH_ParamAccess.list);
            pManager.AddIntegerParameter("填充图案索引", "P", "填充图案的索引号 (0: 实心, 1: 网格, 2: 点, etc.)。可以在Rhino命令行输入 'Hatch' 查看索引。", GH_ParamAccess.item, 1); // 默认选择索引为1的图案
            pManager.AddNumberParameter("图案比例", "S", "填充图案的比例因子", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("图案旋转", "R", "填充图案的旋转角度 (度)", GH_ParamAccess.item, 0.0);
            pManager.AddBooleanParameter("是否烘焙", "B", "是否烘焙填充图案", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("填充", "H", "生成的填充几何体", GH_ParamAccess.list);
        }

        private static bool hatchPatternsLoaded = false;
        private static void EnsureHatchPatternsAreLoaded()
        {
            //Check to make sure the system defaults are in the hatch table
            //If they are not, let's add them back

            if (hatchPatternsLoaded) return; // 如果已经加载过，直接返回


            //Solid
            var hatch_id = RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Solid.Name);
            if (hatch_id == null)
                RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Solid);

            //Hatch1
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Hatch1.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Hatch1);

            //Hatch2
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Hatch2.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Hatch2);

            //Hatch3
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Hatch3.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Hatch3);

            //Dash
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Dash.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Dash);

            //Grid
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Grid.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Grid);

            //Grid60
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Grid60.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Grid60);

            //Plus
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Plus.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Plus);


            //Squares
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Squares.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Squares);
            hatchPatternsLoaded = true; // 标记为已加载

        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            EnsureHatchPatternsAreLoaded();
            List<Curve> boundaryCurves = new List<Curve>();
            int patternIndex = 0;
            double scale = 1.0;
            double rotationDegrees = 0.0;
            bool bakeHatch = false;

            if (!DA.GetDataList(0, boundaryCurves)) return;
            if (!DA.GetData(1, ref patternIndex)) return;
            if (!DA.GetData(2, ref scale)) return;
            if (!DA.GetData(3, ref rotationDegrees)) return;
            if (!DA.GetData(4, ref bakeHatch)) return;

            // 检查输入曲线是否有效且闭合
            List<Curve> validBoundaries = new List<Curve>();
            foreach (Curve curve in boundaryCurves)
            {
                if (curve != null && curve.IsClosed)
                {
                    validBoundaries.Add(curve);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "输入曲线无效或未闭合，已忽略。");
                }
            }

            if (validBoundaries.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "没有找到有效的闭合边界曲线用于填充。");

                System.Windows.Forms.MessageBox.Show("没有找到有效的闭合边界曲线用于填充。", "错误", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // 将角度转换为弧度
            double rotationRadians = Rhino.RhinoMath.ToRadians(rotationDegrees);

            List<Hatch> hatches = new List<Hatch>();

            // 获取Rhino文档中的填充图案数量，以防止索引越界
            int totalHatchPatterns = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Count;
            if (patternIndex < 0 || patternIndex >= totalHatchPatterns)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"填充图案索引 {patternIndex} 超出范围。可用索引范围为 0 到 {totalHatchPatterns - 1}。将使用默认图案。");
                patternIndex = 0; // 默认使用第一个图案
            }

            var tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            foreach (Curve boundary in validBoundaries)
            {
                // 创建 Hatch 对象
                // 注意：Hatch.Create 期望一个 IEnumerable<Curve>，即使只有一个边界
                IEnumerable<Curve> singleBoundary = new List<Curve> { boundary };
                Hatch[] newHatches = Hatch.Create(singleBoundary, patternIndex, rotationRadians, scale, tol);

                if (newHatches != null && newHatches.Length > 0)
                {
                    hatches.AddRange(newHatches);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, text: $"未能为曲线生成填充:");// {boundary.LocalId}
                }
            }

            if (hatches.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "未能生成任何填充几何体。请检查输入和填充设置。");
                return;
            }

            if (bakeHatch)
            {
                foreach (Hatch hatch in hatches)
                {
                    if (hatch != null)
                    {
                        Rhino.RhinoDoc.ActiveDoc.Objects.AddHatch(hatch);
                    }
                }
                Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
            }

            // 输出 Hatch 几何体
            DA.SetDataList(0, hatches);
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        /// 


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
                    var resourceName = "CW2D.Resources.LvLongGu.png";

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
            get { return new Guid("46E8D32B-B3AD-4162-923E-F1A44F7AB7EB"); }
        }
    }
}