using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TarkovHelper.Debug
{
    public static class AppEnv
    {
        #if DEBUG
        public static bool IsDebugMode { get; set; } = true;
        #else
        public static bool IsDebugMode { get; set; } = false;
        #endif

        private static readonly string AppDataHelperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TarkovHelper"
        );

        public static string DataPath { get; set; } = Path.Combine(AppDataHelperPath, "Data");
        public static string CachePath { get; set; } = Path.Combine(AppDataHelperPath, "Cache");
        public static string ConfigPath { get; set; } = Path.Combine(AppDataHelperPath, "Config");
    }
}
