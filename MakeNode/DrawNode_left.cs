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
using System.Linq;
using System.Reflection;

namespace CW2D.MakeNode
{
    public class DrawNode_left : GH_Component
    {
        public DrawNode_left() : base("节点图(左视)", "节点图", "生成节点图", Title.CW2D(), Title.Node())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("幕墙", "幕墙", "幕墙", GH_ParamAccess.list);
            pManager.AddRectangleParameter("选取范围", "选取范围", "选取范围", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("图样", "G", "G", GH_ParamAccess.item);
            pManager.AddPointParameter("定位点", "P", "定位点", GH_ParamAccess.list);
            pManager.AddGenericParameter("点属性", "点属性", "物料名称到点集合的映射", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geos = new List<GH_AttributeData>();
            var rect = Rectangle3d.Unset;
            if (!DA.GetDataList(0, geos)) return;
            if (!DA.GetData(1, ref rect)) return;

            var plane = rect.Plane;
            if (plane.Normal.X < 0)
            {
                var plane0 = new Plane(plane.Origin, -plane.Normal);
                rect.Plane = plane0;
            }

            var results = new List<Curve>();
            var materialPoints = new Dictionary<string, HashSet<Point3d>>();

            IOFile.LoadPathTable();

            var nameMaterialMap = new Dictionary<string, string>();
            var unique = new HashSet<Point3d>();

            int num = 0;
            var parameters = SetParameters(geos, rect);

            // 遍历每个几何体
            foreach (var geo in geos)
            {
                var goo = geo.Value.Goo;
                var attributes = geo.Value.Attribute;

                if (!CheckBoundary(goo, rect)) continue;
                if (goo.Boundingbox.Max.X < rect.Center.X) continue;

                if (attributes.TryGetValue("图样表示", out var name))
                {
                    if (attributes.TryGetValue("物料名称", out var materialName))
                    {
                        if (!nameMaterialMap.ContainsKey(name))
                            nameMaterialMap.Add(name, materialName);
                    }

                    var bbox = goo.Boundingbox;
                    double dy = bbox.Max.Y - bbox.Min.Y;
                    double dx = bbox.Max.X - bbox.Min.X;
                    var delta = dy > dx ? new Vector3d(0, 1, 0) : new Vector3d(1, 0, 0);
                    var line = new LineCurve(new Line(bbox.Center + delta, bbox.Center - delta));
                    num += parameters.AddGeometry(line, Transform.Identity, name) ? 1 : 0;
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

            foreach (HiddenLineDrawingSegment segment in hiddenLineDrawing.Segments)
            {
                var visibility = segment.SegmentVisibility;
                if (segment.Index < 0 || visibility == HiddenLineDrawingSegment.Visibility.Unset) continue;

                var curve = segment.CurveGeometry.DuplicateCurve();
                var source = segment.ParentCurve.SourceObject;

                if (source.Tag is Dictionary<string, string> attribute)
                {
                    foreach (var item in attribute)
                        curve.SetUserString(item.Key, item.Value);

                    if (attribute.TryGetValue("物料名称", out var materialName))
                    {
                        var line = new Line(curve.PointAtStart, curve.PointAtEnd);
                        var point = line.PointAt(0.5);

                        if (!unique.Contains(point))
                        {
                            unique.Add(point);
                            if (!materialPoints.ContainsKey(materialName))
                                materialPoints[materialName] = new HashSet<Point3d>();
                            materialPoints[materialName].Add(point);
                        }
                    }
                }

                switch (visibility)
                {
                    case HiddenLineDrawingSegment.Visibility.Visible:
                    case HiddenLineDrawingSegment.Visibility.Clipped:
                        results.Add(curve);
                        break;
                    case HiddenLineDrawingSegment.Visibility.Hidden:
                        results.Add(curve);
                        break;
                    default:
                        continue;
                }
            }

            DA.SetDataList(0, results);
            DA.SetDataList(1, unique.ToList());  // 定位点输出
            DA.SetData(2, materialPoints);       // 点属性输出
        }

        private bool CheckBoundary(IGH_GeometricGoo goo, Rectangle3d rect)
        {
            int count = 0;
            var bbox = goo.Boundingbox;
            if (rect.Contains(bbox.Min) == PointContainment.Inside) count++;
            if (rect.Contains(bbox.Max) == PointContainment.Inside) count++;
            return count > 0;
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
            viewport.SetCameraLocation(rect.Center + new Vector3d(0, 0, 5));  // 与俯视图一致的相机设置
            viewport.SetCameraDirection(rect.Plane.ZAxis);  // 与俯视图一致的投影方向
            viewport.SetCameraUp(rect.Plane.YAxis);  // 与俯视图一致的相机方向
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
                    var instanceDefinition = instanceReference.InstanceDefinition;
                    if (instanceDefinition != null && instanceDefinition.Objects != null)
                    {
                        var xform1 = xform * instanceReference.Value.Xform;
                        foreach (var modelObject in instanceDefinition.Objects)
                        {
                            var geometryProperty = typeof(ModelObject).GetProperty("Geometry", BindingFlags.NonPublic | BindingFlags.Instance);
                            var geometry = geometryProperty?.GetValue(modelObject) as IGH_GeometricGoo;
                            if (geometry != null) num += AddGeometry(hlr, geometry, xform1, attributes);
                        }
                    }
                    return num;
                case GH_GeometryGroup ghGeometryGroup:
                    foreach (var ghGeometricGoo in ghGeometryGroup.Objects)
                    {
                        if (ghGeometricGoo != null)
                        {
                            num += AddGeometry(hlr, ghGeometricGoo, xform, attributes);
                        }
                    }
                    return num;
                default:
                    var geometry1 = goo is GH_Extrusion ghExtrusion ? ghExtrusion.Value.ToBrep() : goo is GH_SubD ghSubD ? ghSubD.Value.ToBrep() : GH_Convert.ToGeometryBase(goo);
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
                    var resourceName = "CW2D.Resources.Node -left.png";

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
        public override Guid ComponentGuid
        {
            get { return new Guid("B3B67AA4-1558-4FA1-B3FE-97988151022C"); }
        }
    }
}
