using Ed.Eto;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Collections;
using static System.Math;

namespace CW2D.MakePlane
{
    public class MarkPlane : GH_Component
    {
        public MarkPlane() : base("平面图标注", "平面图标注", "平面图标注", Title.CW2D(), Title.Plane())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("平面图", "平面图", "平面图", GH_ParamAccess.list);
            pManager.AddNumberParameter("偏移率", "偏移率", "标注线的偏移比例", GH_ParamAccess.item, 0.05);
            pManager.AddNumberParameter("标注字高", "标注字高", "标注字高，默认为3.5", GH_ParamAccess.item, 3.5);
            pManager[1].Optional = true;
            pManager[2].Optional = true;


        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("标注", "标注", "标注", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var curves = new List<Curve>();
            var textHeight = 3.0;
            var offsetRatio = 0.05;
            var offsets = new List<double>();
            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref offsetRatio);
            DA.GetData(2, ref textHeight);

            var dimensions = new List<AnnotationBase>();


            var list = new List<Curve>();
            foreach (var curve in curves)
            {
                var flag = curve.GetUserString("标注");
                if (flag == "TRUE")
                {
                    list.Add(curve);
                }
            }

            if (list.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "未设置标注曲线，请检查输入。");
                return;
            }

            //获取包围盒，用于定位标注
            var overallBox = BoundingBox.Empty;
            foreach (var curve in list)
            {
                overallBox.Union(curve.GetBoundingBox(true));
            }
            if (!overallBox.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "无法计算有效的几何体边界");
                return;
            }
            offsets = GetOffset(offsetRatio, ref list, overallBox);

            //最小线段长度，小于该长度的线段会被忽略
            const double minLineLength = 50.0;

            var horizontalSegments = new List<Line>();
            var verticalSegments = new List<Line>();

            foreach (var curve in list) // list是过滤后的曲线
            {
                // 优先处理多段线 (PolylineCurve)，如未炸开的矩形
                if (curve is PolylineCurve polylineCurve)
                {
                    // 先转换为Polyline对象，再获取所有线段
                    var segments = polylineCurve.ToPolyline().GetSegments();
                    foreach (var line in segments)
                    {
                        //检查线段长度
                        if (line.Length < minLineLength) continue;
                        // 对每一段进行正交判断
                        if (Math.Abs(line.Direction.Z) < 1e-9 && Math.Abs(line.Direction.Y) < 1e-9)
                        {
                            horizontalSegments.Add(line);
                        }
                        else if (Math.Abs(line.Direction.Z) < 1e-9 && Math.Abs(line.Direction.X) < 1e-9)
                        {
                            verticalSegments.Add(line);
                        }
                    }
                }
                // 再处理其他可能是直线的曲线
                else if (curve.IsLinear(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                {

                    Line line = new Line(curve.PointAtStart, curve.PointAtEnd);
                    //检查线段长度
                    if (line.Length < minLineLength) continue;

                    if (Math.Abs(line.Direction.Z) < 1e-9 && Math.Abs(line.Direction.Y) < 1e-9)
                    {
                        horizontalSegments.Add(line);
                    }
                    else if (Math.Abs(line.Direction.Z) < 1e-9 && Math.Abs(line.Direction.X) < 1e-9)
                    {
                        verticalSegments.Add(line);
                    }
                }
            }


            //从水平/竖直线中提取Y/X坐标
            var yLocations = horizontalSegments.Select(line => line.From.Y).ToList();
            var xLocations = verticalSegments.Select(line => line.From.X).ToList();

            double tolerance = 80.0;
            //获取外围坐标
            // 假设 Y 值最小的线段可能有多条，且它们都在同一个 Y 坐标上
            var minY = verticalSegments.Min(line => Math.Min(line.From.Y, line.To.Y)); // 找到最小的 Y 坐标
            var DownLines = verticalSegments.Where(line => Math.Abs(Math.Min(line.From.Y, line.To.Y) - minY) < tolerance) // 筛选出所有 To.Y 等于最小 Y 坐标的线段
                .Select(line => line.To.X) // 获取它们的 To.X 坐标
                .ToList(); // 转换为列表
            var maxY = verticalSegments.Max(line => Math.Max(line.From.Y, line.To.Y));
            var TopLines = verticalSegments.Where(line => Math.Abs(Math.Max(line.From.Y, line.To.Y) - maxY) < tolerance).Select(line => line.To.X).ToList();
            var minX = horizontalSegments.Min(line => Math.Min(line.From.X, line.To.X));
            var LeftLines = horizontalSegments.Where(line => Math.Abs(Math.Min(line.From.X, line.To.X) - minX) < tolerance).Select(line => line.To.Y).ToList();
            var maxX = horizontalSegments.Max(line => Math.Max(line.From.X, line.To.X));
            var RightLines = horizontalSegments.Where(line => Math.Abs(Math.Max(line.From.X, line.To.X) - maxX) < tolerance).Select(line => line.To.Y).ToList();

            // 在这里初始化包围盒列表 (每次电池计算都会重置为空)
            var globalOccupiedBoxes = new List<BoundingBox>();

            var style = Style.SetDimensionStyle(textHeight);
            CreateDimensionChain(RightLines, LeftLines, "Y", style, ref dimensions, offsets, overallBox, textHeight, ref globalOccupiedBoxes);
            CreateDimensionChain(TopLines, DownLines, "X", style, ref dimensions, offsets, overallBox,textHeight, ref globalOccupiedBoxes);

            DA.SetDataList(0, dimensions);
        }
        /// <summary>
        /// 将一个排序好的坐标列表按公差聚类
        /// </summary>
        /// <param name="sortedLocations">已排序的坐标列表</param>
        /// <param name="tolerance">聚类公差。小于此公差的间距将被视为一个簇</param>
        /// <returns>一个包含多个“簇”的列表，每个“簇”是一个坐标列表</returns>
        private List<List<double>>ClusterLocations(List<double> sortedLocations, double tolerance)
        {
            var clusters = new List<List<double>>();
            if (sortedLocations.Count == 0) return clusters;

            // 开始第一个簇
            List<double> currentCluster = new List<double> { sortedLocations[0] };
            clusters.Add(currentCluster);

            for (int i = 1; i < sortedLocations.Count; i++)
            {
                double previousItem = sortedLocations[i - 1];
                double currentItem = sortedLocations[i];

                // 检查当前点与 *上一个点* 的距离
                if (currentItem - previousItem <= tolerance)
                {
                    // 距离很近，属于同一个簇
                    currentCluster.Add(currentItem);
                }
                else
                {
                    // 间距太大，开始一个新簇
                    currentCluster = new List<double> { currentItem };
                    clusters.Add(currentCluster);
                }
            }
            return clusters;
        }


        ///创建一个基于“位置点”的连续标注链和总标注
        /// </summary>
        /// <param name="locations">要标注的所有坐标点 (例如，所有Y_loc或所有X_loc)</param>
        /// <param name="orientation">"X" (水平标注) 或 "Y" (竖直标注)</param>
        /// <param name="style">标注样式</param>
        /// <param name="dimensions">要添加到的标注列表</param>
        /// <param name="offsets">偏移距离 [0]=分段, [1]=总长</param>
        /// <param name="bbox">总包围盒 (用于定位)</param
        private void CreateDimensionChain(List<double> loc1, List<double> loc2, string orientation, DimensionStyle style, ref List<AnnotationBase> dimensions, List<double> offsets, BoundingBox bbox,double textHeight, ref List<BoundingBox> occupiedBoxes)
        {
            if (loc1.Count < 2 || loc2.Count < 2) return;
            //定义公差
            const double clusterTolerance = 100.0; // 例如，100mm内的线都算一个簇

            //排序，去重，合并
            var sortedLocs1 = loc1.Distinct().OrderBy(p => p).ToList();
            var sortedLocs2 = loc2.Distinct().OrderBy(p => p).ToList();

            //  生成簇
            var clusters1 = ClusterLocations(sortedLocs1, clusterTolerance);
            var clusters2 = ClusterLocations(sortedLocs2, clusterTolerance);



            //  获取每个簇的“中点”，作为代表点
            var mergedLocs1 = new List<double>();
            foreach (var cluster in clusters1)
            {
                if (clusters1.Count > 0)
                {
                    // 计算这个簇的中心点
                    double clusterMidpoint = (cluster.First() + cluster.Last()) / 2.0;
                    mergedLocs1.Add(clusterMidpoint);
                }
            }
            var mergedLocs2 = new List<double>();
            foreach (var cluster in clusters2)
            {
                if (clusters2.Count > 0)
                {
                    // 计算这个簇的中心点
                    double clusterMidpoint = (cluster.First() + cluster.Last()) / 2.0;
                    mergedLocs2.Add(clusterMidpoint);
                }
            }


            if (mergedLocs1.Count < 2 || mergedLocs2.Count < 2) return;

            //准备标注参数

            double detailOffset = offsets.Count > 0 ? offsets[0] : 100.00;
            double totalOffset = offsets.Count > 1 ? offsets[1] : 200;

            //定义标注位置

            double uLoc, uLocTotal, dLoc, dLocTotal;
            Vector3d axis;
            double rotation;

            // 定义避让移动的方向向量
            Vector3d uDir, dDir;

            if (orientation == "X")
            {
                uLoc = bbox.Max.Y + detailOffset;
                uLocTotal = bbox.Max.Y + totalOffset;
                dLoc = bbox.Min.Y - detailOffset;
                dLocTotal = bbox.Min.Y - totalOffset;
                axis = Vector3d.XAxis;
                rotation = 0.0;

                // 上方标注向上避让，下方标注向下避让
                uDir = Vector3d.YAxis;
                dDir = -Vector3d.YAxis;

            }
            else
            {
                uLoc = bbox.Max.X + detailOffset;
                uLocTotal = bbox.Max.X + totalOffset;
                dLoc = bbox.Min.X - detailOffset;
                dLocTotal = bbox.Min.X - totalOffset;
                axis = Vector3d.YAxis;
                rotation = PI / 2;

                // 右侧标注向右避让，左侧标注向左避让
                uDir = Vector3d.XAxis;
                dDir = -Vector3d.XAxis;

            }

            //创建分段标注(上/右）
            for (int i = 0; i < mergedLocs1.Count - 1; i++)
            {
                double p1 = mergedLocs1[i];
                double p2 = mergedLocs1[i + 1];

                Point3d start, end, dimPoint;
                if (orientation == "X")
                {
                    start = new Point3d(p1, bbox.Max.Y, 0);
                    end = new Point3d(p2, bbox.Max.Y, 0);
                    dimPoint = new Point3d((p1+p2)/2, uLoc, 0);

                }
                else // "Y"
                {
                    start = new Point3d(bbox.Max.X, p1, 0); // 附着在BBox右边
                    end = new Point3d(bbox.Max.X, p2, 0);
                    dimPoint = new Point3d(uLoc, (p1 + p2) / 2, 0);
                }
                CreateSafeDimension(start, end, dimPoint, axis, rotation, style, textHeight, uDir, ref dimensions, ref occupiedBoxes);
            }

            //分段标注 下/左
            for (int i = 0; i < mergedLocs2.Count - 1; i++)
            {
                double p1 = mergedLocs2[i];
                double p2 = mergedLocs2[i + 1];

                Point3d start, end, dimPoint;
                if (orientation == "X")
                {
                    start = new Point3d(p1, bbox.Min.Y, 0);
                    end = new Point3d(p2, bbox.Min.Y, 0);
                    dimPoint = new Point3d((p1 + p2) / 2, dLoc, 0);
                }
                else // "Y"
                {
                    start = new Point3d(bbox.Min.X, p1, 0);
                    end = new Point3d(bbox.Min.X, p2, 0);
                    dimPoint = new Point3d(dLoc, (p1 + p2) / 2, 0);
                }
                CreateSafeDimension(start, end, dimPoint, axis, rotation, style, textHeight, dDir, ref dimensions, ref occupiedBoxes);
            }

            //总标注
            double uMin = mergedLocs1.First();
            double uMax = mergedLocs1.Last();
            double dMin = mergedLocs2.First();
            double dMax = mergedLocs2.Last();

            Point3d uStartTotal, uEndTotal, uDimPointTotal;
            Point3d dStartTotal, dEndTotal, dDimPointTotal;

            if (orientation == "X")
            {
                uStartTotal = new Point3d(uMin, bbox.Max.Y, 0);
                uEndTotal = new Point3d(uMax, bbox.Max.Y, 0);
                uDimPointTotal = new Point3d(uMin, uLocTotal, 0);

                dStartTotal = new Point3d(dMin, bbox.Min.Y, 0);
                dEndTotal = new Point3d(dMax, bbox.Min.Y, 0);
                dDimPointTotal = new Point3d(dMin, dLocTotal, 0);
            }
            else // "Y"
            {
                uStartTotal = new Point3d(bbox.Max.X, uMin, 0);
                uEndTotal = new Point3d(bbox.Max.X, uMax, 0);
                uDimPointTotal = new Point3d(uLocTotal, uMin, 0);

                dStartTotal = new Point3d(bbox.Min.X, dMin, 0);
                dEndTotal = new Point3d(bbox.Min.X, dMax, 0);
                dDimPointTotal = new Point3d(dLocTotal, dMin, 0);
            }
            CreateSafeDimension(
                uStartTotal, uEndTotal, uDimPointTotal,
                axis, rotation, style, textHeight,
                uDir,
                ref dimensions, ref occupiedBoxes
            );
            CreateSafeDimension(
                dStartTotal, dEndTotal, dDimPointTotal,
                axis, rotation, style, textHeight,
                dDir, 
                ref dimensions, ref occupiedBoxes
            );

        }

        /// <summary>
        /// 按位置（loc）对线段进行聚类
        /// </summary>
        /// <param name="tolerance">聚类公差。loc值相差在此公差内的线段会被视为同一组</param>
        /// <returns>一个包含多个“簇”的列表，每个“簇”都是一个线段列表</returns>
        private List<List<Segment>> ClusterSegmentsByLocation(List<Segment> segments, double tolerance)
        {
            var clusters = new List<List<Segment>>();
            if (segments.Count == 0) return clusters;

            // 1. 必须先按 loc 排序
            var sortedSegments = segments.OrderBy(s => s.loc).ToList();

            List<Segment> currentCluster = new List<Segment> { sortedSegments[0] };
            clusters.Add(currentCluster);

            for (int i = 1; i < sortedSegments.Count; i++)
            {
                var segment = sortedSegments[i];

                // 检查当前线段是否与当前“簇”的起始线段足够近
                // 与簇的第一个元素比较，可以防止“漂移”
                double clusterStartLoc = currentCluster[0].loc;

                if (Math.Abs(segment.loc - clusterStartLoc) <= tolerance)
                {
                    // 足够近，添加到当前簇
                    currentCluster.Add(segment);
                }
                else
                {
                    // 太远了，开始一个新簇
                    currentCluster = new List<Segment> { segment };
                    clusters.Add(currentCluster);
                }
            }
            return clusters;
        }


        //计算自适应偏移距离
        private List<double> GetOffset(double offsetRatio, ref List<Curve> list, BoundingBox overallBbox)
        {
            List<double> offsets = new List<double>();

            //遍历所有过滤后的曲线，将它们的包围盒合并成一个总包围盒
            foreach (var curve in list)
            {
                overallBbox.Union(curve.GetBoundingBox(true));
            }

            // 安全检查：如果包围盒无效（例如没有输入任何几何体），则退出
            if (!overallBbox.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "无法计算有效的几何体边界,无法获得偏移值。");
                //返回一个固定偏移距离
                offsets.Add(5.0);
                offsets.Add(10.0);
                return offsets;
            }
            // 利用相对最短边计算特征尺寸
            double width = overallBbox.Diagonal.X;
            double height = overallBbox.Diagonal.Y;
            double characteristicSize = Math.Min(width, height);


            //根据比例计算出最终的绝对偏移距离
            double offset1 = characteristicSize * offsetRatio;
            double offset2 = characteristicSize * offsetRatio * 2.0;
            offsets.Add(offset1);
            offsets.Add(offset2);
            return offsets;


        }
        private void CreateSafeDimension(
        Point3d start,
        Point3d end,
        Point3d dimPoint,
        Vector3d axis,
        double rotation,
        DimensionStyle style,
        double textHeight,
        Vector3d moveDir,
        ref List<AnnotationBase> dimensions,
        ref List<BoundingBox> occupiedBoxes
    )
        {
            //初始化
            var plane = Plane.WorldXY;
            double dist = start.DistanceTo(end);
            string content = dist.ToString("F1");

            //获取比例
            double globalScale = style.DimensionScale;
            if (globalScale <= 0) globalScale = 1.0;

            // 计算视觉尺寸
            double visualTextHeight = textHeight * globalScale;
            // 文字宽度估算 (字符数 * 字高 * 0.8)
            double visualTextWidth = (content.Length * textHeight * 0.8) * globalScale;

            //判断是否需要引线 (80% 宽度规则)
            bool needLeader = visualTextWidth >= (dist * 0.8);

            if (!needLeader)
            {
                //普通标注
                var dim = LinearDimension.Create(AnnotationType.Aligned, style, plane, axis, start, end, dimPoint, rotation);
                if (dim != null)
                {
                    dim.PlainText = content;
                    dimensions.Add(dim);
                }
            }
            else
            {
                //引线

                //背景标注 (隐藏文字)
                var dim = LinearDimension.Create(AnnotationType.Aligned, style, plane, axis, start, end, dimPoint, rotation);
                if (dim != null)
                {
                    dim.TextFormula = " ";
                    dimensions.Add(dim);
                }

                //定义三个方向
                // PushDir (moveDir): 主推方向 (上/下/左/右)
                // StackDir (lateralDir): 45度斜向堆叠方向 (垂直于主方向)
                // LandingDir: 最终文字平台的延伸方向 (永远水平!)

                Vector3d stackDir;   // 用于制造45度斜角
                Vector3d landingDir; // 用于最后的水平尾巴

                bool isVerticalMove = Math.Abs(moveDir.Y) > 0.5; // 是 Top/Bottom 标注

                if (isVerticalMove) // 上下标注 (Move Y)
                {
                    stackDir = Vector3d.XAxis;   // 向右歪 (形成 / 形状)
                    landingDir = Vector3d.XAxis; // 水平向右延伸
                }
                else // 左右标注 (Move X)
                {
                    stackDir = Vector3d.YAxis;   // 向上歪 (形成 / 形状)
                                                 // 关键修正：右侧标注向右水平延伸，左侧向左
                    landingDir = (moveDir.X > 0) ? Vector3d.XAxis : -Vector3d.XAxis;
                }

                Point3d anchorPoint = dimPoint;
                double pushDist = visualTextHeight * 1.5;
                double maxPushDist = visualTextHeight * 20.0;

                BoundingBox tryBox;
                int safety = 0;

                do
                {
                    //向右(Push) + 向上(Stack) -> 右上45度
                    Point3d testCenter = anchorPoint + (moveDir * pushDist) + (stackDir * pushDist);

                    // 碰撞检测
                    tryBox = CreateBox(testCenter, visualTextWidth, visualTextHeight, visualTextHeight * 0.2);

                    if (!CheckOverlap(tryBox, occupiedBoxes))
                    {
                        break;
                    }

                    pushDist += visualTextHeight*0.1;
                    safety++;

                } while (safety < 30 && pushDist < maxPushDist);

                occupiedBoxes.Add(tryBox);

                //构造 Leader
                var leader = new Leader();
                leader.Plane = Plane.WorldXY;
                leader.PlainText = content;
                leader.DimensionScale = globalScale;

                if (style.Id == Guid.Empty) leader.DimensionStyleId = Rhino.RhinoDoc.ActiveDoc.DimStyles.Current.Id;
                else leader.DimensionStyleId = style.Id;

                leader.TextHeight = style.TextHeight;
                leader.Font = style.Font;

                //构造点位
                var points = new List<Point3d>();

                //起点
                points.Add(anchorPoint);

                //拐点
                // 位置 = 起点 + 主推距离 + 堆叠距离
                Point3d elbow = anchorPoint + (moveDir * pushDist*1.5) + (stackDir * pushDist*1.5);
                points.Add(elbow);

                //终点 (Landing/TextPos)
                // 从拐点开始，沿着LandingDir (水平)延伸
                double landingLength = visualTextHeight * 0.8;
                Point3d textPos = elbow + (landingDir * landingLength);
                points.Add(textPos);

                leader.Points3D = points.ToArray();


                dimensions.Add(leader);
            }
        }
        public BoundingBox CreateBox(Point3d c, double w, double h, double p)
        {
            return new BoundingBox(c.X - w / 2 - p, c.Y - h / 2 - p, 0, c.X + w / 2 + p, c.Y + h / 2 + p, 0);
        }

        public bool CheckOverlap(BoundingBox a, List<BoundingBox> list)
        {
            double tol = -0.001;
            foreach (var b in list)
            {
                if (a.Min.X < b.Max.X + tol && a.Max.X > b.Min.X - tol &&
                    a.Min.Y < b.Max.Y + tol && a.Max.Y > b.Min.Y - tol)
                    return true;
            }
            return false;
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
                    var resourceName = "CW2D.Resources.Plane Annotation.png";

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

        public override Guid ComponentGuid => new Guid("CD9087A0-545F-4A63-BB2E-CC6BA7C0DE95");
    }

    internal struct Segment
    {
        internal double start, end, loc;
        public Segment(double a, double b, double c)
        {
            start = a; end = b; loc = c;
        }
    }
}