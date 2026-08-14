using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;  // ★ GroupBy/Select/ToList 需要
using System.Reflection;

public class LegendGenerator : GH_Component
{
    public LegendGenerator()
    : base("图例生成", "Legend",
           "根据曲线/填充（带UserString）或构件库属性串（key=value;...）生成图例。",
           "CW2D", "图例")
    { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        // 通用输入：可接几何（带UserString）或 GH_String（属性串）；列表
        pManager.AddGenericParameter("源几何/属性", "G",
            "可接：曲线/填充（带UserString：剖面样式/图样表示/物料名称），或构件库属性串（key=value;...），可多条。",
            GH_ParamAccess.list);

        pManager.AddPointParameter("起点", "P", "图例左上角（WorldXY）", GH_ParamAccess.item, Point3d.Origin);
        pManager.AddIntegerParameter("每行列数", "Cols", "每行的项目数", GH_ParamAccess.item, 3);
        pManager.AddNumberParameter("样板宽", "W", "样板矩形宽度", GH_ParamAccess.item, 40);
        pManager.AddNumberParameter("样板高", "H", "样板矩形高度", GH_ParamAccess.item, 20);
        pManager.AddNumberParameter("水平间距", "GX", "单元之间的水平间距", GH_ParamAccess.item, 30);
        pManager.AddNumberParameter("垂直间距", "GY", "单元之间的垂直间距", GH_ParamAccess.item, 20);
        pManager.AddNumberParameter("文字高度", "Text", "说明文字高度", GH_ParamAccess.item, 6);
        pManager.AddNumberParameter("图案缩放", "Scale", "Hatch图案比例", GH_ParamAccess.item, 1.0);
        pManager.AddBooleanParameter("烘焙", "Bake", "是否烘焙到当前文档", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddCurveParameter("边框", "Frames", "样板边框曲线", GH_ParamAccess.list);
        pManager.AddGenericParameter("填充", "Hatches", "样板Hatch", GH_ParamAccess.list);
        pManager.AddGenericParameter("文字", "Texts", "说明文字（TextEntity）", GH_ParamAccess.list);
        pManager.AddTextParameter("报告", "Info", "生成信息", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        var inputs = new List<IGH_Goo>();
        Point3d origin = Point3d.Origin;
        int cols = 3; double w = 40, h = 20, gx = 30, gy = 20, th = 6, scale = 1.0;
        bool bake = false;

        if (!DA.GetDataList(0, inputs)) return;
        DA.GetData(1, ref origin);
        DA.GetData(2, ref cols);
        DA.GetData(3, ref w);
        DA.GetData(4, ref h);
        DA.GetData(5, ref gx);
        DA.GetData(6, ref gy);
        DA.GetData(7, ref th);
        DA.GetData(8, ref scale);
        DA.GetData(9, ref bake);

        // 1) 解析输入 → (patternIndex, label)
        var items = new List<(int idx, string label)>();
        double tol = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.01;

        foreach (var o in inputs)
        {
            // A. 几何携带的 UserString
            if (o is IGH_GeometricGoo ggoo)
            {
                // 尽量用 GH_Convert，Rhino7 常用此简化重载
                GeometryBase gb = GH_Convert.ToGeometryBase(ggoo);
                if (gb == null) continue;

                string sIdx = gb.GetUserString("剖面填充样式");
                string label = gb.GetUserString("图样表示");
                if (string.IsNullOrWhiteSpace(label))
                    label = gb.GetUserString("物料名称");

                if (int.TryParse(sIdx, out int idx))
                    items.Add((idx, string.IsNullOrWhiteSpace(label) ? null : label));
            }
            // B. 构件库属性串：可一行多键值；Panel 多行=多条记录；中英标点均可
            else if (o is GH_String gs && !string.IsNullOrWhiteSpace(gs.Value))
            {
                foreach (var record in SplitRecords(gs.Value))
                {
                    var dict = ParseKeyValues(record);
                    if (dict.TryGetValue("剖面填充样式", out string sIdx) && int.TryParse(sIdx, out int idx))
                    {
                        dict.TryGetValue("图样表示", out string label);
                        if (string.IsNullOrWhiteSpace(label)) dict.TryGetValue("物料名称", out label);
                        items.Add((idx, string.IsNullOrWhiteSpace(label) ? null : label));
                    }
                }
            }
        }

        if (items.Count == 0)
        {
            DA.SetDataList(3, new[] { "未解析到任何条目。请检查：是否提供了“剖面样式”（int）与“图样表示/物料名称”" });
            return;
        }

        // 2) 去重（同一图案索引 + 同一标签 只保留一个）
        var unique = items
            .GroupBy(t => new { t.idx, lab = t.label ?? "" })
            .Select(g => (g.Key.idx, g.Key.lab))
            .ToList();

        // 3) 索引合法化 + 标签补全（用 HatchPattern 名称兜底）
        int total = RhinoDoc.ActiveDoc?.HatchPatterns.Count ?? 0;
        for (int i = 0; i < unique.Count; i++)
        {
            int idx = unique[i].idx;
            if (idx < 0 || idx >= total) idx = 0;

            string label = unique[i].lab;
            if (string.IsNullOrWhiteSpace(label))
                label = RhinoDoc.ActiveDoc?.HatchPatterns[idx]?.Name ?? $"Pattern {idx}";

            unique[i] = (idx, label);
        }

        // 4) 排版 & 生成几何
        var frames = new List<Curve>();
        var hatches = new List<Hatch>();
        var texts = new List<TextEntity>();
        var report = new List<string>();

        var plane = Plane.WorldXY;
        double textOffset = 5.0;

        for (int i = 0; i < unique.Count; i++)
        {
            int r = i / Math.Max(1, cols);
            int c = i % Math.Max(1, cols);

            var basePt = origin + new Vector3d(c * (w + gx), -r * (h + gy), 0);

            // 边框矩形（闭合）
            var rect = new Rectangle3d(plane, basePt, basePt + new Point3d(w, -h, 0));
            var frame = rect.ToNurbsCurve();
            frame.MakeClosed(tol);
            frames.Add(frame);

            // Hatch（与你现有代码同一重载：带容差）
            var hatchArr = Hatch.Create(new Curve[] { frame }, unique[i].idx, 0.0, scale, tol);
            if (hatchArr != null && hatchArr.Length > 0) hatches.AddRange(hatchArr);

            // 文字（矩形右侧居中）：Plane 取副本→改 Origin→整体赋回（避免 CS1612）
            var textPt = basePt + new Point3d(w + textOffset, -h * 0.5, 0);
            var te = new TextEntity
            {
                Text = unique[i].lab,
                TextHeight = th,
                Justification = TextJustification.MiddleLeft
            };
            var tp = plane; // 副本
            tp.Origin = textPt;
            te.Plane = tp;
            texts.Add(te);
            
            report.Add(item: $"[{i + 1}] idx={unique[i].idx}, label=\"{unique[i].Item2}\"");
            //report.Add($"[{i + 1}] idx={unique[i].idx}, label=\"{unique[i].label}\"");
        }

        // 5) 可选烘焙
        if (bake && RhinoDoc.ActiveDoc != null)
        {
            foreach (var f in frames) RhinoDoc.ActiveDoc.Objects.AddCurve(f);
            foreach (var hch in hatches) RhinoDoc.ActiveDoc.Objects.AddHatch(hch);
            foreach (var t in texts) RhinoDoc.ActiveDoc.Objects.AddText(t);
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        DA.SetDataList(0, frames);
        DA.SetDataList(1, hatches);
        DA.SetDataList(2, texts);
        DA.SetDataList(3, report);
    }

    // —— 支持 Panel 多行：一行=一条记录；容忍中文分号
    private IEnumerable<string> SplitRecords(string s)
    {
        var norm = s.Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Replace('；', ';'); // 中文分号
        var lines = norm.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            yield return line.Trim().TrimEnd(';');
    }

    // —— 解析 “key=value;key=value...”；容忍中文等号/冒号
    private Dictionary<string, string> ParseKeyValues(string s)
    {
        s = s.Replace('；', ';')
             .Replace('＝', '=')   // 中文等号
             .Replace('：', '=');  // 有些输入会写 key：value

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var parts = s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var kv = p.Split(new[] { '=' }, 2);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim();
                var val = kv[1].Trim();
                if (!string.IsNullOrEmpty(key))
                    dict[key] = val;
            }
        }
        return dict;
    }

    public override Guid ComponentGuid => new Guid("F1B1646E-0A2E-4C67-9A7E-8E8B13C5B8E2");
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
                var resourceName = "CW2D.Resources.LegendGenerator.png";

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
}
