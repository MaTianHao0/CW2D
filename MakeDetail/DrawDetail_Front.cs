using CW2D.Attributes;
using Eto.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Grasshopper.Rhinoceros.Model;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.MakeDetail
{
    public class DrawDetail_Front : GH_Component
    {
        public DrawDetail_Front()
          : base("大样图(正视)", "大样图(正视)", "大样图(正视)",
              Title.CW2D(), Title.Detail())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("幕墙", "幕墙", "幕墙", GH_ParamAccess.list);
            pManager.AddVectorParameter("方向", "方向", "方向", GH_ParamAccess.item, Vector3d.YAxis);
            pManager.AddVectorParameter("上方向", "上方向", "上方向", GH_ParamAccess.item, Vector3d.ZAxis);
            pManager.AddPointParameter("基准点", "基准点", "基准点", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddPlaneParameter("裁剪平面", "裁剪平面", "裁剪平面", GH_ParamAccess.list);
            pManager.AddRectangleParameter("节点剖切面", "节点剖切面", "节点剖切面", GH_ParamAccess.list);
            pManager.AddTextParameter("节点索引", "节点索引", "节点索引", GH_ParamAccess.list);
            pManager.AddBooleanParameter("开关", "开关", "开关", GH_ParamAccess.item, false);
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("可见线", "可见线", "可见线", GH_ParamAccess.list);
            pManager.AddGeometryParameter("隐藏线", "隐藏线", "隐藏线", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var attrDatas = new List<GH_AttributeData>();
            var direction = Vector3d.YAxis;
            var updirection = Vector3d.ZAxis;
            var basePoint = Point3d.Origin;
            var cuttingPlanes = new List<Plane>();
            var rects = new List<Rectangle3d>();
            var names = new List<string>();
            var trigger = false;

            if (!DA.GetDataList(0, attrDatas)) return;
            if (!DA.GetData(1, ref direction)) return;
            if (!DA.GetData(2, ref updirection)) return;
            if (!DA.GetData(3, ref basePoint)) return;
            DA.GetDataList(4, cuttingPlanes);
            DA.GetDataList(5, rects);
            DA.GetDataList(6, names);
            if (!DA.GetData(7, ref trigger)) return;

            if (!trigger)
            {
                Message = "未触发";
                return;
            }

            //获取结果并输出
            var results0 = new List<GeometryBase>();
            var results1 = new List<GeometryBase>();

            if (attrDatas.Count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "没有输入幕墙");
                return;
            }

            int num = 0;
            var parameters = SetParameters(attrDatas, direction, updirection, cuttingPlanes);
            foreach (var attrData in attrDatas)
            {
                IGH_GeometricGoo goo = attrData.Value.Goo;
                var attributes = attrData.Value.Attribute;

                if (attributes.TryGetValue("门窗", out var flag))
                {
                    var list = GetGeometryBase(goo, Transform.Identity, attributes);
                    foreach (var geo in list)
                    {
                        var bbox = geo.GetBoundingBox(true);
                        var bcen = bbox.Center;
                        var center = new Point3d(bcen.X, bcen.Y, 0);
                        var width = (bbox.Max.X - bbox.Min.X) / 2.0;
                        var height = (bbox.Max.Z - bbox.Min.Z) / 2.0;

                        var pt0 = center + new Vector3d(-width, height, 0);
                        var pt1 = center + new Vector3d(width, height, 0);
                        var pt2 = center + new Vector3d(width, -height, 0);
                        var pt3 = center + new Vector3d(-width, -height, 0);
                        var poly0 = new Polyline(new List<Point3d> { pt0, pt1, pt2, pt3, pt0 });
                        var poly1 = new Polyline();
                        if (flag[0] == 'L') poly1 = new Polyline(new[] { pt0, (pt1 + pt2) / 2, pt3 });
                        if (flag[0] == 'R') poly1 = new Polyline(new[] { pt1, (pt0 + pt3) / 2, pt2 });
                        if (flag[0] == 'U') poly1 = new Polyline(new[] { pt0, (pt2 + pt3) / 2, pt1 });
                        if (flag[0] == 'D') poly1 = new Polyline(new[] { pt2, (pt0 + pt1) / 2, pt3 });

                        var curve0 = poly0.ToNurbsCurve();
                        var curve1 = poly1.ToNurbsCurve();

                        foreach (var item in attributes)
                        {
                            curve0.SetUserString(item.Key, item.Value);
                            curve1.SetUserString(item.Key, item.Value);
                        }

                        results0.Add(curve0);
                        results0.Add(curve1);
                    }
                }
                else
                {
                    num += AddGeometry(parameters, goo, Transform.Identity, attributes);
                }
            }

            for (int i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];
                parameters.AddGeometry(rect.ToNurbsCurve(), i);
                num++;
            }

            var tol = DocumentTolerance();

            if (num > 0)
            {
                //计算
                var hiddenLineDrawing = HiddenLineDrawing.Compute(parameters, true);
                if (hiddenLineDrawing == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Hidden line drawing failed to compute.");
                    return;
                }

                var points = new Point3d[rects.Count, 2];
                for (int i = 0; i < rects.Count; i++)
                {
                    points[i, 0] = new Point3d(double.MaxValue, double.MaxValue, double.MaxValue);
                    points[i, 1] = new Point3d(double.MinValue, double.MinValue, double.MinValue);
                }

                foreach (HiddenLineDrawingSegment segment in hiddenLineDrawing.Segments)
                {
                    var visibility = segment.SegmentVisibility;
                    if (segment.Index < 0 || visibility == HiddenLineDrawingSegment.Visibility.Unset)
                        continue;
                    
                    var curve = segment.CurveGeometry.DuplicateCurve(); 
                    var tag = segment.ParentCurve.SourceObject.Tag;

                    if (tag is int index)
                    {
                        var start = curve.PointAtStart;
                        var end = curve.PointAtEnd;
                        points[index, 0] = Min(Min(points[index, 0], start), end);
                        points[index, 1] = Max(Max(points[index, 1], start), end);
                    }
                    else
                    {
                        var attribute = (Dictionary<string, string>)tag;
                        foreach (var item in attribute)
                            curve.SetUserString(item.Key, item.Value); //属性赋值或属性获取
                        switch (visibility)
                        {
                            case HiddenLineDrawingSegment.Visibility.Visible:
                            case HiddenLineDrawingSegment.Visibility.Clipped:
                                results0.Add(curve);
                                continue;
                            case HiddenLineDrawingSegment.Visibility.Hidden:
                                curve.SetUserString("线型", "Dashed");
                                results1.Add(curve);
                                continue;
                            default:
                                continue;
                        }
                    }
                }

                for (int i = 0; i < rects.Count; i++)
                {
                    var start = points[i, 0];
                    var end = points[i, 1];
                    var dire = /*rects[i].Plane.Normal*/-Vector3d.YAxis;
                    var name = names[i];
                    results0.AddRange(DrawNodeIndex(start, end, dire, name));
                }
            }

            var bbox0 = BoundingBox.Empty;
            foreach (var geometry in results0)
                bbox0.Union(geometry.GetBoundingBox(false));
            foreach (var geometry in results1)
                bbox0.Union(geometry.GetBoundingBox(false));
            var movedResults0 = Move(results0, basePoint - bbox0.Center);
            var movedResults1 = Move(results1, basePoint - bbox0.Center);

            DA.SetDataList(0, movedResults0);
            DA.SetDataList(1, movedResults1);
        }

        private List<GeometryBase> Move(List<GeometryBase> geometries, Vector3d motion)
        {
            var result = new List<GeometryBase>();
            foreach (var geometry in geometries)
            {
                var xform = Transform.Translation(motion);
                var movedGeometry = geometry.Duplicate();
                movedGeometry.Transform(xform);
                result.Add(movedGeometry);
            }
            return result;
        }

        Point3d Min(Point3d pointA, Point3d pointB)
        {
            return pointA < pointB ? pointA : pointB;
        }

        Point3d Max(Point3d pointA, Point3d pointB)
        {
            return pointA > pointB ? pointA : pointB;
        }

        List<GeometryBase> DrawNodeIndex(Point3d start, Point3d end, Vector3d directon, string name)
        {
            var results = new List<GeometryBase>();
            directon.Unitize();
            var len = start.DistanceTo(end);
            var tol = DocumentTolerance();

            //主线
            var mainLine = new Line(start, end);
            results.Add(new LineCurve(mainLine));

            //副线
            var dist = Math.Min(len * 0.04, 50);
            var subLine = new Line(mainLine.PointAt(0.8) + directon * dist, end + directon * dist);
            results.Add(new LineCurve(subLine));

            //圆
            var vector = start - end; vector.Unitize();
            var len0 = Math.Min(len * 0.5, 600);
            var p0 = start;
            var p1 = p0 + vector * len0;
            var p2 = (p0 + p1) / 2 + directon * len0 * 0.5;
            var polyLine = new Polyline(new List<Point3d> { p0, p1, p2, p0 });
            var center = (p0 + p1) / 2;
            var r = new Line(p0, p2).ClosestPoint(center, true).DistanceTo(center) * 0.95;
            var circle = new Circle(center, r);
            results.Add(new ArcCurve(circle));

            //三角
            var area0 = Brep.CreatePlanarBreps(polyLine.ToNurbsCurve(), tol);
            var area1 = Brep.CreatePlanarBreps(circle.ToNurbsCurve(), tol);
            var area = Brep.CreateBooleanDifference(area0, area1, tol);
            if (area != null)
            {
                var basePoint = area[0].GetBoundingBox(true).Center;
                var hatch = Hatch.CreateFromBrep(area[0], 0, 0, 0, 1, basePoint);
                results.Add(hatch);
            }

            //文字
            var textHeight = r * 1.2;
            var dimStyle = RhinoDoc.ActiveDoc.DimStyles.Current;
            var text = new TextEntity
            {
                Plane = new Plane(center, Vector3d.ZAxis),
                PlainText = name,
                TextHeight = textHeight,
                DimensionStyleId = dimStyle.Id,
                Justification = TextJustification.Center
            };
            results.Add(text);

            return results;
        }

        private static HiddenLineDrawingParameters SetParameters(List<GH_AttributeData> attrDatas, Vector3d vector, Vector3d upVector, List<Plane> cuttingPlanes)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            double left = double.MaxValue, right = double.MinValue;
            double front = double.MaxValue, back = double.MinValue;
            double bottom = double.MaxValue, top = double.MinValue;
            foreach (var attrData in attrDatas)
            {
                var bbox = attrData.Value.Goo.Boundingbox;
                left = Math.Min(left, bbox.Min.X);
                right = Math.Max(right, bbox.Max.X);
                front = Math.Min(front, bbox.Min.Y);
                back = Math.Max(back, bbox.Max.Y);
                bottom = Math.Min(bottom, bbox.Min.Z);
                top = Math.Max(top, bbox.Max.Z);
            }

            vector.Unitize();
            upVector.Unitize();
            var viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);
            var center = new Point3d((left + right) / 2.0, (front + back) / 2.0, (bottom + top) / 2.0);
            var dis = Math.Sqrt(Math.Pow(right - left, 2) + Math.Pow(back - front, 2) + Math.Pow(top - bottom, 2)) / 2.0 + 5.0;
            viewport.SetCameraLocation(center - vector * dis);
            viewport.SetCameraDirection(vector);
            viewport.SetCameraUp(upVector);

            viewport.SetFrustum(
                -dis, dis,
                -dis, dis,
                0.01, dis * 2
            );
            viewport.SetScreenPort(
                left: 0, right: 600,
                top: 0, bottom: 400,
                near: 0, far: 100
            );
            parameters.SetViewport(viewport);

            foreach (var plane in cuttingPlanes)
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

        private List<GeometryBase> GetGeometryBase(IGH_GeometricGoo goo, Transform xform, Dictionary<string, string> attributes)
        {
            var results = new List<GeometryBase>();
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
                            if (geometry != null) results.AddRange(GetGeometryBase(geometry, xform1, attributes));
                        }
                    }
                    return results;
                case GH_GeometryGroup ghGeometryGroup:
                    foreach (IGH_GeometricGoo ghGeometricGoo in ghGeometryGroup.Objects)
                    {
                        if (ghGeometricGoo != null)
                        {
                            IGH_GeometricGoo goo1 = ghGeometricGoo;
                            results.AddRange(GetGeometryBase(goo1, xform, attributes));
                        }
                    }
                    return results;
                default:
                    GeometryBase geometry1 = goo is GH_Extrusion ghExtrusion ? ghExtrusion.Value.ToBrep() : goo is GH_SubD ghSubD ? ghSubD.Value.ToBrep() : GH_Convert.ToGeometryBase(goo);
                    if (geometry1 == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Data of type {goo.TypeName} could not be converted into Rhino geometry.");
                        return new List<GeometryBase>();
                    }
                    return new List<GeometryBase> { geometry1 };
            }
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
                    var resourceName = "CW2D.Resources.detail-front.png";

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
            get { return new Guid("253AB0F5-132A-45C0-9324-8785AFE12C4F"); }
        }
    }
}