using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace TitleBlockBattery
{
    public class TitleBlockManager
    {
        private string _configPath;
        private TitleBlockConfig _config;

        public TitleBlockManager(string configPath = null)
        {
            _configPath = configPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TitleBlockBattery",
                "config.xml");
            LoadConfiguration();
        }

        public void LoadConfiguration()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(TitleBlockConfig));
                    using (var reader = new FileStream(_configPath, FileMode.Open))
                    {
                        _config = (TitleBlockConfig)serializer.Deserialize(reader);
                    }

                    // 迁移/兜底：确保非空
                    if (_config.FrameInfo == null)
                        _config.FrameInfo = new TitleFrameInfo();
                    if (_config.Presets == null)
                        _config.Presets = new List<TitleFrameInfoPreset>();
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show($"Failed to load configuration: {ex.Message}");
                    _config = CreateDefaultConfig();
                }
            }
            else
            {
                _config = CreateDefaultConfig();
                SaveConfiguration();
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var serializer = new XmlSerializer(typeof(TitleBlockConfig));
                using (var writer = new FileStream(_configPath, FileMode.Create))
                {
                    serializer.Serialize(writer, _config);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save configuration: {ex.Message}");
            }
        }

        private TitleBlockConfig CreateDefaultConfig()
        {
            return new TitleBlockConfig
            {
                DefaultTemplatePath = "",
                SupportedSizes = new List<string> { "A0", "A1", "A2", "A3", "A4" },
                DefaultSize = "A4",
                LastUsedPath = "",
                FrameInfo = new TitleFrameInfo(),
                Presets = new List<TitleFrameInfoPreset>()
            };
        }

        public TitleBlockConfig GetConfig() => _config;

        public void UpdateConfig(TitleBlockConfig config)
        {
            _config = config ?? _config;
            // 兜底
            if (_config.FrameInfo == null) _config.FrameInfo = new TitleFrameInfo();
            if (_config.Presets == null) _config.Presets = new List<TitleFrameInfoPreset>();
            SaveConfiguration();
        }

        // ===== 历史预设（多份 17 字段）管理 =====

        public IReadOnlyList<TitleFrameInfoPreset> GetPresets()
        {
            if (_config.Presets == null) _config.Presets = new List<TitleFrameInfoPreset>();
            return _config.Presets.AsReadOnly();
        }

        public TitleFrameInfoPreset AddPreset(TitleFrameInfo info, string name = null)
        {
            if (info == null) info = new TitleFrameInfo();

            var preset = new TitleFrameInfoPreset
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? $"Preset {DateTime.Now:yyyyMMdd_HHmmss}" : name.Trim(),
                FrameInfo = info,
                CreatedAt = DateTime.Now,
                LastUsedAt = DateTime.Now
            };

            if (_config.Presets == null) _config.Presets = new List<TitleFrameInfoPreset>();
            _config.Presets.Add(preset);
            SaveConfiguration();
            return preset;
        }

        public bool DeletePreset(Guid id)
        {
            if (_config.Presets == null) return false;
            var index = _config.Presets.FindIndex(p => p.Id == id);
            if (index >= 0)
            {
                _config.Presets.RemoveAt(index);
                SaveConfiguration();
                return true;
            }
            return false;
        }

        public bool RenamePreset(Guid id, string newName)
        {
            if (_config.Presets == null) return false;
            var item = _config.Presets.Find(p => p.Id == id);
            if (item == null) return false;
            item.Name = string.IsNullOrWhiteSpace(newName) ? item.Name : newName.Trim();
            SaveConfiguration();
            return true;
        }

        /// <summary>
        /// 应用预设到当前 FrameInfo（写回配置）
        /// </summary>
        public bool ApplyPreset(Guid id)
        {
            if (_config.Presets == null) return false;
            var item = _config.Presets.Find(p => p.Id == id);
            if (item == null || item.FrameInfo == null) return false;

            // 深拷贝（通过序列化或手动复制字段）
            _config.FrameInfo = new TitleFrameInfo
            {
                ChiefDesigner = item.FrameInfo.ChiefDesigner,
                Approver = item.FrameInfo.Approver,
                Reviewer = item.FrameInfo.Reviewer,
                ProfessionalLead = item.FrameInfo.ProfessionalLead,
                Checker = item.FrameInfo.Checker,
                Designer = item.FrameInfo.Designer,
                Client = item.FrameInfo.Client,
                ProjectName = item.FrameInfo.ProjectName,
                SubProjectName = item.FrameInfo.SubProjectName,
                DrawingName = item.FrameInfo.DrawingName,
                ProjectCode = item.FrameInfo.ProjectCode,
                Discipline = item.FrameInfo.Discipline,
                Version = item.FrameInfo.Version,
                Phase = item.FrameInfo.Phase,
                Date = item.FrameInfo.Date,
                DrawingNumber = item.FrameInfo.DrawingNumber,
                Barcode = item.FrameInfo.Barcode
            };

            item.LastUsedAt = DateTime.Now;
            SaveConfiguration();
            return true;
        }
    }

    [Serializable]
    public class TitleBlockConfig
    {
        public string DefaultTemplatePath { get; set; }
        public List<string> SupportedSizes { get; set; }
        public string DefaultSize { get; set; }
        public string LastUsedPath { get; set; }
        public TitleFrameInfo FrameInfo { get; set; }

        // 新增：多份历史预设
        public List<TitleFrameInfoPreset> Presets { get; set; }
    }

    [Serializable]
    public class TitleFrameInfoPreset
    {
        public Guid Id { get; set; }
        public string Name { get; set; } // 预设名
        public TitleFrameInfo FrameInfo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }

        public override string ToString()
        {
            return $"{Name}  (saved {CreatedAt:yyyy-MM-dd HH:mm})";
        }
    }
}
