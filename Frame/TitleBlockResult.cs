using System.Collections.Generic;
using Rhino.Geometry;

namespace TitleBlockBattery
{
    /// <summary>
    /// 图框读取结果类
    /// </summary>
    public class TitleBlockResult
    {
        /// <summary>
        /// 曲线集合
        /// </summary>
        public List<Curve> Curves { get; set; }

        /// <summary>
        /// 文本对象集合
        /// </summary>
        public List<string> TextObjects { get; set; }

        /// <summary>
        /// 所有几何体集合
        /// </summary>
        public List<GeometryBase> AllGeometry { get; set; }

        /// <summary>
        /// 处理信息
        /// </summary>
        public string Info { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 处理时间
        /// </summary>
        public System.DateTime ProcessTime { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public TitleBlockResult()
        {
            Curves = new List<Curve>();
            TextObjects = new List<string>();
            AllGeometry = new List<GeometryBase>();
            Info = "";
            ErrorMessage = "";
            IsSuccess = false;
            ProcessTime = System.DateTime.Now;
        }

        /// <summary>
        /// 获取结果统计信息
        /// </summary>
        public string GetStatistics()
        {
            return $"Curves: {Curves.Count}, Texts: {TextObjects.Count}, Total Geometry: {AllGeometry.Count}";
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            Curves.Clear();
            TextObjects.Clear();
            AllGeometry.Clear();
            Info = "";
            ErrorMessage = "";
            IsSuccess = false;
        }
    }
}