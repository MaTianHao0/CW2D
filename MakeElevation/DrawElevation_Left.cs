using CW2D.Attributes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Rhinoceros.Model;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Grasshopper.Kernel.Attributes;


namespace CW2D.MakeElevation
{

    public class DrawElevation_Left : GH_Component
    {
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }
        public DrawElevation_Left() : base("立面图-左视图", "立面图", "生成立面左视图", Title.CW2D(), Title.Elevation())
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("幕墙", "幕墙", "幕墙", GH_ParamAccess.list);
            pManager.AddNumberParameter("可见面", "可见面", "可见面", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("裁剪面", "裁剪面", "裁剪面", GH_ParamAccess.item);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("可见线", "可见线", "可见线", GH_ParamAccess.list);
            pManager.AddCurveParameter("隐藏线", "隐藏线", "隐藏线", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var attrDatas = new List<GH_AttributeData>();
            var floor = new double();
            var ceiling = new double();

            if (!DA.GetDataList(0, attrDatas)) return;
            if (!DA.GetData(1, ref floor)) return;
            if (!DA.GetData(2, ref ceiling)) return;

            //获取结果并输出
            var results1 = new List<Curve>();
            var results2 = new List<Curve>();

            if (floor >= ceiling)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "顶面必须高于底面");
                return;
            }
            if (attrDatas.Count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "没有输入幕墙");
                return;
            }

            int num = 0;
            var parameters = SetParameters(attrDatas, floor, ceiling);
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
                        var center = new Point3d(bcen.Y, bcen.Z, 0);
                        var width = (bbox.Max.Y - bbox.Min.Y) / 2.0;
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

                        results1.Add(curve0);
                        results1.Add(curve1);
                    }
                }
                else
                {
                    num += AddGeometry(parameters, goo, Transform.Identity, attributes);
                }
            }

            if (num > 0)
            {
                //计算
                var hiddenLineDrawing = HiddenLineDrawing.Compute(parameters, true);
                if (hiddenLineDrawing == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Hidden line drawing failed to compute.");
                    return;
                }

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
                            results1.Add(curve);
                            continue;
                        case HiddenLineDrawingSegment.Visibility.Hidden:
                            curve.SetUserString("线型", "Dashed");
                            results2.Add(curve);
                            continue;
                        default:
                            continue;
                    }
                }
            }

            DA.SetDataList(0, results1);
            DA.SetDataList(1, results2);
        }

        //设置HLDP参数
        private HiddenLineDrawingParameters SetParameters(List<GH_AttributeData> attrDatas, double floor, double ceiling)
        {
            var parameters = new HiddenLineDrawingParameters()
            {
                AbsoluteTolerance = DocumentTolerance(),
                IncludeTangentEdges = true,
                IncludeTangentSeams = true,
                Flatten = true
            };

            double left = 0.0, right = 0.0;
            double bottom = 0.0, top = 0.0;
            double front = 0.0, back = 0.0;


            foreach (var attrData in attrDatas)
            {
                IGH_GeometricGoo goo = attrData.Value.Goo;
                BoundingBox bbox = goo.Boundingbox;
                left = Math.Min(left, bbox.Min.X);
                right = Math.Max(right, bbox.Max.X);

                bottom = Math.Min(bottom, bbox.Min.Z);
                top = Math.Max(top, bbox.Max.Z);

                front = Math.Min(front, bbox.Min.Y);
                back = Math.Max(back, bbox.Max.Y);
            }

            var viewport = new ViewportInfo();
            viewport.ChangeToParallelProjection(true);

            viewport.SetCameraLocation(new Point3d(left - 5.0, 0, 0));
            // 修改相机方向：从X轴负方向指向X轴正方向
            viewport.SetCameraDirection(Vector3d.XAxis);
            // 保持相机上方向为Z轴正方向
            viewport.SetCameraUp(Vector3d.ZAxis);

            double d = 5.0;
            viewport.SetFrustum(
                front - d, back + d,
                bottom - d, top + d,
                0.01, right - left
             );
            viewport.SetScreenPort(
                left: 0, right: 600,
                top: 0, bottom: 400,
                near: 0, far: 100
            );
            parameters.SetViewport(viewport);

            var plane1 = new Plane(new Point3d(floor, 0, 0), -Vector3d.XAxis);
            var plane2 = new Plane(new Point3d(ceiling, 0, 0), Vector3d.XAxis);

            parameters.AddClippingPlane(plane1);
            parameters.AddClippingPlane(plane2);

            return parameters;
        }

        //读取并处理几何体信息
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

        //从IGH_GeometricGoo中提取GeometryBase对象
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
                    var resourceName = "CW2D.Resources.Elevation Drawing.png";

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
            get { return new Guid("6392C388-2A9C-4BAF-A2AA-E63BD48EB4A1"); }
        }
    }
}