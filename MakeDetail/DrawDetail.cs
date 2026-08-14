using CW2D.Attributes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Grasshopper.Rhinoceros.Model;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.Detail
{
    public class DrawDetail : GH_Component
    {
        public DrawDetail() : base("大样图", "大样图", "生成大样图框架", Title.CW2D(), Title.Detail())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("幕墙", "幕墙", "请输入幕墙", GH_ParamAccess.list);
            pManager.AddNumberParameter("剖面位置", "剖面位置", "纵向剖面位置 (0-1之间)", GH_ParamAccess.item, 0.5);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("可见线", "可见线", "可见线", GH_ParamAccess.list);
            pManager.AddCurveParameter("隐藏线", "隐藏线", "隐藏线", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geos = new List<GH_AttributeData>();
            double sectionPosition = 0.5;

            if (!DA.GetDataList(0, geos)) return;
            if (!DA.GetData(1, ref sectionPosition)) return;

            // 限制剖面位置在0-1之间
            sectionPosition = Math.Max(0.0, Math.Min(1.0, sectionPosition));

            // --- 第1步：计算总包围盒 ---
            BoundingBox totalBBox = BoundingBox.Empty;
            foreach (var geo in geos)
            {
                totalBBox.Union(geo.Value.Goo.Boundingbox);
            }
            // 如果BBox无效（比如只有一个点），则给一个默认大小
            if (!totalBBox.IsValid)
            {
                totalBBox.Union(new Point3d(0, 0, 0));
                totalBBox.Inflate(1.0); // 膨胀1个单位
            }

            // 计算归一化变换 ---
            // 获取包围盒中心点
            Point3d center = totalBBox.Center;
            // 创建一个从中心点移动到世界原点(0,0,0)的变换
            Transform moveToOrigin = Transform.Translation(-center.X, -center.Y, -center.Z);

            double d = 5.0; // 边距

            // 顶视图 (Up View) 的宽高
            double topViewWidth = (totalBBox.Max.X - totalBBox.Min.X) + d * 2;
            double topViewHeight = (totalBBox.Max.Y - totalBBox.Min.Y) + d * 2;

            // 前视图 (Front View) 的宽高
            double frontViewWidth = topViewWidth; // 和顶视图一样宽
            double frontViewHeight = (totalBBox.Max.Z - totalBBox.Min.Z) + d * 2;

            // 左视图 (Left View) 的宽高
            double leftViewWidth = topViewHeight; // 左视图的宽度 = 模型的深度(Y)
            double leftViewHeight = frontViewHeight; // 和前视图一样高

            // 纵向剖面图 (Longitudinal Section) 的宽高
            double sectionViewWidth = topViewWidth; // 和顶视图一样宽
            double sectionViewHeight = frontViewHeight; // 和前视图一样高

            double viewSpacing = frontViewHeight * 0.1; // 视图间距,设定为左视图高度的10%

            // --- 第4步：用真实宽高计算每个视图的起始位置 (v) ---
            double v_Up = 0;
            double v_Front = v_Up + topViewWidth + viewSpacing;
            double v_Left = v_Front + frontViewWidth + viewSpacing;
            double v_Section = v_Front + frontViewWidth + viewSpacing;    // 纵向剖面图位置

            // --- 第5步：设置各个视图的参数 ---
            var parametersUp = SetParametersUp(totalBBox, v_Up, topViewWidth, topViewHeight, d);
            var parametersFront = SetParametersFront(totalBBox, v_Front, frontViewWidth, frontViewHeight, d);
            var parametersLeft = SetParametersLeft(totalBBox, v_Left, leftViewWidth, leftViewHeight, d);
            var parametersSection = SetParametersSection(totalBBox, 0, sectionViewWidth, sectionViewHeight, d, sectionPosition);

            // --- 第6步：添加几何体时，传入 moveToOrigin 变换 ---
            int num = 0;
            foreach (var geo in geos)
            {
                var goo = geo.Value.Goo;
                var attributes = geo.Value.Attribute;
                // 将同一个几何体分别添加到四个不同的视图参数中
                // 使用 moveToOrigin 变换将几何体"虚拟地"移动到原点
                num += AddGeometry(parametersUp, goo, moveToOrigin, attributes);
                AddGeometry(parametersFront, goo, moveToOrigin, attributes);
                AddGeometry(parametersLeft, goo, moveToOrigin, attributes);
                AddGeometry(parametersSection, goo, moveToOrigin, attributes);
            }
            if (num < 1) return;

            // ... (TextEntity 相关的代码被注释掉了) ...

            // 分别计算四个视图的消隐线，并分开可见线和隐藏线
            var (visibleUp, hiddenUp) = Calculate(parametersUp);
            var (visibleFront, hiddenFront) = Calculate(parametersFront);
            var (visibleLeft, hiddenLeft) = Calculate(parametersLeft);
            var (visibleSection, hiddenSection) = Calculate(parametersSection);

            // 绕 Z 轴旋转 -PI/2 弧度 (即 -90度)
            Transform rotation = Transform.Rotation(-Math.PI / 2, Vector3d.ZAxis, Point3d.Origin);

            // --- 对可见线应用变换 ---

            // 旋转顶视图可见线
            foreach (Curve curve in visibleUp)
            {
                curve.Transform(rotation);
            }

            // 旋转前视图可见线并平移
            Transform xformFront = Transform.Translation(0, -(frontViewWidth / 2 + viewSpacing), 0);
            foreach (Curve curve in visibleFront)
            {
                curve.Transform(rotation);
                curve.Transform(xformFront);
            }

            // 旋转左视图可见线并平移
            Transform xformLeft = Transform.Translation(0, leftViewHeight / 2 + viewSpacing, 0);
            foreach (Curve curve in visibleLeft)
            {
                curve.Transform(rotation);
                curve.Transform(xformLeft);
            }

            // 旋转纵向剖面图可见线并平移
            Transform xformSection = Transform.Translation(-viewSpacing - v_Section, (leftViewHeight / 2 + viewSpacing), 0);
            foreach (Curve curve in visibleSection)
            {
                curve.Transform(xformSection);
            }

            // --- 对隐藏线应用相同的变换 ---

            // 旋转顶视图隐藏线
            foreach (Curve curve in hiddenUp)
            {
                curve.Transform(rotation);
            }

            // 旋转前视图隐藏线并平移
            foreach (Curve curve in hiddenFront)
            {
                curve.Transform(rotation);
                curve.Transform(xformFront);
            }

            // 旋转左视图隐藏线并平移
            foreach (Curve curve in hiddenLeft)
            {
                curve.Transform(rotation);
                curve.Transform(xformLeft);
            }

            // 旋转纵向剖面图隐藏线并平移
            foreach (Curve curve in hiddenSection)
            {
                /*curve.Transform(rotation);*/
                curve.Transform(xformSection);
            }

            // 合并所有可见线
            var allVisibleCurves = new List<Curve>();
            allVisibleCurves.AddRange(visibleUp);
            allVisibleCurves.AddRange(visibleFront);
            allVisibleCurves.AddRange(visibleLeft);
            allVisibleCurves.AddRange(visibleSection);

            // 合并所有隐藏线
            var allHiddenCurves = new List<Curve>();
            allHiddenCurves.AddRange(hiddenUp);
            allHiddenCurves.AddRange(hiddenFront);
            allHiddenCurves.AddRange(hiddenLeft);
            allHiddenCurves.AddRange(hiddenSection);

            // 分别输出到两个参数
            DA.SetDataList(0, allVisibleCurves);  // 可见线 → 参数0
            DA.SetDataList(1, allHiddenCurves);   // 隐藏线 → 参数1
        }

        // 设置纵向剖面图参数
        private HiddenLineDrawingParameters SetParametersSection(BoundingBox bbox, double v, double viewWidth, double viewHeight, double d, double sectionPosition)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            // ---- 1. 获取 "centered" 尺寸 ----
            double modelWidthX = bbox.Max.X - bbox.Min.X;
            double modelDepthY = bbox.Max.Y - bbox.Min.Y;
            double modelHeightZ = bbox.Max.Z - bbox.Min.Z;

            double left = -modelWidthX / 2.0;
            double right = modelWidthX / 2.0;
            double front = -modelDepthY / 2.0;
            double back = modelDepthY / 2.0;
            double floor = -modelHeightZ / 2.0;
            double ceiling = modelHeightZ / 2.0;

            // 计算剖面位置 (在X方向上的位置)
            double sectionX = bbox.Min.X + (bbox.Max.X - bbox.Min.X) * sectionPosition;
            double sectionXTransformed = sectionX - bbox.Center.X;


            var viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);

            // ---- 2. 设置相机（纵向剖面图，从Y轴正方向观察）----
            // 相机位置：在模型"新"最前方
            viewport.SetCameraLocation(new Point3d(sectionX, front - d, 0));
            viewport.SetCameraDirection(Vector3d.YAxis);
            viewport.SetCameraUp(Vector3d.ZAxis);

            // ---- 3. 设置3D视锥 (Frustum) ----
            viewport.SetFrustum(
                floor - d, ceiling + d,     // 3D B/T (Model Z)
                left - sectionX - d, right - sectionX + d, // 3D L/R (相对于剖面的X方向)
                0.01, modelDepthY + 2 * d   // 3D N/F (Model Y 深度)
            );


            // ---- 4. 设置2D屏幕端口 (ScreenPort) ----
            viewport.SetScreenPort(
                left: (int)v,
                right: (int)(v + viewWidth),
                top: 0,
                bottom: (int)viewHeight,
                near: 0,
                far: 10
            );
            parameters.SetViewport(viewport);

            // ---- 5. 添加剖切平面 ----
            // 在剖面位置添加一个剖切平面，只显示剖切面后的几何体
            var sectionPlane = new Plane(new Point3d(sectionXTransformed, 0, 0), Vector3d.XAxis);
            parameters.AddClippingPlane(sectionPlane);

            return parameters;
        }

        // 其他方法保持不变...
        private List<Curve> ReferBox(string refer, Point3d point1, Point3d point2, out Point3d center)
        {
            // 计算四个角点
            double z = point1.Z;

            var pt0 = point1;                             // 左上 
            var pt1 = new Point3d(point2.X, point1.Y, z); // 右上
            var pt2 = point2;                             // 右下
            var pt3 = new Point3d(point1.X, point2.Y, z); // 左下

            double width = point2.X - point1.X;
            double height = point1.Y - point2.Y;
            double shortSide = Math.Min(width, height);

            var results = new List<Curve>();

            // 四段直线
            double radius = shortSide / 8.0;
            results.Add(new Line(pt0, pt1 + new Vector3d(-radius, 0, 0)).ToNurbsCurve());
            results.Add(new Line(pt1 + new Vector3d(0, -radius, 0), pt2 + new Vector3d(0, radius, 0)).ToNurbsCurve());
            results.Add(new Line(pt2 + new Vector3d(-radius, 0, 0), pt3 + new Vector3d(radius, 0, 0)).ToNurbsCurve());
            results.Add(new Line(pt3 + new Vector3d(0, radius, 0), pt0).ToNurbsCurve());

            // 三段圆角
            results.Add(new Arc(pt1 + new Vector3d(-radius, 0, 0), new Vector3d(1, 0, 0), pt1 + new Vector3d(0, -radius, 0)).ToNurbsCurve());
            results.Add(new Arc(pt2 + new Vector3d(0, radius, 0), new Vector3d(0, -1, 0), pt2 + new Vector3d(-radius, 0, 0)).ToNurbsCurve());
            results.Add(new Arc(pt3 + new Vector3d(radius, 0, 0), new Vector3d(-1, 0, 0), pt3 + new Vector3d(0, radius, 0)).ToNurbsCurve());

            // 引用
            var radius0 = shortSide / 3.0;
            var length = shortSide / 10.0;
            center = pt0 + new Vector3d(0, length + radius0, 0);
            results.Add(new Line(pt0, pt0 + new Vector3d(0, length, 0)).ToNurbsCurve());
            results.Add(new Circle(center, radius0).ToNurbsCurve());

            return results;
        }

        private int AddGeometry(HiddenLineDrawingParameters hlr, IGH_GeometricGoo goo, Transform xform, Dictionary<string, string> attributes)
        {
            int num = 0;
            switch (goo)
            {
                case GH_InstanceReference instanceReference:
                    ModelInstanceDefinition instanceDefinition = instanceReference.InstanceDefinition;
                    if (instanceDefinition != null && instanceDefinition.Objects != null)
                    {
                        Transform xform1 = xform * instanceReference.Value.Xform;
                        foreach (ModelObject modelObject in (IEnumerable<ModelObject>)instanceDefinition.Objects)
                        {
                            var geometryProperty = typeof(ModelObject).GetProperty("Geometry", BindingFlags.NonPublic | BindingFlags.Instance);
                            var geometry = geometryProperty?.GetValue(modelObject) as IGH_GeometricGoo;
                            if (geometry != null) num += AddGeometry(hlr, geometry, xform1, attributes);
                        }
                    }
                    return num;
                case GH_GeometryGroup ghGeometryGroup:
                    foreach (IGH_GeometricGoo ghGeometricGoo in ghGeometryGroup.Objects)
                    {
                        if (ghGeometricGoo != null)
                        {
                            IGH_GeometricGoo goo1 = ghGeometricGoo;
                            num += AddGeometry(hlr, goo1, xform, attributes);
                        }
                    }
                    return num;
                default:
                    GeometryBase geometry1 = goo is GH_Extrusion ghExtrusion ? ghExtrusion.Value.ToBrep() : goo is GH_SubD ghSubD ? ghSubD.Value.ToBrep() : GH_Convert.ToGeometryBase(goo);
                    if (geometry1 == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Data of type {goo.TypeName} could not be converted into Rhino geometry.");
                        return 0;
                    }
                    if (hlr.AddGeometry(geometry1, xform, attributes, true)) return 1;
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Geometry of type {geometry1.GetType().Name} is not supported for hidden line drawings.");
                    return 0;
            }
        }

        // --- 其他SetParameters方法保持不变 ---
        private HiddenLineDrawingParameters SetParametersUp(BoundingBox bbox, double v, double viewWidth, double viewHeight, double d)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            double modelWidthX = bbox.Max.X - bbox.Min.X;
            double modelDepthY = bbox.Max.Y - bbox.Min.Y;
            double modelHeightZ = bbox.Max.Z - bbox.Min.Z;

            double left = -modelWidthX / 2.0;
            double right = modelWidthX / 2.0;
            double front = -modelDepthY / 2.0;
            double back = modelDepthY / 2.0;
            double ceiling = modelHeightZ / 2.0;

            var viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);

            viewport.SetCameraLocation(new Point3d(0, 0, ceiling + d));
            viewport.SetCameraDirection(-Vector3d.ZAxis);
            viewport.SetCameraUp(Vector3d.YAxis);

            viewport.SetFrustum(
                left - d, right + d,
                front - d, back + d,
                0.01, modelHeightZ + d
            );

            viewport.SetScreenPort(
               left: (int)(v),
               right: (int)(v + viewWidth),
               top: 0,
               bottom: (int)viewHeight,
               near: 0,
               far: 10
            );
            parameters.SetViewport(viewport);

            return parameters;
        }

        private HiddenLineDrawingParameters SetParametersFront(BoundingBox bbox, double v, double viewWidth, double viewHeight, double d)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            double modelWidthX = bbox.Max.X - bbox.Min.X;
            double modelDepthY = bbox.Max.Y - bbox.Min.Y;
            double modelHeightZ = bbox.Max.Z - bbox.Min.Z;

            double left = -modelWidthX / 2.0;
            double right = modelWidthX / 2.0;
            double front = -modelDepthY / 2.0;
            double floor = -modelHeightZ / 2.0;
            double ceiling = modelHeightZ / 2.0;

            var viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);

            viewport.SetCameraLocation(new Point3d(0, front - d, 0));
            viewport.SetCameraDirection(Vector3d.YAxis);
            viewport.SetCameraUp(Vector3d.ZAxis);

            viewport.SetFrustum(
                left - d, right + d,
                floor - d, ceiling + d,
                0.01, modelDepthY + 2 * d
            );

            viewport.SetScreenPort(
                left: (int)v,
                right: (int)(v + viewWidth),
                top: 0,
                bottom: (int)viewHeight,
                near: 0,
                far: 10
            );
            parameters.SetViewport(viewport);

            return parameters;
        }

        private HiddenLineDrawingParameters SetParametersLeft(BoundingBox bbox, double v, double viewWidth, double viewHeight, double d)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            double modelWidthX = bbox.Max.X - bbox.Min.X;
            double modelDepthY = bbox.Max.Y - bbox.Min.Y;
            double modelHeightZ = bbox.Max.Z - bbox.Min.Z;

            double left = -modelWidthX / 2.0;
            double front = -modelDepthY / 2.0;
            double back = modelDepthY / 2.0;
            double floor = -modelHeightZ / 2.0;
            double ceiling = modelHeightZ / 2.0;

            var viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);

            viewport.SetCameraLocation(new Point3d(left - d, 0, 0));
            viewport.SetCameraDirection(Vector3d.XAxis);
            viewport.SetCameraUp(Vector3d.ZAxis);

            viewport.SetFrustum(
               front - d, back + d,
               floor - d, ceiling + d,
               0.01, modelWidthX + 2 * d
            );

            viewport.SetScreenPort(
                left: (int)v,
                right: (int)(v + viewWidth),
                top: 0,
                bottom: (int)viewHeight,
                near: 0,
                far: 10
            );
            parameters.SetViewport(viewport);

            return parameters;
        }

        private (List<Curve> visibleCurves, List<Curve> hiddenCurves) Calculate(HiddenLineDrawingParameters parameters)
        {
            var visibleCurves = new List<Curve>();
            var hiddenCurves = new List<Curve>();

            var hiddenLineDrawing = HiddenLineDrawing.Compute(parameters, true);

            foreach (HiddenLineDrawingSegment segment in hiddenLineDrawing.Segments)
            {
                var visibility = segment.SegmentVisibility;
                if (segment.Index < 0 || visibility == HiddenLineDrawingSegment.Visibility.Unset)
                    continue;

                var curve = segment.CurveGeometry.DuplicateCurve();
                var source = segment.ParentCurve.SourceObject;
                var attribute = (Dictionary<string, string>)source.Tag;
                foreach (var item in attribute)
                    curve.SetUserString(item.Key, item.Value);

                switch (visibility)
                {
                    case HiddenLineDrawingSegment.Visibility.Visible:
                    case HiddenLineDrawingSegment.Visibility.Clipped:
                        visibleCurves.Add(curve);
                        continue;
                    case HiddenLineDrawingSegment.Visibility.Hidden:
                        hiddenCurves.Add(curve);
                        continue;
                    default:
                        continue;
                }
            }

            return (visibleCurves, hiddenCurves);
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
                    var resourceName = "CW2D.Resources.Detail Drawing.png";

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
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        public override Guid ComponentGuid => new Guid("{CAEC480C-52F7-48CF-A05A-9077373BA32D}");
    }
}