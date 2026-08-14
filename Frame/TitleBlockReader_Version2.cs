using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Rhino.Geometry;
using Rhino.FileIO;
using Rhino;

namespace TitleBlockBattery
{
    /// <summary>
    /// 现代化的图框读取器，使用最新的FileDwg API
    /// </summary>
    public class ModernTitleBlockReader
    {
        private readonly Dictionary<string, FrameSettings> _frameSettings;

        // 定义每个标题需要的空格数量 - 更新后的配置
        private readonly Dictionary<string, int> _titleSpaceMapping = new Dictionary<string, int>
        {
            {"设计总负责人", 18},  // 18个空格 
            {"审定人", 29},       // 29个空格 
            {"审核人", 29},       // 29个空格 
            {"校对人", 29},       // 29个空格 
            {"设计人", 29},       // 29个空格 
            {"条形码", 12},       // 12个空格 
            {"专业负责人", 22},   // 22个空格 
            {"建设单位", 10},     // 10个空格 
            {"工程名称", 10},     // 10个空格 
            {"子项名称", 10},     // 10个空格 
            {"工程编号", 10},     // 10个空格 
            {"图名", 16},         // 16个空格 
            {"图号", 16},         // 16个空格 
            {"专业", 16},         // 16个空格 
            {"版本", 10},         // 10个空格 
            {"阶段", 16},         // 16个空格 
            {"日期",10}          // 10个空格 
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        public ModernTitleBlockReader()
        {
            _frameSettings = InitializeFrameSettings();
        }

        /// <summary>
        /// 初始化图框设置
        /// </summary>
        private Dictionary<string, FrameSettings> InitializeFrameSettings()
        {
            return new Dictionary<string, FrameSettings>
            {
                ["A0"] = new FrameSettings(1189, 841, "A0_Frame.dwg", "A0 图框"),
                ["A1"] = new FrameSettings(841, 594, "A1_Frame.dwg", "A1 图框"),
                ["A2"] = new FrameSettings(594, 420, "A2_Frame.dwg", "A2 图框"),
                ["A3"] = new FrameSettings(420, 297, "A3_Frame.dwg", "A3 图框"),
                ["A4"] = new FrameSettings(297, 210, "A4_Frame.dwg", "A4 图框")
            };
        }

        /// <summary>
        /// 获取支持的图框尺寸
        /// </summary>
        public List<string> GetSupportedSizes()
        {
            return new List<string>(_frameSettings.Keys);
        }

        /// <summary>
        /// 获取指定尺寸的图框设置
        /// </summary>
        public FrameSettings GetFrameSettings(string frameSize)
        {
            return _frameSettings.TryGetValue(frameSize, out var settings) ? settings : null;
        }

        /// <summary>
        /// 读取图框
        /// </summary>
        public TitleBlockResult ReadTitleBlock(string templatePath, string frameSize, Point3d basePoint, TitleFrameInfo frameInfo = null)
        {
            var result = new TitleBlockResult();

            try
            {
                // 验证图框尺寸
                if (!_frameSettings.ContainsKey(frameSize))
                {
                    throw new ArgumentException($"不支持的图框尺寸: {frameSize}。支持的尺寸: {string.Join(", ", _frameSettings.Keys)}");
                }

                // 验证模板路径
                if (!Directory.Exists(templatePath))
                {
                    throw new DirectoryNotFoundException($"模板目录不存在: {templatePath}");
                }

                var frameSetting = _frameSettings[frameSize];
                var dwgFilePath = Path.Combine(templatePath, frameSetting.FileName);

                if (!File.Exists(dwgFilePath))
                {
                    throw new FileNotFoundException($"模板文件不存在: {dwgFilePath}");
                }

                // 执行DWG文件读取
                ReadDwgFileModern(dwgFilePath, basePoint, frameSetting, result, frameInfo);

                result.IsSuccess = true;
                result.Info = $"成功导入 {frameSetting.Description}，共 {result.AllGeometry.Count} 个对象";
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.Info = $"导入失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 使用现代FileDwg API读取DWG文件
        /// </summary>
        private void ReadDwgFileModern(string filePath, Point3d basePoint, FrameSettings frameSettings, TitleBlockResult result, TitleFrameInfo frameInfo)
        {
            try
            {
                // 创建DWG读取选项
                var readOptions = CreateOptimalReadOptions();

                // 使用FileDwg类进行读取
                using (var tempDoc = RhinoDoc.CreateHeadless(null))
                {
                    // 设置文档单位
                    tempDoc.ModelUnitSystem = UnitSystem.Millimeters;

                    // 使用最新的FileDwg.Read方法
                    var success = FileDwg.Read(filePath, tempDoc, readOptions);

                    if (!success)
                    {
                        throw new Exception($"FileDwg.Read 失败: {Path.GetFileName(filePath)}");
                    }

                    // 验证导入结果
                    if (tempDoc.Objects.Count == 0)
                    {
                        throw new Exception("DWG文件导入后未找到任何对象");
                    }

                    // 收集几何体
                    var importedGeometry = CollectGeometryFromDoc(tempDoc);

                    if (importedGeometry.Count == 0)
                    {
                        throw new Exception("未能从DWG文件中提取到有效几何体");
                    }

                    // 处理几何体
                    ProcessGeometryWithTransform(importedGeometry, basePoint, frameSettings, result, frameInfo);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取DWG文件时发生错误 '{Path.GetFileName(filePath)}': {ex.Message}");
            }
        }

        /// <summary>
        /// 创建优化的读取选项
        /// </summary>
        private FileDwgReadOptions CreateOptimalReadOptions()
        {
            return new FileDwgReadOptions()
            {
                // 针对图框优化的设置
                ConvertRegionsToCurves = true,           // 图框边界转换为曲线
                ConvertWidePolylinesToSurfaces = false, // 保持线条形式
                IgnoreThickness = false,                // 保留线宽信息
                ImportUnreferencedBlocks = true,        // 导入所有块定义
                ImportUnreferencedLayers = true,        // 导入所有图层
                ImportUnreferencedLinetypes = true,     // 导入所有线型
                ModelUnits = UnitSystem.Millimeters,    // 设置模型单位
                MeshPrecision = (FileDwgReadOptions.MeshPrecisionMode)0.1, // 适中的网格精度
                SetLayerMaterialToLayerColor = true     // 根据图层颜色设置材质
            };
        }

        /// <summary>
        /// 从文档中收集几何体
        /// </summary>
        private List<GeometryBase> CollectGeometryFromDoc(RhinoDoc doc)
        {
            var geometryList = new List<GeometryBase>();

            foreach (var rhinoObject in doc.Objects)
            {
                if (rhinoObject?.Geometry != null)
                {
                    // 复制几何体以避免引用问题
                    var geometry = rhinoObject.Geometry.Duplicate();
                    if (geometry != null)
                    {
                        geometryList.Add(geometry);
                    }
                }
            }

            return geometryList;
        }

        /// <summary>
        /// 处理几何体并应用变换
        /// </summary>
        private void ProcessGeometryWithTransform(
            List<GeometryBase> geometryList,
            Point3d basePoint,
            FrameSettings frameSettings,
            TitleBlockResult result,
            TitleFrameInfo frameInfo)
        {
            if (geometryList.Count == 0) return;

            // 计算边界框和变换
            var bounds = CalculateBoundingBox(geometryList);
            var transform = CalculateTransformation(bounds, basePoint);

            // 用于去重的文本处理记录
            var processedTexts = new HashSet<string>();

            // 处理每个几何对象
            foreach (var geometry in geometryList)
            {
                ProcessSingleGeometry(geometry, transform, result, frameInfo, processedTexts, frameSettings);
            }
        }

        /// <summary>
        /// 计算几何体的边界框
        /// </summary>
        private BoundingBox CalculateBoundingBox(List<GeometryBase> geometryList)
        {
            var bounds = BoundingBox.Empty;
            foreach (var geometry in geometryList)
            {
                if (geometry != null)
                {
                    bounds.Union(geometry.GetBoundingBox(true));
                }
            }
            return bounds;
        }

        /// <summary>
        /// 计算变换矩阵
        /// </summary>
        private Transform CalculateTransformation(BoundingBox bounds, Point3d targetPoint)
        {
            var sourcePoint = bounds.IsValid ? bounds.Min : Point3d.Origin;
            return Transform.Translation(targetPoint - sourcePoint);
        }

        /// <summary>
        /// 处理单个几何对象 - 保持原始字体大小，不进行缩放
        /// </summary>
        private void ProcessSingleGeometry(GeometryBase geometry, Transform transform, TitleBlockResult result,
            TitleFrameInfo frameInfo, HashSet<string> processedTexts, FrameSettings frameSettings)
        {
            if (geometry == null) return;

            // 应用变换
            var transformedGeometry = geometry.Duplicate();
            transformedGeometry.Transform(transform);

            // 按类型分类处理
            switch (transformedGeometry)
            {
                case Curve curve:
                    result.Curves.Add(curve);
                    result.AllGeometry.Add(transformedGeometry);
                    break;

                case TextEntity textEntity:
                    // 处理文字，保持原始字体大小
                    var processedText = ProcessTextWithoutScaling(textEntity, frameInfo, processedTexts);
                    if (processedText != null)
                    {
                        result.AllGeometry.Add(processedText);
                        result.TextObjects.Add(processedText.Text ?? "");
                    }
                    break;

                case Brep brep:
                    result.AllGeometry.Add(transformedGeometry);
                    ExtractBrepEdges(brep, result.Curves);
                    break;

                case Rhino.Geometry.Mesh rhinoMesh:
                    result.AllGeometry.Add(transformedGeometry);
                    ExtractMeshOutlines(rhinoMesh, result.Curves);
                    break;

                case Point point:
                    result.AllGeometry.Add(transformedGeometry);
                    break;

                default:
                    result.AllGeometry.Add(transformedGeometry);
                    break;
            }
        }

        /// <summary>
        /// 处理文本，不进行字体缩放，保持原始大小
        /// </summary>
        private TextEntity ProcessTextWithoutScaling(TextEntity originalText, TitleFrameInfo frameInfo,
            HashSet<string> processedTexts)
        {
            try
            {
                // 复制原始文本对象，保留其属性包括原始字体大小
                var newText = originalText.Duplicate() as TextEntity;
                if (newText == null) return originalText;

                // 获取原始文本位置，用于去重判断
                var textKey = $"{newText.Plane.Origin.X:F2},{newText.Plane.Origin.Y:F2},{newText.Text}";

                // 检查是否已经处理过相同位置的相同文本
                if (processedTexts.Contains(textKey))
                {
                    System.Diagnostics.Debug.WriteLine($"跳过重复文本: {newText.Text} at {newText.Plane.Origin}");
                    return null; // 跳过重复文本
                }

                processedTexts.Add(textKey);

                // 只替换占位符文本，不修改字体大小
                if (frameInfo != null && newText.Text != null)
                {
                    string originalString = newText.Text;
                    string replacedText = ReplaceTextWithUpdatedCustomSpaces(originalString, frameInfo);

                    if (replacedText != originalString)
                    {
                        newText.Text = replacedText;
                        System.Diagnostics.Debug.WriteLine($"文本替换: '{originalString}' -> '{replacedText}' (保持原始字体大小: {newText.TextHeight:F2})");
                    }
                }

                return newText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理文本时出错: {ex.Message}");
                return originalText;
            }
        }

        /// <summary>
        /// 根据更新后的标题配置使用不同数量的空格进行替换
        /// </summary>
        private string ReplaceTextWithUpdatedCustomSpaces(string text, TitleFrameInfo frameInfo)
        {
            if (frameInfo == null || string.IsNullOrEmpty(text))
                return text;

            // 定义标题到值的映射
            var titleValueMappings = new Dictionary<string, string>
            {
                {"设计总负责人", frameInfo.ChiefDesigner},
                {"审定人", frameInfo.Approver},
                {"审核人", frameInfo.Reviewer},
                {"专业负责人", frameInfo.ProfessionalLead},
                {"校对人", frameInfo.Checker},
                {"设计人", frameInfo.Designer},
                {"条形码", frameInfo.Barcode},
                {"建设单位", frameInfo.Client},
                {"工程名称", frameInfo.ProjectName},
                {"子项名称", frameInfo.SubProjectName},
                {"工程编号", frameInfo.ProjectCode},
                {"图名", frameInfo.DrawingName},
                {"图号", frameInfo.DrawingNumber},
                {"专业", frameInfo.Discipline},
                {"版本", frameInfo.Version},
                {"阶段", frameInfo.Phase},
                {"日期", frameInfo.Date}
            };

            string result = text;

            // 遍历所有标题，查找匹配并应用相应的空格数量
            foreach (var titleMapping in titleValueMappings)
            {
                string titleKey = titleMapping.Key;
                string titleValue = titleMapping.Value;

                if (result.Contains(titleKey))
                {
                    // 获取该标题需要的空格数量
                    int spaceCount = _titleSpaceMapping.TryGetValue(titleKey, out int spaces) ? spaces : 1;

                    // 创建指定数量的空格字符串
                    string customSpaces = new string(' ', spaceCount);

                    // 匹配"标题 + 任意空格 + XX"的模式
                    string pattern = $@"({Regex.Escape(titleKey)})\s*XX";
                    string replacement = !string.IsNullOrEmpty(titleValue) ? titleValue : "XX";

                    // 使用更新后的自定义空格数量替换
                    string replaceWith = $"{titleKey}{customSpaces}{replacement}";

                    result = Regex.Replace(result, pattern, replaceWith);

                    if (result != text)
                    {
                        System.Diagnostics.Debug.WriteLine($"更新空格配置替换: '{titleKey}' -> '{replacement}' ({spaceCount}个空格)");
                        return result; // 找到匹配就返回，避免多次替换
                    }
                }
            }

            // 处理英文标签（使用默认单空格）
            var englishLabelMappings = new Dictionary<string, string>
            {
                {"designer", frameInfo.Designer},
                {"date", frameInfo.Date},
                {"project", frameInfo.ProjectName},
                {"drawing", frameInfo.DrawingName},
                {"version", frameInfo.Version},
                {"phase", frameInfo.Phase},
                {"approver", frameInfo.Approver},
                {"reviewer", frameInfo.Reviewer},
                {"checker", frameInfo.Checker},
                {"chief", frameInfo.ChiefDesigner}
            };

            foreach (var mapping in englishLabelMappings)
            {
                string pattern = $@"({Regex.Escape(mapping.Key)})\s+XX";
                if (Regex.IsMatch(result, pattern, RegexOptions.IgnoreCase))
                {
                    string replacement = !string.IsNullOrEmpty(mapping.Value) ? mapping.Value : "XX";
                    string replaceWith = $"{mapping.Key} {replacement}"; // 英文保持单空格

                    result = Regex.Replace(result, pattern, replaceWith, RegexOptions.IgnoreCase);

                    System.Diagnostics.Debug.WriteLine($"英文标签替换: '{mapping.Key}' -> '{replacement}' (单个空格)");
                    return result;
                }
            }

            // 处理单独的"XX"情况
            if (result.Trim() == "XX")
            {
                result = !string.IsNullOrEmpty(frameInfo.Designer) ? frameInfo.Designer : "XX";
                System.Diagnostics.Debug.WriteLine($"单独XX替换为默认设计人: '{result}'");
            }

            return result;
        }

        /// <summary>
        /// 提取Brep的边缘曲线
        /// </summary>
        private void ExtractBrepEdges(Brep brep, List<Curve> curves)
        {
            try
            {
                foreach (var edge in brep.Edges)
                {
                    var edgeCurve = edge.DuplicateCurve();
                    if (edgeCurve != null)
                    {
                        curves.Add(edgeCurve);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略边缘提取错误
            }
        }

        /// <summary>
        /// 提取网格轮廓线
        /// </summary>
        private void ExtractMeshOutlines(Rhino.Geometry.Mesh mesh, List<Curve> curves)
        {
            try
            {
                var outlines = mesh.GetOutlines(Plane.WorldXY);
                if (outlines != null && outlines.Length > 0)
                {
                    foreach (var outline in outlines)
                    {
                        var polylineCurve = outline.ToPolylineCurve();
                        if (polylineCurve != null)
                        {
                            curves.Add(polylineCurve);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 忽略轮廓提取错误
            }
        }

        /// <summary>
        /// 创建备用图框（当DWG导入失败时）
        /// </summary>
        public TitleBlockResult CreateFallbackFrame(string frameSize, Point3d basePoint)
        {
            var result = new TitleBlockResult();

            if (_frameSettings.TryGetValue(frameSize, out var settings))
            {
                // 创建简单的矩形框架
                var corners = new Point3d[]
                {
                    basePoint,
                    basePoint + new Vector3d(settings.Width, 0, 0),
                    basePoint + new Vector3d(settings.Width, settings.Height, 0),
                    basePoint + new Vector3d(0, settings.Height, 0),
                    basePoint
                };

                var rectangle = new PolylineCurve(corners);
                result.Curves.Add(rectangle);
                result.AllGeometry.Add(rectangle);
                result.IsSuccess = true;
                result.Info = $"创建了备用 {settings.Description} 框架";
            }

            return result;
        }
    }
}