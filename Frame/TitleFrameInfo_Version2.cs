using System;
using System.Xml.Serialization;

namespace TitleBlockBattery
{
    [Serializable]
    public class TitleFrameInfo
    {
        public string ChiefDesigner { get; set; } = "XX"; // 设计总负责人
        public string Approver { get; set; } = "XX"; // 审定人
        public string Reviewer { get; set; } = "XX"; // 审核人
        public string ProfessionalLead { get; set; } = "XX"; // 专业负责人
        public string Checker { get; set; } = "XX"; // 校对人
        public string Designer { get; set; } = "XX"; // 设计人
        public string Client { get; set; } = "XX"; // 建设单位
        public string ProjectName { get; set; } = "XX"; // 工程名称
        public string SubProjectName { get; set; } = "XX"; // 子项名称
        public string DrawingName { get; set; } = "XX"; // 图名
        public string ProjectCode { get; set; } = "XX"; // 工程编号
        public string Discipline { get; set; } = "XX"; // 专业
        public string Version { get; set; } = "XX"; // 版本
        public string Phase { get; set; } = "XX"; // 阶段
        public string Date { get; set; } = "XX"; // 日期
        public string DrawingNumber { get; set; } = "XX"; // 图号
        public string Barcode { get; set; } = "XX"; // 条形码

        // 填充当前日期
        public void FillCurrentDate()
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd");
        }
    }
}