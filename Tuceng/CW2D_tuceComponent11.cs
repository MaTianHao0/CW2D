using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special; // GH_Panel
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.Specialized; // NameValueCollection
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms; // ToolStripMenuItem
using SD = System.Drawing; // alias to avoid Point ambiguity
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace CW2D_tuceng
{
    /// <summary>
    /// Layer & Geometry Info exporter with:
    /// 1) Extrusion → Brep conversion before output/measures/positions
    /// 2) Optional Block (InstanceObject) explode with full transform + recursion
    /// 3) Context menu: create/connect a Panel for "Layer Name", and fill it with doc layers
    /// </summary>
    public class LayerAndGeometryInfo : GH_Component
    {
        public LayerAndGeometryInfo()
          : base(
              name: "图层信息",
              nickname: "图层几何",
              description: "Read geometry/attributes by layer; converts Extrusion→Brep; optional block explode. 右键菜单可创建/连接一个 Panel 作为“层名”输入，并写入当前文档图层列表。",
              category: "CW2D",
              subCategory: "输入输出")
        { }

        // ---------- Inputs ----------
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddBooleanParameter("运行", "运行", "Set to true to execute. Default false prevents auto-run.", GH_ParamAccess.item, false);
            p.AddTextParameter("层名", "层名", "Target layer(s). Use 'all' to scan all layers. Multiple layers can be set (comma or newline).", GH_ParamAccess.item, string.Empty);
            p.AddBooleanParameter("子层", "子层", "If true, include all descendants of the specified layer.", GH_ParamAccess.item, true);
            p.AddBooleanParameter("几何", "几何", "If true, output geometry + bounding boxes.", GH_ParamAccess.item, true);
            p.AddBooleanParameter("属性", "属性", "If true, output object/layer attributes and user strings.", GH_ParamAccess.item, true);
            p.AddBooleanParameter("炸块", "炸块", "If true, expand InstanceObjects (blocks) recursively and apply instance transforms.", GH_ParamAccess.item, false);
        }

        // ---------- Outputs ----------
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGeometryParameter("几何", "几何", "Output geometry (Extrusion converted to Brep; blocks expanded if enabled).", GH_ParamAccess.list);
            p.AddPointParameter("点位", "点位", "Representative position per object (volume/area centroid; fallback bbox center).", GH_ParamAccess.list);
            p.AddBoxParameter("包围盒", "包围盒", "World-aligned bounding boxes.", GH_ParamAccess.list);
            p.AddTextParameter("信息", "信息", "Per-object key=value; summary string (Id, Type, Name, LayerFullPath, colors, linetype, user strings, measures).", GH_ParamAccess.list);
            p.AddIntegerParameter("数量", "数量", "Total processed objects (including expanded children).", GH_ParamAccess.item);
            p.AddTextParameter("汇总", "汇总", "Per-layer human-readable summary.", GH_ParamAccess.item);
            p.AddTextParameter("层表", "层表", "Data tree describing each layer (Index, Name, FullPath, Color, Visible, Locked, ParentIndex, ObjectCount).", GH_ParamAccess.tree);
        }

        public override Guid ComponentGuid => new Guid("f3a1a2f0-8ac7-4c8b-9a35-9f5a40f0b6a1");
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
                    var resourceName = "CW2D.Resources.lay info.png";

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
        // ===================== 右键菜单：Panel 交互 =====================
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var miCreateConnect = new ToolStripMenuItem("创建/连接 层名 Panel（若不存在）");
            miCreateConnect.Click += (s, e) =>
            {
                var panel = GetOrCreateLayerPanel(connectIfCreated: true);
                if (panel == null)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "无法创建或获取 Panel。");
            };
            menu.Items.Add(miCreateConnect);

            var miAll = new ToolStripMenuItem("写入：所有图层（全路径，每行一个）");
            miAll.Click += (s, e) => WriteLayersToPanel(includeOnlyVisible: false);
            menu.Items.Add(miAll);

            var miVisible = new ToolStripMenuItem("写入：仅可见图层（全路径，每行一个）");
            miVisible.Click += (s, e) => WriteLayersToPanel(includeOnlyVisible: true);
            menu.Items.Add(miVisible);
        }

        /// <summary>
        /// 查找已连接到“层名”输入的 GH_Panel，如果没有则创建一个并自动连接。
        /// </summary>
        private GH_Panel GetOrCreateLayerPanel(bool connectIfCreated)
        {
            if (Params.Input.Count < 2) return null;
            var targetInput = Params.Input[1]; // "层名"

            // 1) 先找已连接的 Panel（Sources 里直接就是 IGH_Param；Panel 自身就是一个 Param）
            foreach (var src in targetInput.Sources)
            {
                if (src.Attributes?.GetTopLevel.DocObject is GH_Panel p1) return p1;
            }

            // 2) 再找画布上最近的 Panel（未连接）
            var doc = OnPingDocument();
            if (doc != null)
            {
                GH_Panel nearest = null;
                double best = double.MaxValue;
                foreach (var obj in doc.Objects)
                {
                    if (obj is GH_Panel p)
                    {
                        var d = (p.Attributes?.Bounds.Location ?? new SD.PointF(0, 0));
                        var c = (this.Attributes?.Bounds.Location ?? new SD.PointF(0, 0));
                        var dx = d.X - c.X; var dy = d.Y - c.Y;
                        var dist2 = dx * dx + dy * dy;
                        if (dist2 < best) { best = dist2; nearest = p; }
                    }
                }
                if (nearest != null)
                {
                    // 若有最近 Panel 且未连接，尝试连接（直接用 panel 作为源）
                    if (!targetInput.Sources.Contains(nearest))
                    {
                        targetInput.AddSource(nearest);
                        this.Params.OnParametersChanged();
                        this.ExpireSolution(true);
                    }
                    return nearest;
                }
            }

            // 3) 都没有的话，新建 Panel 并可选连接
            return CreateAndMaybeConnectPanel(connectIfCreated);
        }

        private GH_Panel CreateAndMaybeConnectPanel(bool connect)
        {
            var ghdoc = OnPingDocument();
            if (ghdoc == null) return null;

            var panel = new GH_Panel
            {
                NickName = "层名",
                UserText = "", // 初始为空，后续写入
            };
            // 放在组件左侧
            var compBounds = this.Attributes?.Bounds ?? new SD.RectangleF(100, 100, 80, 40);
            panel.Attributes.Pivot = new SD.PointF(compBounds.Left - 200, compBounds.Top + 10);

            ghdoc.AddObject(panel, false);

            if (connect && Params.Input.Count > 1)
            {
                var inParam = Params.Input[1]; // 层名
                inParam.AddSource(panel);      // 直接把 panel 作为源
                Params.OnParametersChanged();
                ExpireSolution(true);
            }
            return panel;
        }

        private void WriteLayersToPanel(bool includeOnlyVisible)
        {
            var panel = GetOrCreateLayerPanel(connectIfCreated: true);
            if (panel == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "没有可用的 Panel。");
                return;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "无活动 Rhino 文档。");
                return;
            }

            IEnumerable<Layer> q = doc.Layers.Where(l => !l.IsDeleted);
            if (includeOnlyVisible) q = q.Where(l => l.IsVisible);

            // 使用 FullPath；若为空回退 Name
            var lines = q.Select(l => string.IsNullOrEmpty(l.FullPath) ? l.Name : l.FullPath)
                         .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

            // 每行一个，便于 Panel 中逐行编辑；SolveInstance 里兼容逗号/换行
            panel.UserText = string.Join(System.Environment.NewLine, lines);

            // 触发 Panel 更新与重算
            panel.ExpireSolution(true);
            ExpireSolution(true);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            string layerName = string.Empty;
            bool includeSublayers = true, includeGeometry = true, includeAttributes = true, explodeBlocks = false;

            DA.GetData(0, ref run);
            if (!run)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Run=false. Set Run to true to execute.");
                return;
            }

            // “层名”输入允许来自 Panel 的多行：按换行/逗号分割并重组为搜索关键词
            string raw = string.Empty;
            if (!DA.GetData(1, ref raw)) raw = string.Empty;
            layerName = (raw ?? string.Empty).Trim();

            DA.GetData(2, ref includeSublayers);
            DA.GetData(3, ref includeGeometry);
            DA.GetData(4, ref includeAttributes);
            DA.GetData(5, ref explodeBlocks);

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document.");
                return;
            }

            // Panel 多行 / 逗号输入兼容：如果包含换行或逗号，视为多层；否则按单一关键字处理
            List<string> requestedNames = new List<string>();
            if (layerName.Length > 0)
            {
                var parts = layerName.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .Where(s => s.Length > 0)
                                     .ToList();
                if (parts.Count > 0) requestedNames = parts;
            }

            // Resolve target layers
            var layersToProcess = (requestedNames.Count == 0)
                ? ResolveLayers(doc, layerName, includeSublayers) // 兼容原逻辑，如空串或 "all"
                : ResolveLayersBatch(doc, requestedNames, includeSublayers);

            if (layersToProcess.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No matching layers.");
                return;
            }
            var parallelResults = new System.Collections.Concurrent.ConcurrentBag<LayerThreadResult>();
            //设置并发度
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, System.Environment.ProcessorCount - 1)
            };

            Parallel.ForEach(layersToProcess, parallelOptions, (layer, state, i) =>
            {
                var localRes = new LayerThreadResult();
                localRes.SortIndex = (int)i;
                localRes.Branch = new GH_Path((int)i);

                localRes.LayerTreeInfo.Add(new GH_String($"Layer.Index={layer.Index}"));
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.Name={layer.Name}"));
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.FullPath={SafeFullPath(layer)}"));
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.Color={SD.ColorTranslator.ToHtml(layer.Color)}"));
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.Visible={layer.IsVisible}"));
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.Locked={layer.IsLocked}"));
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.ParentId={layer.ParentLayerId}"));

                var objs = doc.Objects.FindByLayer(layer);
                if(objs!= null)
                {
                    foreach( var obj in objs ) {
                        ProcessOneRhinoObject(doc, obj, includeGeometry, includeAttributes, explodeBlocks,
                                              localRes.geomOut, localRes.posOut, localRes.Bboxes, localRes.infoOut,
                                              ref localRes.ObjectCount, parentInstanceInfo: null);
                    }
                }
                localRes.SummaryText = $"Layer '{SafeFullPath(layer)}': objects={localRes.ObjectCount}, color={SD.ColorTranslator.ToHtml(layer.Color)}, visible={layer.IsVisible}, locked={layer.IsLocked}";
                localRes.LayerTreeInfo.Add(new GH_String($"Layer.ObjectCount={localRes.ObjectCount}"));

                parallelResults.Add(localRes);

            }
            );

            int totalCount = parallelResults.Sum(r=>r.ObjectCount);
            var finalGeom = new List<GeometryBase>(totalCount);
            var finalPos=new List<Point3d>(totalCount);
            var finalBbox=new List<Box>(totalCount);
            var finalInfo=new List<string>(totalCount);
            var finalSummary = new StringBuilder();
            var finalLayerTable = new GH_Structure<GH_String>();

            var sortedResults=parallelResults.OrderBy(r=>r.SortIndex).ToList();

            foreach (var res in sortedResults)
            {
                finalGeom.AddRange(res.geomOut);
                finalPos.AddRange(res.posOut);
                finalBbox.AddRange(res.Bboxes);
                finalInfo.AddRange(res.infoOut);

                finalSummary.AppendLine(res.SummaryText);

                finalLayerTable.AppendRange(res.LayerTreeInfo, res.Branch);
            }
            DA.SetDataList(0, finalGeom);
            DA.SetDataList(1, finalPos);
            DA.SetDataList(2, finalBbox);
            DA.SetDataList(3, finalInfo);
            DA.SetData(4, totalCount);
            DA.SetData(5, finalSummary.ToString());
            DA.SetDataTree(6, finalLayerTable);
        }

        // ---- Core per-object processing (handles blocks, extrusion→brep, measures, info) ----
        private void ProcessOneRhinoObject(
            RhinoDoc doc,
            RhinoObject obj,
            bool includeGeometry,
            bool includeAttributes,
            bool explodeBlocks,
            List<GeometryBase> geomOut,
            List<Point3d> posOut,
            List<Box> bboxOut,
            List<string> infoOut,
            ref int layerCount,
            InstanceContext parentInstanceInfo)
        {
            if (obj == null) return;

            // 1) Block instance handling
            if (obj.ObjectType == ObjectType.InstanceReference && obj is InstanceObject inst)
            {
                if (!explodeBlocks)
                {
                    // Instance-level accurate output without expanding children
                    var info = BuildInfoString(doc, obj, null, parentInstanceInfo, instanceOnly: true);
                    if (includeGeometry)
                    {
                        var bbox = obj.Geometry.GetBoundingBox(true);
                        var dupInst = obj.Geometry?.Duplicate();
                        if (dupInst != null) geomOut.Add(dupInst);
                        bboxOut.Add(new Box(Plane.WorldXY, bbox));
                        posOut.Add(bbox.Center);
                    }
                    else
                    {
                        var bbox = obj.Geometry.GetBoundingBox(true);
                        bboxOut.Add(new Box(Plane.WorldXY, bbox));
                        posOut.Add(bbox.Center);
                    }
                    infoOut.Add(info);
                    layerCount++;
                    return;
                }

                var idef = inst.InstanceDefinition;
                if (idef == null) return;
                var xform = inst.InstanceXform;

                var nextCtx = new InstanceContext
                {
                    InstanceLayerIndex = obj.Attributes.LayerIndex,
                    InstanceLayerFullPath = SafeFullPath(doc.Layers[obj.Attributes.LayerIndex]),
                    InstanceGuid = obj.Id,
                    InstanceName = idef.Name,
                    AccumulatedTransform = parentInstanceInfo?.AccumulatedTransform != null
                        ? parentInstanceInfo.AccumulatedTransform * xform
                        : xform
                };

                foreach (var idefObj in idef.GetObjects())
                {
                    if (idefObj == null) continue;
                    ProcessIdefChild(doc, idefObj, nextCtx, includeGeometry, includeAttributes,
                                     geomOut, posOut, bboxOut, infoOut, ref layerCount);
                }
                return;
            }

            // 2) Regular object
            var g = obj.Geometry;
            if (g == null)
            {
                if (includeAttributes)
                    infoOut.Add(BuildInfoString(doc, obj, null, parentInstanceInfo));
                return;
            }

            GeometryBase work = ConvertExtrusionToBrepIfNeeded(g);
            GeometryBase forOutput = includeGeometry ? work.Duplicate() : work;

            if (includeGeometry)
            {
                geomOut.Add(forOutput);
            }

            var bboxG = forOutput.GetBoundingBox(true);
            bboxOut.Add(new Box(Plane.WorldXY, bboxG));
            posOut.Add(ComputePosition(forOutput));

            if (includeAttributes)
            {
                var sbInfo = BuildInfoString(doc, obj, work, parentInstanceInfo);
                infoOut.Add(sbInfo);
            }

            layerCount++;
        }

        private GeometryBase ConvertExtrusionToBrepIfNeeded(GeometryBase g)
        {
            if (g is Extrusion ex)
            {
                var brepFromEx = ex.ToBrep(true);
                if (brepFromEx != null) return brepFromEx;
            }
            return g;
        }

        private Point3d ComputePosition(GeometryBase g)
        {
            try
            {
                switch (g)
                {
                    case Extrusion ex:
                        {
                            var exBrep = ex.ToBrep(true);
                            if (exBrep != null)
                            {
                                if (exBrep.IsSolid)
                                {
                                    var vmp = VolumeMassProperties.Compute(exBrep);
                                    if (vmp != null) return vmp.Centroid;
                                }
                                var amp = AreaMassProperties.Compute(exBrep);
                                if (amp != null) return amp.Centroid;
                            }
                            break;
                        }
                    case Brep brepG:
                        {
                            if (brepG.IsSolid)
                            {
                                var v = VolumeMassProperties.Compute(brepG);
                                if (v != null) return v.Centroid;
                            }
                            var a2 = AreaMassProperties.Compute(brepG);
                            if (a2 != null) return a2.Centroid;
                            break;
                        }
                    case Surface srf:
                        {
                            var a1 = AreaMassProperties.Compute(srf);
                            if (a1 != null) return a1.Centroid;
                            break;
                        }
                    case Mesh mesh:
                        {
                            var am = AreaMassProperties.Compute(mesh);
                            if (am != null) return am.Centroid;
                            break;
                        }
                    case Curve crv:
                        {
                            crv.Domain = new Interval(0.0, 1.0);
                            return crv.PointAtNormalizedLength(0.5);
                        }
                    case Rhino.Geometry.Point pt:
                        return pt.Location;

                    case PointCloud pc:
                        return pc.GetBoundingBox(true).Center;
                }
            }
            catch { }

            return g.GetBoundingBox(true).Center;
        }

        private string SafeFullPath(Layer layer)
        {
            try
            {
                return layer.FullPath ?? layer.Name;
            }
            catch
            {
                return layer?.Name ?? string.Empty;
            }
        }

        private List<Layer> ResolveLayers(RhinoDoc doc, string layerName, bool includeSublayers)
        {
            var result = new List<Layer>();
            var all = doc.Layers;
            layerName = (layerName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(layerName) || string.Equals(layerName, "all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var lyr in all)
                {
                    if (!lyr.IsDeleted) result.Add(lyr);
                }
                return result;
            }

            foreach (var lyr in all)
            {
                if (lyr.IsDeleted) continue;
                if (string.Equals(lyr.Name, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(lyr);
                    if (includeSublayers)
                        result.AddRange(GetDescendants(all, lyr));
                }
            }
            return result.Distinct().ToList();
        }

        private List<Layer> ResolveLayersBatch(RhinoDoc doc, List<string> names, bool includeSublayers)
        {
            var set = new HashSet<Layer>();
            foreach (var name in names)
            {
                var ls = ResolveLayers(doc, name, includeSublayers);
                foreach (var l in ls) set.Add(l);
            }
            return set.ToList();
        }

        private List<Layer> GetDescendants(Rhino.DocObjects.Tables.LayerTable table, Layer parent)
        {
            var list = new List<Layer>();
            var queue = new Queue<Layer>();
            queue.Enqueue(parent);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var layer in table)
                {
                    if (layer.IsDeleted) continue;
                    if (layer.ParentLayerId == cur.Id)
                    {
                        list.Add(layer);
                        queue.Enqueue(layer);
                    }
                }
            }
            return list;
        }

        private string BuildInfoString(
            RhinoDoc doc,
            RhinoObject obj,
            GeometryBase geomOrNull,
            InstanceContext instanceCtx,
            bool instanceOnly = false)
        {
            if (obj == null) return string.Empty;

            var atts = obj.Attributes;
            var sb = new StringBuilder();

            // 基础
            sb.Append($"Id={obj.Id}; ");
            sb.Append($"Type={obj.ObjectType}; ");
            sb.Append($"Name={obj.Name}; ");

            // 图层信息
            var layerIdx = atts?.LayerIndex ?? -1;
            string layerPath = (layerIdx >= 0 && layerIdx < doc.Layers.Count)
                ? SafeFullPath(doc.Layers[layerIdx])
                : string.Empty;
            sb.Append($"Layer={layerPath}; ");

            // 颜色/线型
            try
            {
                var color = atts?.DrawColor(doc) ?? SD.Color.Empty;
                sb.Append($"Color={SD.ColorTranslator.ToHtml(color)}; ");
                var linetype = (atts?.LinetypeIndex ?? -1) >= 0 && atts.LinetypeIndex < doc.Linetypes.Count
                    ? doc.Linetypes[atts.LinetypeIndex].Name
                    : "Default";
                sb.Append($"Linetype={linetype}; ");
            }
            catch { }

            // 实例（块）上下文
            if (instanceCtx != null)
            {
                sb.Append($"Instance.Name={instanceCtx.InstanceName}; ");
                sb.Append($"Instance.Guid={instanceCtx.InstanceGuid}; ");
                sb.Append($"Instance.Layer={instanceCtx.InstanceLayerFullPath}; ");
            }

            // 用户字符串
            try
            {
                if (atts?.UserDictionary != null && atts.UserDictionary.Count > 0)
                {
                    foreach (var key in atts.UserDictionary.Keys)
                    {
                        var val = atts.UserDictionary[key];
                        sb.Append($"User[{key}]={val}; ");
                    }
                }
            }
            catch { }

            if (!instanceOnly)
            {
                // 几何量度
                try
                {
                    if (geomOrNull != null)
                    {
                        switch (geomOrNull)
                        {
                            case Brep brep:
                                if (brep.IsSolid)
                                {
                                    var v = VolumeMassProperties.Compute(brep);
                                    if (v != null) sb.Append($"Volume={v.Volume:F6}; ");
                                }
                                var a = AreaMassProperties.Compute(brep);
                                if (a != null) sb.Append($"Area={a.Area:F6}; ");
                                break;

                            case Extrusion ex:
                                var b = ex.ToBrep(true);
                                if (b != null)
                                {
                                    if (b.IsSolid)
                                    {
                                        var vv = VolumeMassProperties.Compute(b);
                                        if (vv != null) sb.Append($"Volume={vv.Volume:F6}; ");
                                    }
                                    var aa = AreaMassProperties.Compute(b);
                                    if (aa != null) sb.Append($"Area={aa.Area:F6}; ");
                                }
                                break;

                            case Mesh mesh:
                                var am = AreaMassProperties.Compute(mesh);
                                if (am != null) sb.Append($"Area={am.Area:F6}; ");
                                break;

                            case Curve crv:
                                sb.Append($"Length={crv.GetLength():F6}; ");
                                break;
                        }
                    }
                }
                catch { }
            }

            return sb.ToString();
        }

        private void ProcessIdefChild(
            RhinoDoc doc,
            RhinoObject idefChild,
            InstanceContext ctx,
            bool includeGeometry,
            bool includeAttributes,
            List<GeometryBase> geomOut,
            List<Point3d> posOut,
            List<Box> bboxOut,
            List<string> infoOut,
            ref int layerCount)
        {
            if (idefChild == null) return;

            var g = idefChild.Geometry;
            if (g == null)
            {
                if (includeAttributes)
                    infoOut.Add(BuildInfoString(doc, idefChild, null, ctx));
                return;
            }

            GeometryBase work = ConvertExtrusionToBrepIfNeeded(g);
            var dup = work.Duplicate();
            dup?.Transform(ctx.AccumulatedTransform);

            if (includeGeometry && dup != null)
                geomOut.Add(dup);

            var bbox = (dup ?? work).GetBoundingBox(true);
            bboxOut.Add(new Box(Plane.WorldXY, bbox));
            posOut.Add(ComputePosition(dup ?? work));

            if (includeAttributes)
                infoOut.Add(BuildInfoString(doc, idefChild, dup ?? work, ctx));

            layerCount++;
        }

        private class InstanceContext
        {
            public int InstanceLayerIndex;
            public string InstanceLayerFullPath;
            public Guid InstanceGuid;
            public string InstanceName;
            public Transform AccumulatedTransform = Transform.Identity;
        }
        //新增在多线程中暂存单个图层处理结果的类
        private class LayerThreadResult
        {
            public int SortIndex; // 用于最后排序，保证输出顺序和输入一致
            public GH_Path Branch;

            // 对应 GH 的输出参数
            public List<GeometryBase> geomOut = new List<GeometryBase>();
            public List<Point3d> posOut = new List<Point3d>();
            public List<Box> Bboxes = new List<Box>();
            public List<string> infoOut = new List<string>();

            // 对应 Tree 的数据
            public List<GH_String> LayerTreeInfo = new List<GH_String>();

            // 统计数据
            public int ObjectCount = 0;
            public string SummaryText = "";
        }
    }

}



