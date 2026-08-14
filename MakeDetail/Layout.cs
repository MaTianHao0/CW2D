using Eto.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data; // 必须引用 Data
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace CW2D.Layout
{
    public class LayoutMultiDrawings : GH_Component
    {
        public LayoutMultiDrawings()
            : base("大样图排版", "大样图排版", "将多个剖面图横向排列并对齐到主视图下方", Title.CW2D(), Title.Detail())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("主视图", "主视图", "立面图 (作为基准)", GH_ParamAccess.list);
            //改为 Tree Access，以便一次性接收所有剖面图并进行整体排版
            pManager.AddGenericParameter("下部视图", "下部视图", "多个剖面图 (请确保每个图在不同的分支/Group中)", GH_ParamAccess.tree);
            pManager.AddGenericParameter("右部视图","右部视图","多个纵剖图",GH_ParamAccess.tree);

            pManager.AddNumberParameter("垂直间距", "垂直间距", "上下间距", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("水平间距", "水平间距", "剖面图之间的水平间距", GH_ParamAccess.item, 0.1);
            pManager.AddBooleanParameter("旋转", "旋转", "是否旋转下部视图 -90 度", GH_ParamAccess.item, true);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("排版结果", "排版结果", "排列好的结果", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 获取主视图
            var mainGoos = new List<IGH_GeometricGoo>();
            if (!DA.GetDataList(0, mainGoos)) return;

            //获取下部视图 (Tree)
            if (!DA.GetDataTree(1, out GH_Structure < IGH_Goo > subTree) ) subTree = new GH_Structure<IGH_Goo>();

            //获取右部视图

            if(!DA.GetDataTree(2, out GH_Structure<IGH_Goo> rightTree)) rightTree = new GH_Structure<IGH_Goo>();
            //设置默认垂直间距和水平间距,其值等于间距与主视图长/宽的比例
            double vGap=0.1;
            double hGap=0.1;
            bool doRotate = true;

            DA.GetData(3, ref vGap);
            DA.GetData(4, ref hGap);
            DA.GetData(5, ref doRotate);

            // 结果列表
            var finalResult = new List<IGH_GeometricGoo>();
            finalResult.AddRange(mainGoos);

            // 计算主视图包围盒
            BoundingBox bboxMain = GetTotalBoundingBox(mainGoos);
            if (!bboxMain.IsValid) return;
            double width=bboxMain.Max.X - bboxMain.Min.X;
            double height = bboxMain.Max.Y - bboxMain.Min.Y;
            vGap = vGap * height;
            hGap=hGap * width;

            //排列下方视图
            if (subTree != null && subTree.PathCount > 0)
            {
                //预处理
                //处理下方视图
                Transform rotation = doRotate ? Transform.Rotation(-Math.PI / 2.0, Vector3d.ZAxis, Point3d.Origin) : Transform.Identity;
                var processedSubs = GetProcessedViews(ref subTree, rotation);

                //纵向堆叠排版

                // 初始 Y 坐标：设定在主视图的底部下方 vGap 处
                double currentTopY = bboxMain.Min.Y - vGap;

                // 主视图的中心 X 坐标 (用于对齐)
                double mainCenterX = bboxMain.Center.X;

                foreach (var sub in processedSubs)
                {
                    //计算 X 轴移动量,让当前剖面图居中对齐到主视图
                    double subWidth = sub.BBox.Max.X - sub.BBox.Min.X;
                    double targetMinX = mainCenterX - (subWidth / 2.0); // 目标左边缘 = 中心 - 半宽
                    double deltaX = targetMinX - sub.BBox.Min.X;

                    //计算 Y 轴移动量,让当前剖面图顶部对齐到 currentTopY
                    double deltaY = currentTopY - sub.BBox.Max.Y;

                    Transform move = Transform.Translation(deltaX, deltaY, 0);

                    //应用移动
                    foreach (var geo in sub.Geometry)
                    {
                        geo.Transform(move);
                        finalResult.Add(geo);
                    }

                    // D. 更新 Y 坐标，为下一个图做准备
                    // 下一张图的起始位置 = 当前图的底部 - 间距
                    double subHeight = sub.BBox.Max.Y - sub.BBox.Min.Y;
                    currentTopY -= (subHeight + vGap);
                }
            }


            //排列右方视图
            if (rightTree != null && rightTree.PathCount > 0)
            {
                //处理右方视图
                var processedRights = GetProcessedViews(ref rightTree, Transform.Identity);

                // 计算右侧堆叠的总高度 (用于垂直对齐)
                // 策略：让右侧第一张图的顶部，与主视图顶部对齐

                double currentY = bboxMain.Max.Y; // 从主视图顶部开始
                double startX = bboxMain.Max.X + hGap; // 主视图右侧

                foreach (var right in processedRights)
                {

                    double rightWidth = right.BBox.Max.X - right.BBox.Min.X;

                    // X轴: 对齐到 startX
                    double targetMinX = startX;
                    double deltaX = targetMinX - right.BBox.Min.X;

                    // B. 计算 Y 轴移动量 (让当前剖面图顶部对齐到 currentY)
                    double deltaY = currentY - right.BBox.Max.Y;


                    Transform move = Transform.Translation(deltaX, deltaY, 0);

                    foreach (var geo in right.Geometry)
                    {
                        geo.Transform(move);
                        finalResult.Add(geo);
                    }

                    // 更新 X (向右堆叠);
                    startX += (rightWidth + hGap); // 这里的 gap 也可以是单独的 item 间距
                }
            }
                DA.SetDataList(0, finalResult);
        }
        // 辅助类：存储处理过的视图信息
        class ProcessedView
        {
            public List<IGH_GeometricGoo> Geometry = new List<IGH_GeometricGoo>();
            public BoundingBox BBox;
            public double Width => BBox.Max.X - BBox.Min.X;
        }

        private void AddToSolidBox(IGH_Goo goo, ref BoundingBox allBox, ref bool hasSolid)
        {
            if (goo == null) return;

            GeometryBase geom = GH_Convert.ToGeometryBase(goo);
            // 如果转换失败 (比如是数字、字符串或空)，直接跳过
            if (geom == null) return;
            // 获取包围盒
            BoundingBox gBox = geom.GetBoundingBox(true);
            if (!gBox.IsValid) return;
            // 更新全局盒子 (包含标注)
            allBox.Union(gBox);
        }

        //获取包围盒
        private BoundingBox GetTotalBoundingBox(List<IGH_GeometricGoo> goos)
        {
            BoundingBox allBox = BoundingBox.Empty;
            bool hasSolid = false;

            foreach (var goo in goos)
            {
                AddToSolidBox(goo,  ref allBox, ref hasSolid);
            }

            return allBox;
        }

        private List<ProcessedView> GetProcessedViews(ref GH_Structure<IGH_Goo> tree, Transform rotation)
        {
            var processedViews = new List<ProcessedView>();

            foreach (var branch in tree.Branches)
            {
                if (branch == null || branch.Count == 0) continue;

                var View = new ProcessedView();

                // 遍历分支内的所有物体
                // 在 GetProcessedViews 的循环内部：
                foreach (IGH_Goo item in branch)
                {
                    if (item == null) continue;

                    IGH_GeometricGoo geoGoo = null;

                    // 优先检查是否本身就是几何体
                    if (item is IGH_GeometricGoo g)
                    {
                        geoGoo = g;
                    }
                    //尝试转换 ,针对原生 Rhino 对象
                    else
                    {
                        object raw = item.ScriptVariable();
                        geoGoo = GH_Convert.ToGeometricGoo(raw);
                    }

                    if (geoGoo != null)
                    {
                        // 复制并旋转
                        var duplicated = geoGoo.DuplicateGeometry();
                        if (duplicated != null)
                        {
                            duplicated.Transform(rotation);
                            View.Geometry.Add(duplicated);
                        }
                    }
                }

                // 只有当分支收集完毕后，才添加到结果列表
                if (View.Geometry.Count > 0)
                {
                    View.BBox = GetTotalBoundingBox(View.Geometry);
                    if (View.BBox.IsValid)
                    {
                        processedViews.Add(View);
                    }
                }
            }
            return processedViews;
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
                    var resourceName = "CW2D.Resources.layout.png";

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
        public override Guid ComponentGuid => new Guid("CAEC480C-52F7-48CF-A05A-9077373BA33F");
    }
}