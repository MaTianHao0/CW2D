using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CW2D.MakeNode
{
    public class IOFile
    {
        public static string DictPath = @"..\Plug-ins\Grasshopper\Components\PathTable.json";
        //@"..\Plug-ins\Grasshopper\Components\PathTable.json";

        public static Dictionary<string, string> PathTable = new Dictionary<string, string>();

        public string FilePath { get; set; }

        public List<GeometryBase> Geometries { get; set; }

        public Point3d Center { get; set; }

        public string Name { get => Path.GetFileName(FilePath); }

        public IOFile()
        {
            FilePath = string.Empty;
            Geometries = new List<GeometryBase>();
            Center = Point3d.Origin;
        }

        public IOFile(string filePath)
        {
            FilePath = filePath;
            Geometries = new List<GeometryBase>();
            Center = Point3d.Origin;
        }

        public IOFile(string filePath, List<GeometryBase> geometries, Point3d center)
        {
            FilePath = filePath;
            Geometries = geometries;
            Center = center;
        }

        /// <summary>
        /// 将 pathTable 写入 dictPath 指定的 JSON 文件
        /// </summary>
        public static void SavePathTable()
        {
            var json = JsonSerializer.Serialize(PathTable, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DictPath, json);
        }

        /// <summary>
        /// 从 dictPath 指定的 JSON 文件读取 pathTable
        /// </summary>
        public static void LoadPathTable()
        {
            if (!File.Exists(DictPath))
                throw new FileNotFoundException($"未找到字典文件: {DictPath}");

            var json = File.ReadAllText(DictPath);
            PathTable = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
        }

        public void ReadFile()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    throw new FileNotFoundException($"文件未找到: {FilePath}");
                }

                using (var tempDoc = RhinoDoc.CreateHeadless(null))
                {
                    tempDoc.ModelUnitSystem = UnitSystem.Millimeters;

                    var options = new FileDwgReadOptions()
                    {
                        ImportUnreferencedLayers = true,
                        ImportUnreferencedBlocks = true,
                        ImportUnreferencedLinetypes = true,
                        ConvertWidePolylinesToSurfaces = true,
                        IgnoreThickness = true,
                        ConvertRegionsToCurves = true,
                        MeshPrecision = FileDwgReadOptions.MeshPrecisionMode.DoublePrecision,
                        ModelUnits = UnitSystem.Millimeters,
                        LayoutUnits = UnitSystem.Millimeters,
                        SetLayerMaterialToLayerColor = false
                    };

                    if (!FileDwg.Read(FilePath, tempDoc, options))
                    {
                        throw new Exception($"FileDwg.Read 失败: {Path.GetFileName(FilePath)}");
                    }

                    var layerIndex = tempDoc.Layers.FindName("CenterPoint").Index;

                    var geometries = new List<GeometryBase>();
                    foreach (var obj in tempDoc.Objects)
                    {
                        if (obj.Attributes.LayerIndex == layerIndex)
                        {
                            var point = obj.Geometry as Point;
                            Center = point.Location;
                            continue;
                        }
                        var geo = obj.Geometry.Duplicate();
                        if (geo != null)
                        {
                            geometries.Add(geo);
                        }
                    }
                    Geometries = geometries;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取文件时出错: {ex.Message}");
            }
        }

        public void WriteFile()
        {
            try
            {
                using (var tempDoc = RhinoDoc.CreateHeadless(null))
                {
                    tempDoc.ModelUnitSystem = UnitSystem.Millimeters;

                    var layer = new Layer() { Name = "CenterPoint" };
                    int layerIndex = tempDoc.Layers.Add(layer);
                    var objattr = new ObjectAttributes() { LayerIndex = layerIndex };
                    Point pt = new Point(Center);
                    tempDoc.Objects.Add(pt, objattr);

                    foreach (var geometry in Geometries)
                    {
                        if (geometry != null)
                        {
                            tempDoc.Objects.Add(geometry);
                        }
                    }

                    var options = new FileDwgWriteOptions();

                    if (!FileDwg.Write(FilePath, tempDoc, options))
                    {
                        throw new Exception($"FileDwg.Write 失败: {Path.GetFileName(FilePath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"写入文件时出错: {ex.Message}");
            }
        }
    }
}
