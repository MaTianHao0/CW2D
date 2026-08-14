using CW2D.Attributes;
using CW2D.MakeNode;
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

namespace CW2D.Node
{
    public class DrawNode : GH_Component
    {
        struct Pair
        {
            internal string name;
            internal Dictionary<string, string> attributes;
            internal double dx; //存储图形的x差值
            internal double dy;//存储图形的y差值

            internal Pair(string name, Dictionary<string, string> attributes, double dx, double dy)
            {
                this.name = name;
                this.attributes = attributes;
                this.dx = dx;
                this.dy = dy;
            }
        }

        public DrawNode() : base("节点图(俯视)", "节点图", "生成节点图", Title.CW2D(), Title.Node())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("幕墙", "幕墙", "幕墙", GH_ParamAccess.list);
            pManager.AddPointParameter("基准点", "基准点", "基准点", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddRectangleParameter("选取范围", "选取范围", "选取范围", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("图样", "图样", "图样", GH_ParamAccess.item);
            pManager.AddPointParameter("定位点", "定位点", "定位点", GH_ParamAccess.list);
            pManager.AddGenericParameter("点属性", "点属性", "物料名称到点集合的映射", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geos = new List<GH_AttributeData>();
            var basePoint = Point3d.Origin;
            var rect = Rectangle3d.Unset;
            if (!DA.GetDataList(0, geos)) return;
            if (!DA.GetData(1, ref basePoint)) return;
            if (!DA.GetData(2, ref rect)) return;

            var results = new List<GeometryBase>();
            var results2 = new List<Point3d>();
            var materialPoints = new Dictionary<string, HashSet<Point3d>>();

            IOFile.LoadPathTable();

            int num = 0;
            var nameMaterialMap = new Dictionary<string, string>();
            var parameters = SetParameters(geos, rect);

            foreach (var geo in geos)
            {
                var goo = geo.Value.Goo;
                var attributes = geo.Value.Attribute;

                if (!CheckBoundary(goo, rect)) continue;

                if (attributes.TryGetValue("图样表示", out var name))
                {
                    if (attributes.TryGetValue("物料名称", out var materialName))
                    {
                        if (!nameMaterialMap.ContainsKey(name))
                            nameMaterialMap.Add(name, materialName);
                    }

                    // 获取原始几何体尺寸
                    var bbox = goo.Boundingbox;
                    double dx = bbox.Max.X - bbox.Min.X;
                    double dy = bbox.Max.Y - bbox.Min.Y;

                    // 保护措施
                    if (dx < 0.1) dx = 0.1;
                    if (dy < 0.1) dy = 0.1;

                    var delta = dy > dx ? new Vector3d(0, 1, 0) : new Vector3d(1, 0, 0);
                    var line = new LineCurve(new Line(bbox.Center + delta, bbox.Center - delta));

                    // 存 Pair
                    num += parameters.AddGeometry(line, Transform.Identity, new Pair(name, attributes, dx, dy)) ? 1 : 0;
                }
                else
                {
                    num += AddGeometry(parameters, goo, Transform.Identity, attributes);
                }
            }

            if (num <= 0) return;

            var hiddenLineDrawing = HiddenLineDrawing.Compute(parameters, true);
            if (hiddenLineDrawing == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Hidden line drawing failed to compute.");
                return;
            }

            var unique = new HashSet<Point3d>();
            foreach (HiddenLineDrawingSegment segment in hiddenLineDrawing.Segments)
            {
                var visibility = segment.SegmentVisibility;
                if (segment.Index < 0 || visibility == HiddenLineDrawingSegment.Visibility.Unset) continue;

                var curve = segment.CurveGeometry.DuplicateCurve();
                if (curve == null) continue;

                var crv = curve.ToNurbsCurve();
                var source = segment.ParentCurve.SourceObject;

                if (source.Tag is Dictionary<string, string> attribute)
                {
                    foreach (var item in attribute)
                        curve.SetUserString(item.Key, item.Value);

                    if (attribute.TryGetValue("物料名称", out var materialName))
                    {
                        var line = new Line(crv.PointAtStart, crv.PointAtEnd);
                        var point = line.PointAt(0.5);

                        if (!unique.Contains(point))
                        {
                            unique.Add(point);
                            results2.Add(point);

                            if (!materialPoints.TryGetValue(materialName, out var set))
                            {
                                set = new HashSet<Point3d>();
                                materialPoints[materialName] = set;
                            }
                            set.Add(point);
                        }
                    }
                }

                //图样替换
                if (source.Tag is Pair tag)
                {
                    var name = tag.name;
                    var attr = tag.attributes;

                    //获取目标尺寸
                    double targetDx = tag.dx;
                    double targetDy = tag.dy;

                    // 如果dy > dx，判定为竖向
                    bool isVertical = targetDy > targetDx;
                    double targetLength = isVertical ? targetDy : targetDx;

                    var line = new Line(crv.PointAtStart, crv.PointAtEnd);
                    var point = line.PointAt(0.5);

                    // 确定旋转：如果是竖向构件，需要旋转90度
                    var angel = isVertical ? Math.PI / 2 : 0;

                    if (!unique.Contains(point))
                    {
                        unique.Add(point);
                        results2.Add(point);

                        if (nameMaterialMap.TryGetValue(name, out var materialName))
                        {
                            if (!materialPoints.TryGetValue(materialName, out var set))
                            {
                                set = new HashSet<Point3d>();
                                materialPoints[materialName] = set;
                            }
                            set.Add(point);
                        }

                        if (IOFile.PathTable.TryGetValue(name, out var filePath))
                        {
                            var ioFile = new IOFile(filePath);
                            ioFile.ReadFile();

                            //炸开图块，Brep转曲线
                            var sourceGeometries = FlattenAndExplode(ioFile.Geometries);

                            //原始包围盒
                            var totalBBox = BoundingBox.Empty;
                            foreach (var g in sourceGeometries) totalBBox.Union(g.GetBoundingBox(true));

                            if (totalBBox.IsValid)
                            {
                                // 假设图块是水平绘制的，长度沿 X 轴
                                double blockLen = totalBBox.Max.X - totalBBox.Min.X;
                                if (blockLen < 0.1) blockLen = 1;

                                //需要拉开的距离
                                double gapSize = targetLength - blockLen;

                                // 切割位置为包围盒的中心 X 坐标
                                double splitX = totalBBox.Center.X;
                                var processedCurves = new List<GeometryBase>();
                                var splitPlane = new Plane(new Point3d(splitX, 0, 0), Vector3d.XAxis); // 切割平面

                                foreach (var geo in sourceGeometries)
                                {
                                    if (geo is Curve subCrv)
                                    {
                                        var bbox = subCrv.GetBoundingBox(true);
                                        if (bbox.Max.X < splitX - 0.001)
                                        {
                                            processedCurves.Add(subCrv.DuplicateCurve());
                                        }
                                        //右边整体向右平移 gapSize
                                        else if (bbox.Min.X > splitX + 0.001)
                                        {
                                            var movedCrv = subCrv.DuplicateCurve();
                                            movedCrv.Translate(gapSize, 0, 0);
                                            processedCurves.Add(movedCrv);
                                        }
                                        else
                                        {
                                            var events = Rhino.Geometry.Intersect.Intersection.CurvePlane(subCrv, splitPlane, 0.001);

                                            if (events != null && events.Count > 0)
                                            {
                                                var splitParams = new List<double>();
                                                foreach (var e in events) splitParams.Add(e.ParameterA);

                                                var pieces = subCrv.Split(splitParams);
                                                if (pieces != null)
                                                {
                                                    foreach (var piece in pieces)
                                                    {
                                                        if (piece.PointAtNormalizedLength(0.5).X > splitX)
                                                        {
                                                            piece.Translate(gapSize, 0, 0);
                                                            processedCurves.Add(piece);
                                                        }
                                                        else
                                                        {
                                                            processedCurves.Add(piece);
                                                        }
                                                    }
                                                }

                                                // 在切断处画直线连接左右两端
                                                foreach (var e in events)
                                                {
                                                    var ptStart = e.PointA; // 左侧断点
                                                    var ptEnd = new Point3d(ptStart.X + gapSize, ptStart.Y, ptStart.Z); // 右侧断点

                                                    // 添加连接线
                                                    processedCurves.Add(new LineCurve(ptStart, ptEnd));
                                                }
                                            }
                                            else
                                            { 
                                                processedCurves.Add(subCrv.DuplicateCurve());
                                            }
                                        }
                                    }
                                    else if (geo is Hatch hatch)
                                    {
                                        //如果在右边就移走
                                        if (hatch.GetBoundingBox(true).Center.X > splitX)
                                        {
                                            var newHatch = (Hatch)hatch.Duplicate();
                                            newHatch.Translate(gapSize, 0, 0);
                                            processedCurves.Add(newHatch);
                                        }
                                        else
                                        {
                                            processedCurves.Add(hatch.Duplicate());
                                        }
                                    }
                                }
                                var finalBBox = BoundingBox.Empty;
                                foreach (var g in processedCurves) finalBBox.Union(g.GetBoundingBox(true));
                                var newBlockCenter = finalBBox.Center;

                                var toOrigin = Transform.Translation(Point3d.Origin - newBlockCenter);
                                var rotate = Transform.Rotation(angel, Vector3d.ZAxis, Point3d.Origin);
                                var toTarget = Transform.Translation(new Vector3d(point));

                                var finalXform = toTarget * rotate * toOrigin;
                                foreach (var g in processedCurves)
                                {
                                    g.Transform(finalXform);
                                    foreach (var item in attr)
                                        g.SetUserString(item.Key, item.Value);

                                    results.Add(g);
                                }
                            }
                        }
                    }
                }

                switch (visibility)
                {
                    case HiddenLineDrawingSegment.Visibility.Visible:
                    case HiddenLineDrawingSegment.Visibility.Clipped:
                        results.Add(curve);
                        continue;
                    case HiddenLineDrawingSegment.Visibility.Hidden:
                        continue;
                    default:
                        continue;
                }
            }

            var movedResults = Move(results, basePoint, out var motion);
            for (int i = 0; i < results2.Count; i++)
            {
                results2[i] += motion;
            }

            DA.SetDataList(0, movedResults);
            DA.SetDataList(1, results2);
            DA.SetData(2, materialPoints);
        }

        private List<GeometryBase> FlattenAndExplode(IEnumerable<GeometryBase> geos)
        {
            var list = new List<GeometryBase>();
            foreach (var g in geos)
            {
                if (g == null) continue;

                if (g is Brep brep)
                {
                    var curves = brep.GetWireframe(-1);
                    if (curves != null) list.AddRange(curves);
                }
                else if (g is Extrusion extrusion)
                {
                    var brepRef = extrusion.ToBrep();
                    if (brepRef != null) list.AddRange(brepRef.GetWireframe(-1));
                }
                else if (g is InstanceReferenceGeometry)
                {
                    //如果是组，需要炸开
                }
                else
                {
                    list.Add(g.Duplicate());
                }
            }
            return list;
        }

        private List<GeometryBase> Move(List<GeometryBase> geometries, Point3d target, out Vector3d motion)
        {
            var bbox = BoundingBox.Empty;
            foreach (var geometry in geometries)
                bbox.Union(geometry.GetBoundingBox(false));
            var basePoint = bbox.Center;

            var result = new List<GeometryBase>();
            motion = target - basePoint;
            foreach (var geometry in geometries)
            {
                var xform = Transform.Translation(motion);
                var movedGeometry = geometry.Duplicate();
                movedGeometry.Transform(xform);
                result.Add(movedGeometry);
            }
            return result;
        }

        bool CheckBoundary(IGH_GeometricGoo goo, Rectangle3d rect)
        {
            int cnt = 0;
            var bbox = goo.Boundingbox;
            for (int i = 0; i < 4; i++)
            {
                var point = bbox.Corner((i & 1) > 0, (i & 2) > 0, true);
                if (rect.Contains(point) == PointContainment.Inside) cnt++;
            }
            return cnt > 0;
        }

        private HiddenLineDrawingParameters SetParameters(List<GH_AttributeData> attrDatas, Rectangle3d rect)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            ViewportInfo viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);
            viewport.SetCameraLocation(rect.Center + new Vector3d(0, 0, 5));
            viewport.SetCameraDirection(rect.Plane.ZAxis);
            viewport.SetCameraUp(rect.Plane.YAxis);
            viewport.SetFrustum(-0.5 * rect.Width, 0.5 * rect.Width, -0.5 * rect.Height,
                0.5 * rect.Height, 0.001, 0.5 * rect.Circumference);
            viewport.SetScreenPort(0, 600, 0, 400, 0, 100);
            parameters.SetViewport(viewport);

            for (int i = 0; i < 4; i++)
            {
                var point0 = rect.Corner(i);
                var point1 = rect.Corner((i + 1) % 4);
                var normal = point1 - point0;
                normal.Rotate(-Math.PI / 2, rect.Plane.Normal);
                parameters.AddClippingPlane(new Plane(point0, normal));
            }
            var plane = rect.Plane.Clone();
            plane.Flip();
            parameters.AddClippingPlane(plane);

            return parameters;
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

        protected override Bitmap Icon
        {
            get
            {
                try
                {
                    bool isHovering = false;
                    if (this.Attributes is GH_ComponentAttributes attributes)
                    {
                        var field = typeof(GH_ComponentAttributes).GetField("m_mouseOver",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        isHovering = (bool)(field?.GetValue(attributes) ?? false);
                    }

                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "CW2D.Resources.Node Drawing.png";

                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var originalIcon = new Bitmap(stream);

                            if (isHovering)
                            {
                                return ResizeIcon(originalIcon, 48, 48);
                            }
                            else if (originalIcon.Width != 24 || originalIcon.Height != 24)
                            {
                                return ResizeIcon(originalIcon, 24, 24);
                            }
                            return originalIcon;
                        }
                        return null;
                    }
                }
                catch { return null; }
            }
        }

        private Bitmap ResizeIcon(Bitmap source, int width, int height)
        {
            var dest = new Bitmap(width, height);
            using (var g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                float scale = Math.Min((float)width / source.Width, (float)height / source.Height);
                int scaledWidth = (int)(source.Width * scale);
                int scaledHeight = (int)(source.Height * scale);
                int x = (width - scaledWidth) / 2;
                int y = (height - scaledHeight) / 2;

                g.Clear(Color.Transparent);
                g.DrawImage(source, x, y, scaledWidth, scaledHeight);
            }
            return dest;
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid => new Guid("{2A8392A7-E699-4BAF-9E50-D4DC2B2061AC}");
    }
}