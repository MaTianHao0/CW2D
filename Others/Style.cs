using Rhino;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.Drawing;

internal class Style
{
   
    static Dictionary<string, Color> _colors = new Dictionary<string, Color>()
    {
        { "Red1", Color.FromArgb(255, 000, 000)},
        { "Yellow2", Color.FromArgb(255, 255, 000) },
        { "Green3", Color.FromArgb(000, 255, 000) },
        { "Cyan4", Color.FromArgb(000, 255, 255) },
        { "Blue5", Color.FromArgb(000, 000, 255) },
        { "Magenta6", Color.FromArgb(255, 000, 255) },
        { "White7", Color.FromArgb(255, 255, 255) },
        { "Gray8", Color.FromArgb(065, 065, 065) },
        { "Gray9", Color.FromArgb(128, 128, 128) },
        { "Khaki40", Color.FromArgb(255, 191, 000) },
        { "Blue134", Color.FromArgb(000, 129, 129) },
    };

    ObjectAttributes _attributes;

    public ObjectAttributes Attributes
    {
        get => _attributes;
    }

    public Style()
    {
        _attributes = new ObjectAttributes();
    }

    public static bool AddColor(string name, Color color)
    {
        if (!_colors.ContainsKey(name))
        {
            _colors.Add(name, color);
            return true;
        }
        else
        {
            return false;
        }
    }

    public static int AddLintype(string name, double[] linetypes)
    {
        int flag = RhinoDoc.ActiveDoc.Linetypes.Find(name);
        int index = flag == -1 ? RhinoDoc.ActiveDoc.Linetypes.Add(name, linetypes) : flag;
        return index;
    }

    public static int AddHatch(HatchPattern hatchPattern)
    {
        int index = RhinoDoc.ActiveDoc.HatchPatterns.Add(hatchPattern);
        return index;
    }

    public static int AddLayer(Layer layer)
    {
        int index = RhinoDoc.ActiveDoc.Layers.Add(layer);
        return index;
    }

    public static int AddLayer(string name, Color color, int linetypeIndex)
    {
        var layer = new Layer()
        {
            Name = name,
            Color = color,
            LinetypeIndex = linetypeIndex,
        };
        int index = RhinoDoc.ActiveDoc.Layers.Add(layer);
        return index;
    }

    public static int AddLayer(string name, Color color, int linetypeIndex, Guid parentId)
    {
        var layer = new Layer()
        {
            Name = name,
            Color = color,
            LinetypeIndex = linetypeIndex,
            ParentLayerId = parentId,
        };
        int index = RhinoDoc.ActiveDoc.Layers.Add(layer);
        return index;
    }

    public static int FindLinetype(string name)
    {
        return RhinoDoc.ActiveDoc.Linetypes.Find(name);
    }

    public static int FindHatch(string name)
    {
        var hatch = RhinoDoc.ActiveDoc.HatchPatterns.FindName(name);
        return hatch.Index;
    }

    public static int FindLayer(string name)
    {
        var layer = RhinoDoc.ActiveDoc.Layers.FindName(name);
        return layer.Index;
    }

    public void SetColor(Color color)
    {
        _attributes.ColorSource = ObjectColorSource.ColorFromObject;
        _attributes.ObjectColor = color;
    }

    public void SetLinetype(int index)
    {
        _attributes.LinetypeSource = ObjectLinetypeSource.LinetypeFromObject;
        _attributes.LinetypeIndex = index;
    }

    public void SetLayer(int index)
    {
        _attributes.LayerIndex = index;
    }

    public void ColorFromLayer()
    {
        _attributes.ColorSource = ObjectColorSource.ColorFromLayer;
    }

    public void LinetypeFromLayer()
    {
        _attributes.LinetypeSource = ObjectLinetypeSource.LinetypeFromLayer;
    }

    public static bool InitializeOrNot { get; set; } = false;

    public static void Initialize()
    {
        var layers = RhinoDoc.ActiveDoc.Layers;
        var linetype = RhinoDoc.ActiveDoc.Linetypes;

        double[] centerLine = { 1.25, -.25, .25, -.25 };
        double[] dashedLine = { .5, -.25 };
        double[] divideLine = { .5, -.25, 0, -.25, 0, -.25 };
        double[] dotLine = { 0, -.25 };
        double[] phantomLine = { 1.25, -.25, .25, -.25, .25, -.25 };

        int continuous = RhinoDoc.ActiveDoc.Linetypes.Find("Continuous");
        int center = AddLintype("Center", centerLine);
        int dashed = AddLintype("Dashed", dashedLine);
        int divide = AddLintype("Divide", divideLine);
        int dot = AddLintype("Dot", dotLine);
        int phantom = AddLintype("Phantom", phantomLine);

        Layer[] myLayers =
        {
            new Layer()
            {
                Name = "Default",
            },
            new Layer()
            {
                Name = "01B2-文字",
                Color = _colors["White7"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "02B2-钢材",
                Color = _colors["Yellow2"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "03B2-型材",
                Color = _colors["Green3"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "04B2-铝板",
                Color = _colors["Magenta6"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "05B2-玻璃",
                Color = _colors["Cyan4"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "06B2-石材",
                Color = _colors["Khaki40"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "07B2-附件",
                Color = _colors["Gray8"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "08B2-填充",
                Color = _colors["Gray8"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "09B2-结构",
                Color = _colors["Blue5"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "10B2-虚线",
                Color = _colors["Gray9"],
                LinetypeIndex = dashed,
            },
            new Layer()
            {
                Name = "11B2-轴线",
                Color = _colors["Red1"],
                LinetypeIndex = center,
            },
            new Layer()
            {
                Name = "12B2-边界线",
                Color = _colors["Gray9"],
                LinetypeIndex = divide,
            },
            new Layer()
            {
                Name = "13B2-辅助线",
                Color = _colors["Gray9"],
                LinetypeIndex = dot,
            },
            new Layer()
            {
                Name = "14B2-图元",
                Color = _colors["Red1"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "15B2-图框",
                Color = _colors["Blue134"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "16B2-标注",
                Color = _colors["Red1"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "17B2-轮廓线",
                Color = _colors["Green3"],
                LinetypeIndex = continuous,
            },
            new Layer()
            {
                Name = "18B2-双点划线",
                Color = _colors["Gray8"],
                LinetypeIndex = phantom,
            },
        };

        for (int i = 0; i < myLayers.Length; i++)
        {
            int index = AddLayer(myLayers[i]);
        }

        AddHatch(HatchPattern.Defaults.Solid);
        AddHatch(HatchPattern.Defaults.Hatch1);
        AddHatch(HatchPattern.Defaults.Hatch2);
        AddHatch(HatchPattern.Defaults.Hatch3);
        AddHatch(HatchPattern.Defaults.Dash);
        AddHatch(HatchPattern.Defaults.Grid);
        AddHatch(HatchPattern.Defaults.Grid60);
        AddHatch(HatchPattern.Defaults.Plus);
        AddHatch(HatchPattern.Defaults.Squares);

    }

    public static DimensionStyle SetDimensionStyle(double length)
    {
        var style = RhinoDoc.ActiveDoc.DimStyles.Current.Duplicate();
        style.TextHeight = length;
        style.FitText = DimensionStyle.TextFit.TextInside;
        int index = RhinoDoc.ActiveDoc.DimStyles.Add(style, false);
        if (index >= 0) style = RhinoDoc.ActiveDoc.DimStyles[index];
        return style;
    }

}