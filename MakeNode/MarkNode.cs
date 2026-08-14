using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Microsoft.Office.Interop.Excel;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using static Rhino.DocObjects.DimensionStyle;
using RgLine = Rhino.Geometry.Line;

namespace CW2D.MakeNode
{
    public class MarkNode : GH_Component
    {
        public MarkNode() : base("节点图标注", "节点图标注", "节点图标注", Title.CW2D(), Title.Node())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("点", "点", "点", GH_ParamAccess.item);
            pManager.AddNumberParameter("引线长度", "引线长度", "引线向外的长度,默认值为100", GH_ParamAccess.item, 100);
            pManager.AddNumberParameter("标注距离", "标注距离", "同一物料名称的点小于此距离则视为一组，只标一次；<=0 时使用引线长度", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("标注字高", "标注字高", "标注字高，默认为3.5", GH_ParamAccess.item, 3.5);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("标注", "标注", "标注", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 获取输入
            object inputPoints = null;
            double leaderlen = 100.0;
            double clusterDist = 0.0;   // 独立的聚类距离输入
            double textHeight = 3.5;

            if (!DA.GetData(0, ref inputPoints)) return;
            DA.GetData(1, ref leaderlen);
            DA.GetData(2, ref clusterDist);
            DA.GetData(3, ref textHeight);

            if (clusterDist <= 0.0)
                clusterDist = leaderlen;

            var targets = inputPoints as Dictionary<string, HashSet<Point3d>>;

            if (targets == null && inputPoints is Grasshopper.Kernel.Types.GH_ObjectWrapper wrapper)
            {
                targets = wrapper.Value as Dictionary<string, HashSet<Point3d>>;
            }
            if (targets == null || targets.Count <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "未能获取有效的点属性");
                return;
            }

            // 获取所有点包围盒
            BoundingBox bbox = BoundingBox.Empty;
            foreach (var points in targets.Values)
            {
                foreach (var pt in points)
                    bbox.Union(pt);
            }
            if (!bbox.IsValid) return;
            Point3d center = bbox.Center;

            var results = new List<AnnotationBase>();
            //Guid StyleId = SetupCustomStyle(textHeight * 0.8, textHeight);

            // 输出引线
            // 对同一物料名称的点进行“聚类”：近的只标一次，远的再标一处
            var labelCentersPerName = new Dictionary<string, List<Point3d>>();

            foreach (var kvp in targets)
            {
                string name = kvp.Key;
                var ptList = kvp.Value;

                if (!labelCentersPerName.TryGetValue(name, out var centers))
                {
                    centers = new List<Point3d>();
                    labelCentersPerName[name] = centers;
                }

                foreach (var pt in ptList)
                {
                    // 检查这个点是否离已有的标注中心太近，如果太近就不再新建标注
                    bool tooClose = false;
                    foreach (var c in centers)
                    {
                        if (pt.DistanceTo(c) < clusterDist)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose)
                        continue;

                    // 这个点所在区域还没有标过，以它为中心生成一条新标注
                    centers.Add(pt);

                    Vector3d dir = SetDirection(bbox, pt);

                    // 终点（仅由引线长度控制）
                    Point3d endpt = pt + (leaderlen * dir);
                    var pts = new List<Point3d> { pt, endpt };

                    int isParallel = dir.IsParallelTo(Vector3d.YAxis);
                    if (Math.Abs(Math.Abs(isParallel) - 1) < 1e-6)
                    {
                        // 与Y轴平行时，增加水平尾巴
                        Vector3d landingDir = Vector3d.XAxis; // 水平向右尾巴
                        Point3d landpt = endpt + (leaderlen * 0.1 * landingDir);
                        pts.Add(landpt);
                    }
                    var dimStyle = Style.SetDimensionStyle(textHeight);
                    var leader = Leader.Create(name, Plane.WorldXY, dimStyle, pts.ToArray());
                    leader.LeaderTextVerticalAlignment = TextVerticalAlignment.Bottom;
                    results.Add(leader);
                }
            }

            DA.SetDataList(0, results);
        }

        // SetDirection：设置引线是垂直还是水平
        // 判断方式：是靠近包围盒的哪一边，距离相同，默认设置垂直标注
        private Vector3d SetDirection(BoundingBox bbox, Point3d pt)
        {
            if (!bbox.IsValid) return Vector3d.XAxis;

            double CenterX = bbox.Center.X;
            double CenterY = bbox.Center.Y;

            double dx = pt.X - CenterX;
            double dy = pt.Y - CenterY;

            double halfWidth = (bbox.Max.X - bbox.Min.X) * 0.5;
            double halfHeight = (bbox.Max.Y - bbox.Min.Y) * 0.5;

            double disToLeftRight = halfWidth - Math.Abs(dx);
            double disToTopBottom = halfHeight - Math.Abs(dy);

            if (disToLeftRight < disToTopBottom)
                return dx > 0 ? Vector3d.XAxis : -Vector3d.XAxis;
            else
                return dy > 0 ? Vector3d.YAxis : -Vector3d.YAxis;
        }

        // CreateSafeDimension：绘制引导线，如果会和其他引导线重叠，则自动避让（尚未实现）
        private void CreateSafeDimension(BoundingBox bbox, Point3d p)
        {
            // 预留：可以在这里实现引线自动避让逻辑
        }

        // 设置或更新自定义标注样式
        //private Guid SetupCustomStyle(double arrowSize, double textSize)
        //{
        //    var doc = Rhino.RhinoDoc.ActiveDoc;
        //    string styleName = "CW2D_Node_Style"; //命名

        //    // 查找是否已存在该样式
        //    DimensionStyle dimStyle = doc.DimStyles.FindName(styleName);

        //    // 不存在，就以当前样式为模板复制一份
        //    if (dimStyle == null)
        //    {
        //        dimStyle = doc.DimStyles.Current.Duplicate();
        //        // 新增到文档中
        //        int index = doc.DimStyles.Add(dimStyle, false);
        //        // 重新获取这个已添加的样式对象
        //        dimStyle = doc.DimStyles[index];
        //    }

        //    // 文字在横线上方
        //    dimStyle.LeaderTextVerticalAlignment = TextVerticalAlignment.Bottom;

        //    // 设置箭头大小
        //    dimStyle.ArrowLength = arrowSize;
        //    dimStyle.LeaderArrowType = DimensionStyle.ArrowType.SolidTriangle; // 设为实心三角形

        //    dimStyle.TextHeight = textSize; // 字高
        //    dimStyle.LeaderContentAngleType = LeaderContentAngleStyle.Horizontal; // 文字水平
        //    dimStyle.TextHorizontalAlignment = TextHorizontalAlignment.Right; // 右对齐

        //    doc.DimStyles.Modify(dimStyle, dimStyle.Id, true);

        //    return dimStyle.Id;
        //}

        protected override Bitmap Icon
        {
            get
            {
                try
                {
                    // 获取当前鼠标悬停状态
                    bool isHovering = false;
                    if (Attributes is GH_ComponentAttributes attributes)
                    {
                        var field = typeof(GH_ComponentAttributes).GetField("m_mouseOver",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        isHovering = (bool)(field?.GetValue(attributes) ?? false);
                    }

                    // 加载原始图标
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "CW2D.Resources.Node Annotation.png";

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var originalIcon = new Bitmap(stream);

                            if (isHovering)
                            {
                                return ResizeIcon(originalIcon, 48, 48); // 悬停时 48x48
                            }
                            else if (originalIcon.Width != 24 || originalIcon.Height != 24)
                            {
                                return ResizeIcon(originalIcon, 24, 24); // 非标准尺寸调整为 24x24
                            }
                            return originalIcon; // 标准尺寸直接返回
                        }
                        else
                        {
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

                float scale = Math.Min(
                    (float)width / source.Width,
                    (float)height / source.Height);
                int scaledWidth = (int)(source.Width * scale);
                int scaledHeight = (int)(source.Height * scale);
                int x = (width - scaledWidth) / 2;
                int y = (height - scaledHeight) / 2;

                g.Clear(Color.Transparent);
                g.DrawImage(source, x, y, scaledWidth, scaledHeight);
            }
            return dest;
        }

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
        }

        public override Guid ComponentGuid => new Guid("{7B9F8376-5B31-442B-90B9-3C18EEC1169E}");
    }
}
