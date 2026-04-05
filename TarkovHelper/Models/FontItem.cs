using System;

namespace TarkovHelper.Models
{
    /// <summary>
    /// Model to represent a custom font available in the Fonts directory
    /// </summary>
    public class FontItem
    {
        /// <summary>
        /// Display name shown in UI (e.g., "MapleStory (maplestory.ttf)")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Internal FontFamily name (e.g., "MapleStory")
        /// </summary>
        public string InternalName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the font file including extension
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Formats setting string for persistence (e.g., "InternalName|FileName")
        /// </summary>
        public string ToSettingString()
        {
            return $"{InternalName}|{FileName}";
        }

        /// <summary>
        /// Parses setting string into internal and file name
        /// </summary>
        public static (string InternalName, string FileName) ParseSetting(string setting)
        {
            if (string.IsNullOrEmpty(setting)) return (string.Empty, string.Empty);
            
            var parts = setting.Split('|');
            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }
            // For legacy or simplified settings
            return (setting, string.Empty);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
