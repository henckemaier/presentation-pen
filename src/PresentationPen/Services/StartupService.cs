using System;
using Microsoft.Win32;

namespace PresentationPen.Services
{
    /// <summary>Liga/desliga a inicialização junto com o Windows (HKCU\...\Run).</summary>
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PresentationPen";

        public static bool IsEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
