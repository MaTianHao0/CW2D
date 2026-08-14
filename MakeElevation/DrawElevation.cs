using CW2D.Attributes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Grasshopper.Rhinoceros.Model;
using Microsoft.Office.Interop.Excel;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace CW2D.Elevation
{
    public class DrawElevation : GH_Component
    {
        public DrawElevation() : base("立面图", "立面图", "生成立面图框架", Title.CW2D(), Title.Elevation())
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("幕墙", "幕墙", "幕墙", GH_ParamAccess.list);
            pManager.AddVectorParameter("方向", "方向", "方向", GH_ParamAccess.item, Vector3d.YAxis);
            pManager.AddVectorParameter("上方向", "上方向", "上方向", GH_ParamAccess.item, Vector3d.ZAxis);
            pManager.AddPointParameter("基准点", "基准点", "基准点", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddPlaneParameter("裁剪平面", "裁剪平面", "裁剪平面", GH_ParamAccess.list);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("可见线", "可见线", "可见线", GH_ParamAccess.list);
            pManager.AddCurveParameter("隐藏线", "隐藏线", "隐藏线", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var attrDatas = new List<GH_AttributeData>();
            var direction = Vector3d.YAxis;
            var updirection = Vector3d.ZAxis;
            var basePoint = Point3d.Origin;
            var cuttingPlanes = new List<Plane>();

            if (!DA.GetDataList(0, attrDatas)) return;
            if (!DA.GetData(1, ref direction)) return;
            if (!DA.GetData(2, ref updirection)) return;
            if (!DA.GetData(3, ref basePoint)) return;
            DA.GetDataList(4, cuttingPlanes);

            //获取结果并输出
            var results1 = new List<GeometryBase>();
            var results2 = new List<GeometryBase>();

            if (attrDatas.Count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "没有输入幕墙");
                return;
            }

            int num = 0;
            var parameters = SetParameters(attrDatas, direction, updirection, cuttingPlanes);//调用SetParameters方法，返回HiddenLineDrawingParameters对象
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
                        curve.SetUserString(item.Key, item.Value); //属性赋值或属性获取

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

            var bbox0 = BoundingBox.Empty;
            foreach (var geometry in results1)
                bbox0.Union(geometry.GetBoundingBox(false));
            foreach (var geometry in results2)
                bbox0.Union(geometry.GetBoundingBox(false));
            var movedResults1 = Move(results1, basePoint - bbox0.Center);
            var movedResults2 = Move(results2, basePoint - bbox0.Center);

            DA.SetDataList(0, movedResults1);
            DA.SetDataList(1, movedResults2);
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

        //静态方法调用
        //private关键字表示这个方法只能在当前类内部被调用。它接收attrDatas、floor、ceiling作为参数，并返回一个HiddenLineDrawingParameters对象。
        //设置HLDP参数
        //这个方法专门负责为HiddenLineDrawing.Compute方法准备参数
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

            //视锥体(Frustum)
            //设置前4个参数定义视图的"窗口"（X和Z方向）
            //后2个参数定义近 / 远裁剪面距离（沿Y轴）  

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
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }
        public override Guid ComponentGuid => new Guid("{F89AE70D-6EE4-412C-B4B2-7923EEDE496E}");
    }
}