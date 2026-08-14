using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Rhinoceros.Model;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Reflection;

namespace CW2D.Others
{
    internal class Projection
    {
        internal double Tolerance { get; set; } = 0.001;
        internal bool TangentEdge { get; set; } = true;
        internal bool TangentSeam { get; set; } = true;
        internal ViewportInfo Viewport { get; set; } = new ViewportInfo();
        internal List<Plane> ClippingPlanes { get; } = new List<Plane>();
        internal List<Curve> VisibleCurve { get; } = new List<Curve>();
        internal List<Curve> HiddenCurve { get; } = new List<Curve>();
        private HiddenLineDrawingParameters Parameters => SetParameters();

        public Projection(Projection other)
        {
            Tolerance = other.Tolerance; 
            TangentEdge = other.TangentEdge;
            TangentSeam = other.TangentSeam;
            Viewport = other.Viewport;
            ClippingPlanes = new List<Plane>(other.ClippingPlanes);
            VisibleCurve = new List<Curve>(other.VisibleCurve);
            HiddenCurve = new List<Curve>(other.HiddenCurve);
        }

        public void SetViewport(ViewportInfo viewport)
        {
            Viewport = viewport;
        }

        public void SetViewport(Rectangle3d rectangle)
        {
            SetViewport(rectangle.Center, rectangle.Plane.ZAxis, rectangle.Plane.YAxis,
                -0.5 * rectangle.Width, 0.5 * rectangle.Width, -0.5 * rectangle.Height, 0.5 * rectangle.Height,
                0.001, 0.5 * rectangle.Circumference);
        }

        public void SetViewport(Point3d location, Vector3d direction, Vector3d up, double left, double right, double bottom, double top, double near, double far)
        {
            Viewport.ChangeToParallelProjection(true);
            Viewport.SetCameraLocation(location);
            Viewport.SetCameraDirection(direction);
            Viewport.SetCameraUp(up);
            Viewport.SetFrustum(left, right, bottom, top, near, far);
            Viewport.SetScreenPort(0, 600, 0, 400, 0, 100);
        }

        public void AddClippingPlane(Plane plane)
        {
            ClippingPlanes.Add(plane);
        }

        public void AddClippingPlane(List<Plane> planes)
        {
            ClippingPlanes.AddRange(planes);
        }

        public int AddGeometry(IGH_GeometricGoo goo, Transform xform, object tag)
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
                            if (geometry != null) num += AddGeometry(geometry, xform1, tag);
                        }
                    }
                    return num;
                case GH_GeometryGroup ghGeometryGroup:
                    foreach (IGH_GeometricGoo ghGeometricGoo in ghGeometryGroup.Objects)
                    {
                        if (ghGeometricGoo != null)
                        {
                            IGH_GeometricGoo goo1 = ghGeometricGoo;
                            num += AddGeometry(goo1, xform, tag);
                        }
                    }
                    return num;
                default:
                    GeometryBase geometry1 = goo is GH_Extrusion ghExtrusion ? ghExtrusion.Value.ToBrep() : goo is GH_SubD ghSubD ? ghSubD.Value.ToBrep() : GH_Convert.ToGeometryBase(goo);
                    if (geometry1 == null) return 0;
                    if (Parameters.AddGeometry(geometry1, xform, tag, true)) return 1;
                    return 0;
            }
        }

        private HiddenLineDrawingParameters SetParameters()
        {
            var parameters = new HiddenLineDrawingParameters();
            parameters.Flatten = true;
            parameters.AbsoluteTolerance = Tolerance;
            parameters.IncludeTangentEdges = TangentEdge;
            parameters.IncludeTangentSeams = TangentSeam;
            parameters.SetViewport(Viewport);
            foreach (var plane in ClippingPlanes)
                parameters.AddClippingPlane(plane);
            return parameters;
        }

        public void Calculate()
        {
            var hiddenLineDrawing = HiddenLineDrawing.Compute(Parameters, true);
            if (hiddenLineDrawing == null) return;
            foreach (HiddenLineDrawingSegment segment in hiddenLineDrawing.Segments)
            {
                var visibility = segment.SegmentVisibility;
                if (segment.Index < 0 || visibility == HiddenLineDrawingSegment.Visibility.Unset) continue;
                var curve = segment.CurveGeometry.DuplicateCurve();

                var source = segment.ParentCurve.SourceObject;
                if (source.Tag is Dictionary<string, string> dictionary)
                    foreach (var item in dictionary) curve.SetUserString(item.Key, item.Value);

                switch (visibility)
                {
                    case HiddenLineDrawingSegment.Visibility.Visible:
                    case HiddenLineDrawingSegment.Visibility.Clipped:
                        VisibleCurve.Add(curve);
                        continue;
                    case HiddenLineDrawingSegment.Visibility.Hidden:
                        HiddenCurve.Add(curve);
                        continue;
                    default:
                        continue;
                }
            }
        }

        public List<Curve> Move(Vector3d motion)
        {
            var result = new List<Curve>();
            foreach (var curve in VisibleCurve)
            {
                var xform = Transform.Translation(motion);
                var movedCurve = curve.DuplicateCurve();
                movedCurve.Transform(xform);
                result.Add(movedCurve);
            }
            return result;
        }

        public List<Curve> Move(Point3d target)
        {
            var bbox = BoundingBox.Empty;
            foreach (var curve in VisibleCurve)
                bbox.Union(curve.GetBoundingBox(false));
            var basePoint = bbox.Center;
            return Move(target - basePoint);
        }

    }
}