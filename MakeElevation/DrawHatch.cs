using System;
using System.Collections.Generic;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Rhino;
using Grasshopper.Kernel.Attributes;
using Rhino.Geometry;
using System.Reflection;
using System.Linq;
using System.Drawing;

namespace CW2D.MakeElevation
{
    public class DrawHatch : GH_Component
    {

        public DrawHatch()
          : base("剖面填充", "剖面填充", "剖面填充",
              Title.CW2D(), Title.Elevation())
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("曲线", "C", "曲线", GH_ParamAccess.list);
            pManager.AddNumberParameter("缩放", "S", "缩放", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("是否烘焙", "B", "是否烘焙剖面线", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("填充", "H", "填充", GH_ParamAccess.list);
        }
        private static bool hatchPatternsLoaded = false;
        private static void EnsureHatchPatternsAreLoaded()
        {
            //Check to make sure the system defaults are in the hatch table
            //If they are not, let's add them back

            if (hatchPatternsLoaded) return; // 如果已经加载过，直接返回


            //Solid
            var hatch_id = RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Solid.Name);
            if (hatch_id == null)
                RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Solid);

            //Hatch1
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Hatch1.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Hatch1);

            //Hatch2
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Hatch2.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Hatch2);

            //Hatch3
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Hatch3.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Hatch3);

            //Dash
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Dash.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Dash);

            //Grid
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Grid.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Grid);

            //Grid60
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Grid60.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Grid60);

            //Plus
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Plus.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Plus);


            //Squares
            hatch_id = Rhino.RhinoDoc.ActiveDoc.HatchPatterns.FindName(HatchPattern.Defaults.Squares.Name);
            if (hatch_id == null)
                Rhino.RhinoDoc.ActiveDoc.HatchPatterns.Add(HatchPattern.Defaults.Squares);
            hatchPatternsLoaded = true; // 标记为已加载

        }


        /// <summary>
        /// Split curves at all mutual intersection points (including overlap interval ends) so that
        /// downstream planar region construction becomes more robust for real-world section/elevation linework.
        /// NOTE: This method is intended to be used STRICTLY within a single "剖面样式" group.
        /// </summary>
        private static List<Curve> SplitCurvesAtIntersections(IEnumerable<Curve> curves, double tol)
        {
            var crvs = curves?.Where(c => c != null).Select(c => c.DuplicateCurve()).ToList() ?? new List<Curve>();
            if (crvs.Count <= 1) return crvs;

            // Collect split parameters per curve index
            var splitParams = new List<List<double>>(crvs.Count);
            for (int i = 0; i < crvs.Count; i++) splitParams.Add(new List<double>());

            for (int i = 0; i < crvs.Count; i++)
            {
                for (int j = i + 1; j < crvs.Count; j++)
                {
                    var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(crvs[i], crvs[j], tol, tol);
                    if (events == null || events.Count == 0) continue;

                    foreach (var ev in events)
                    {
                        if (ev.IsPoint)
                        {
                            splitParams[i].Add(ev.ParameterA);
                            splitParams[j].Add(ev.ParameterB);
                        }
                        else if (ev.IsOverlap)
                        {
                            // Add overlap interval ends as split points (best-effort)
                            splitParams[i].Add(ev.OverlapA.T0);
                            splitParams[i].Add(ev.OverlapA.T1);
                            splitParams[j].Add(ev.OverlapB.T0);
                            splitParams[j].Add(ev.OverlapB.T1);
                        }
                    }
                }
            }

            var pieces = new List<Curve>();
            for (int i = 0; i < crvs.Count; i++)
            {
                var c = crvs[i];
                var ps = splitParams[i];
                if (ps == null || ps.Count == 0)
                {
                    pieces.Add(c);
                    continue;
                }

                // Deduplicate/sort parameters
                var dom = c.Domain;
                double domLen = Math.Abs(dom.T1 - dom.T0);
                double eps = Math.Max(1e-9, domLen * 1e-10);

                var uniq = ps
                    .Where(t => t > dom.T0 + eps && t < dom.T1 - eps) // exclude ends
                    .OrderBy(t => t)
                    .ToList();

                if (uniq.Count == 0)
                {
                    pieces.Add(c);
                    continue;
                }

                // Remove near-duplicates
                var filtered = new List<double>();
                double last = double.NaN;
                foreach (var t in uniq)
                {
                    if (double.IsNaN(last) || Math.Abs(t - last) > eps)
                    {
                        filtered.Add(t);
                        last = t;
                    }
                }

                try
                {
                    var segs = c.Split(filtered);
                    if (segs != null && segs.Length > 0)
                        pieces.AddRange(segs.Where(s => s != null));
                    else
                        pieces.Add(c);
                }
                catch
                {
                    // If split fails for any reason, fall back to the original curve
                    pieces.Add(c);
                }
            }

            return pieces;
        }

        /// <summary>
        /// Normalize hatch key from UserString: trim whitespace. Returns null if empty.
        /// </summary>
        private static string NormalizeHatchKey(string key)
        {
            if (key == null) return null;
            var k = key.Trim();
            return string.IsNullOrWhiteSpace(k) ? null : k;
        }

        /// <summary>
        /// Parse hatch index from normalized key. Returns false if invalid.
        /// </summary>
        private static bool TryParseHatchIndex(string key, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(key)) return false;
            return int.TryParse(key, out index);
        }



        /// <summary>
        /// Normalize "类型" key (component/material type). Trims whitespace; returns null if empty.
        /// </summary>
        private static string NormalizeTypeKey(string key)
        {
            if (key == null) return null;
            var k = key.Trim();
            return string.IsNullOrWhiteSpace(k) ? null : k;
        }

        /// <summary>
        /// Types for which holes (Inner loops) are important and should be preserved (Outer+Inner => leave holes).
        /// </summary>
        private static readonly HashSet<string> HoleSensitiveTypes = new HashSet<string>
{
    "钢材",
    "型材",
    "铝龙骨",
    "钢龙骨",
    "门五金",
    "窗五金",
    "点驳件",
    "转接件",
    "挂件",
    "螺栓",
    "螺钉",
    "拉铆钉",
    "埋件",
    "锚栓"
};

        private static bool IsHoleSensitiveType(string typeKey)
        {
            if (string.IsNullOrWhiteSpace(typeKey)) return false;
            return HoleSensitiveTypes.Contains(typeKey);
        }

        private class HatchGroup
        {
            public int Idx;
            public string StyleKey;

            // All curves in this group share the same "剖面样式".
            // We DO NOT isolate by type because that can break planar region construction
            // when upstream does not assign identical type strings to all boundary segments.
            // Instead, we keep type statistics to decide the hatch-hole strategy.
            public Dictionary<string, int> TypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            public string DominantTypeKey = "未指定";
            public bool UseHoles = false;

            public List<Curve> Curves = new List<Curve>();
        }
        /// <summary>
        /// Stable sort curves to reduce order-sensitivity in downstream ops.
        /// Sort by bbox center (X,Y,Z), then by length.
        /// </summary>
        private static List<Curve> StableSortCurves(IEnumerable<Curve> curves)
        {
            return (curves ?? Enumerable.Empty<Curve>())
                .Where(c => c != null)
                .OrderBy(c => c.GetBoundingBox(true).Center.X)
                .ThenBy(c => c.GetBoundingBox(true).Center.Y)
                .ThenBy(c => c.GetBoundingBox(true).Center.Z)
                .ThenBy(c => c.GetLength())
                .ToList();
        }

        /// <summary>
        /// Deduplicate very common duplicate segments after HLR + splitting.
        /// For planar region construction, duplicated (or reverse-duplicated) linear edges
        /// can cause Brep.CreatePlanarBreps to miss valid regions.
        ///
        /// Strategy:
        /// - Remove nulls
        /// - Remove extremely short segments
        /// - For linear curves (lines/polylines segments), key by quantized endpoints (order-independent)
        /// - Keep one representative per key
        /// - Non-linear curves are kept as-is
        /// </summary>
        private static List<Curve> DeduplicateLinearSegments(IEnumerable<Curve> curves, double tol)
        {
            var list = (curves ?? Enumerable.Empty<Curve>()).Where(c => c != null).ToList();
            if (list.Count == 0) return list;

            double minLen = Math.Max(tol * 0.5, 1e-9);
            double q = Math.Max(tol * 0.5, 1e-9); // quantization step

            long Q(double v) => (long)Math.Round(v / q);

            bool LexLessOrEqual(
                (long, long, long, long, long, long) k1,
                (long, long, long, long, long, long) k2)
            {
                if (k1.Item1 != k2.Item1) return k1.Item1 < k2.Item1;
                if (k1.Item2 != k2.Item2) return k1.Item2 < k2.Item2;
                if (k1.Item3 != k2.Item3) return k1.Item3 < k2.Item3;
                if (k1.Item4 != k2.Item4) return k1.Item4 < k2.Item4;
                if (k1.Item5 != k2.Item5) return k1.Item5 < k2.Item5;
                if (k1.Item6 != k2.Item6) return k1.Item6 < k2.Item6;
                return true;
            }

            // key: (ax,ay,az,bx,by,bz) with (a<=b) lexicographically
            var seen = new Dictionary<(long, long, long, long, long, long), Curve>();
            var outList = new List<Curve>();

            foreach (var c in list)
            {
                try
                {
                    if (c.GetLength() < minLen) continue;

                    // Only dedupe strictly linear curves; keep others to avoid false merges.
                    if (!c.IsLinear(tol))
                    {
                        outList.Add(c);
                        continue;
                    }

                    var a = c.PointAtStart;
                    var b = c.PointAtEnd;

                    var ax = Q(a.X); var ay = Q(a.Y); var az = Q(a.Z);
                    var bx = Q(b.X); var by = Q(b.Y); var bz = Q(b.Z);

                    // order-independent
                    var key1 = (ax, ay, az, bx, by, bz);
                    var key2 = (bx, by, bz, ax, ay, az);
                    var key = LexLessOrEqual(key1, key2) ? key1 : key2;

                    if (!seen.ContainsKey(key))
                    {
                        seen[key] = c;
                        outList.Add(c);
                    }
                }
                catch
                {
                    // On any failure, keep the curve.
                    outList.Add(c);
                }
            }

            return outList;
        }

        /// <summary>
        /// Compute area magnitude for a closed planar curve. Returns 0 if cannot compute.
        /// </summary>
        private static double CurveAreaMagnitude(Curve c)
        {
            if (c == null || !c.IsClosed) return 0.0;
            try
            {
                var amp = AreaMassProperties.Compute(c);
                if (amp == null) return 0.0;
                return Math.Abs(amp.Area);
            }
            catch { return 0.0; }
        }

        /// <summary>
        /// Sort boundaries so that the largest-area loop is first (outer), and the rest follow (inners).
        /// This improves hatch stability for holes.
        /// </summary>
        private static List<Curve> SortBoundariesOuterFirst(List<Curve> boundaries)
        {
            if (boundaries == null || boundaries.Count <= 1) return boundaries ?? new List<Curve>();
            return boundaries
                .Where(b => b != null && b.IsClosed)
                .OrderByDescending(b => CurveAreaMagnitude(b))
                .ToList();
        }

        /// <summary>
        /// Ensure a layer exists, return its index; -1 if failed.
        /// </summary>
        private static int EnsureLayer(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return -1;
            var idx = doc.Layers.FindByFullPath(name, -1);
            if (idx >= 0) return idx;
            var layer = new Layer { Name = name };
            return doc.Layers.Add(layer);
        }



        protected override void SolveInstance(IGH_DataAccess DA)
        {
            EnsureHatchPatternsAreLoaded();

            var geos = new List<GeometryBase>();
            var scale = new double();
            var flag = new bool();

            if (!DA.GetDataList(0, geos)) return;
            if (!DA.GetData(1, ref scale)) return;
            if (!DA.GetData(2, ref flag)) return;

            var results = new List<Hatch>();
            var tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // -----------------------------
            // Strict group isolation:
            // - normalize key
            // - group only by the normalized "剖面样式"
            // - all intersection/split/join/planar-brep happen INSIDE each group only
            // -----------------------------
            var dict = new Dictionary<string, HatchGroup>();


            foreach (var geo in geos)
            {
                if (geo is Curve curve)
                {
                    var crv = curve.DuplicateCurve();

                    // Style key
                    var rawStyle = crv.GetUserString("剖面样式");
                    var styleKey = NormalizeHatchKey(rawStyle);
                    if (styleKey == null) continue;

                    if (!TryParseHatchIndex(styleKey, out int styleIdx))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"无效的剖面样式值: '{rawStyle}'. 需要为整数（例如 7）。该曲线已跳过。");
                        continue;
                    }

                    // Type key (provided by upstream component)
                    var rawType = crv.GetUserString("类型");
                    var typeKey = NormalizeTypeKey(rawType) ?? "未指定";

                    // Strict isolation: group by normalized "剖面样式" only (styles do not interfere)
                    var groupKey = styleKey;

                    if (!dict.TryGetValue(groupKey, out var grp))
                    {
                        grp = new HatchGroup
                        {
                            Idx = styleIdx,
                            StyleKey = styleKey
                        };
                        dict[groupKey] = grp;
                    }

                    grp.Curves.Add(crv);

                    // Track type distribution for this style group (after normalization)
                    if (!grp.TypeCounts.TryGetValue(typeKey, out int ct))
                        grp.TypeCounts[typeKey] = 1;
                    else
                        grp.TypeCounts[typeKey] = ct + 1;
                }
            }


            // Decide dominant type per style group, then stable processing order by hatch index
            foreach (var g in dict.Values)
            {
                // Exclude "未指定" when we have any specified types
                var specified = g.TypeCounts
                    .Where(kv => kv.Key != "未指定")
                    .OrderByDescending(kv => kv.Value)
                    .ToList();

                if (specified.Count > 0)
                {
                    g.DominantTypeKey = specified[0].Key;

                    if (specified.Select(kv => kv.Key).Distinct().Count() > 1)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                            $"同一剖面样式 '{g.StyleKey}' 发现多种类型：{string.Join(", ", specified.Select(kv => kv.Key))}。将使用出现次数最多的类型 '{g.DominantTypeKey}' 决定是否留洞。");
                    }
                }
                else
                {
                    g.DominantTypeKey = "未指定";
                }

                g.UseHoles = IsHoleSensitiveType(g.DominantTypeKey);
            }

            var orderedGroups = dict.Values
                .OrderBy(g => g.Idx)
                .ThenBy(g => g.StyleKey, StringComparer.Ordinal)
                .ToList();

            foreach (var grp in orderedGroups)
            {
                var index = grp.Idx;

                // Group-internal stable ordering to reduce order sensitivity
                var groupCurves = StableSortCurves(grp.Curves);
                if (groupCurves.Count == 0) continue;

                var joinTol = tol * 2.0;

                // --- Strict: split only inside this group ---
                var splitPieces = SplitCurvesAtIntersections(groupCurves, tol);
                splitPieces = StableSortCurves(splitPieces);

                // IMPORTANT: For real-world HLR output (especially grid-like linework),
                // joining edges can inadvertently remove internal boundaries and cause
                // Brep.CreatePlanarBreps to miss valid regions. Therefore we prefer using
                // the split *segments* directly (after de-duplication) to build planar regions.
                var regionCurves = DeduplicateLinearSegments(splitPieces, tol);
                regionCurves = StableSortCurves(regionCurves);

                // Simplify curves, remove numeric noise
                foreach (var c in regionCurves)
                {
                    c?.Simplify(CurveSimplifyOptions.All, tol, tol);
                }

                // Build planar regions ONLY from curves inside this group
                Brep[] breps = Brep.CreatePlanarBreps(regionCurves, tol);

                // Fallback: if segment-based region creation fails, try joining as a last resort
                if (breps == null || breps.Length == 0)
                {
                    Curve[] joined = Curve.JoinCurves(regionCurves, joinTol);
                    if (joined != null && joined.Length > 0)
                    {
                        foreach (var c in joined) c?.Simplify(CurveSimplifyOptions.All, tol, tol);
                        breps = Brep.CreatePlanarBreps(joined, tol);
                    }
                }

                if (breps == null || breps.Length == 0)
                    continue;

                // Filter tiny faces
                var minArea = tol * tol * 10.0;

                foreach (var b in breps)
                {
                    var amp = AreaMassProperties.Compute(b);
                    if (amp != null && amp.Area < minArea)
                        continue;


                    // Face-based hatch creation. All groups are processed per-face; only loop combination differs by type.
                    foreach (var face in b.Faces)
                    {
                        if (face == null) continue;

                        if (grp.UseHoles)
                        {
                            // Hole-sensitive types: Outer + Inner => leave holes
                            var outers = new List<Curve>();
                            var inners = new List<Curve>();

                            foreach (var loop in face.Loops)
                            {
                                var loopCrv = loop.To3dCurve();
                                if (loopCrv == null || !loopCrv.IsClosed) continue;

                                if (loop.LoopType == BrepLoopType.Outer)
                                    outers.Add(loopCrv);
                                else if (loop.LoopType == BrepLoopType.Inner)
                                    inners.Add(loopCrv);
                                else
                                    outers.Add(loopCrv); // fallback
                            }

                            // Stable ordering
                            outers = outers.OrderByDescending(CurveAreaMagnitude).ToList();
                            inners = inners.OrderByDescending(CurveAreaMagnitude).ToList();

                            if (outers.Count == 0)
                            {
                                // Fallback: fill each closed loop individually
                                foreach (var c in inners)
                                {
                                    var hs0 = Hatch.Create(new List<Curve> { c }, index, 0.0, scale, tol);
                                    if (hs0 != null && hs0.Length > 0)
                                        results.AddRange(hs0);
                                }
                            }
                            else
                            {
                                foreach (var outer in outers)
                                {
                                    var boundaries = new List<Curve> { outer };
                                    boundaries.AddRange(inners);

                                    var hs = Hatch.Create(boundaries, index, 0.0, scale, tol);
                                    if (hs != null && hs.Length > 0)
                                        results.AddRange(hs);
                                }
                            }
                        }
                        else
                        {
                            // Non-hole-sensitive types: "closed => fill" (do NOT treat inner loops as holes)
                            foreach (var loop in face.Loops)
                            {
                                var loopCrv = loop.To3dCurve();
                                if (loopCrv == null || !loopCrv.IsClosed) continue;

                                var hs = Hatch.Create(new List<Curve> { loopCrv }, index, 0.0, scale, tol);
                                if (hs != null && hs.Length > 0)
                                    results.AddRange(hs);
                            }
                        }
                    }

                }
            }

            if (flag)
            {
                // Bake per-style into separate layers to reduce "visual interference" (optional, does not affect geometry isolation)
                foreach (var hatch in results)
                {
                    try
                    {
                        var layerName = $"Hatch_{hatch.PatternIndex}";
                        var layerIndex = EnsureLayer(layerName);

                        if (layerIndex >= 0)
                        {
                            var attr = new ObjectAttributes { LayerIndex = layerIndex };
                            RhinoDoc.ActiveDoc.Objects.AddHatch(hatch, attr);
                        }
                        else
                        {
                            RhinoDoc.ActiveDoc.Objects.AddHatch(hatch);
                        }
                    }
                    catch
                    {
                        RhinoDoc.ActiveDoc.Objects.AddHatch(hatch);
                    }
                }
                RhinoDoc.ActiveDoc.Views.Redraw();
            }

            DA.SetDataList(0, results);



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
                    var resourceName = "CW2D.Resources.Material Properties.png";

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

        public override Guid ComponentGuid => new Guid("3A6AF98C-34F3-4034-89F3-C61F4E9E9B5B");
    }
}