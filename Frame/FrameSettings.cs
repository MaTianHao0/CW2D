using System;

namespace TitleBlockBattery
{
    /// <summary>
    /// 图框设置类，定义不同尺寸图框的属性
    /// </summary>
    public class FrameSettings
    {
        /// <summary>
        /// 图框宽度（毫米）
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 图框高度（毫米）
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// DWG文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 图框描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 是否为横向布局
        /// </summary>
        public bool IsLandscape { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FrameSettings()
        {
            Width = 210;
            Height = 297;
            FileName = "A3_Frame.dwg";
            Description = "A3 Frame";
            IsLandscape = false;
        }

        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        public FrameSettings(double width, double height, string fileName, string description = "")
        {
            Width = width;
            Height = height;
            FileName = fileName;
            Description = description;
            IsLandscape = width > height;
        }

        /// <summary>
        /// 获取图框的长宽比
        /// </summary>
        public double AspectRatio => Height != 0 ? Width / Height : 1.0;

        /// <summary>
        /// 获取图框面积（平方毫米）
        /// </summary>
        public double Area => Width * Height;

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        public override string ToString()
        {
            return $"{Description} ({Width}×{Height}mm)";
        }
    }
}