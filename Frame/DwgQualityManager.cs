using Rhino.FileIO;
using Rhino;

namespace TitleBlockBattery
{
    public static class DwgQualityManager
    {
        public static FileDwgReadOptions GetReadOptions(string quality)
        {
            switch (quality?.ToLower())
            {
                case "fast":
                    return CreateFastOptions();
                case "high":
                    return CreateHighQualityOptions();
                default:
                    return CreateNormalOptions();
            }
        }

        private static FileDwgReadOptions CreateFastOptions()
        {
            return new FileDwgReadOptions()
            {
                ConvertRegionsToCurves = true,
                ConvertWidePolylinesToSurfaces = false,
                IgnoreThickness = true,                 // 忽略厚度提高速度
                ImportUnreferencedBlocks = false,       // 只导入必要内容
                ImportUnreferencedLayers = false,
                ImportUnreferencedLinetypes = false,
                ModelUnits = UnitSystem.Millimeters,
                MeshPrecision = (FileDwgReadOptions.MeshPrecisionMode)1.0, // 低精度
                SetLayerMaterialToLayerColor = false
            };
        }

        private static FileDwgReadOptions CreateNormalOptions()
        {
            return new FileDwgReadOptions()
            {
                ConvertRegionsToCurves = true,
                ConvertWidePolylinesToSurfaces = false,
                IgnoreThickness = false,
                ImportUnreferencedBlocks = true,
                ImportUnreferencedLayers = true,
                ImportUnreferencedLinetypes = true,
                ModelUnits = UnitSystem.Millimeters,
                MeshPrecision = (FileDwgReadOptions.MeshPrecisionMode)0.1, // 适中精度
                SetLayerMaterialToLayerColor = true
            };
        }

        private static FileDwgReadOptions CreateHighQualityOptions()
        {
            return new FileDwgReadOptions()
            {
                ConvertRegionsToCurves = false,          // 保持原始几何类型
                ConvertWidePolylinesToSurfaces = true,  // 更精确的表示
                IgnoreThickness = false,
                ImportUnreferencedBlocks = true,
                ImportUnreferencedLayers = true,
                ImportUnreferencedLinetypes = true,
                ModelUnits = UnitSystem.Millimeters,
                MeshPrecision = (FileDwgReadOptions.MeshPrecisionMode)0.01, // 高精度
                SetLayerMaterialToLayerColor = true
            };
        }
    }
}