using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace OverlayScope
{
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath;

        static SettingsManager()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDir = Path.Combine(appDataPath, "OverlayScope");
            Directory.CreateDirectory(settingsDir);
            SettingsFilePath = Path.Combine(settingsDir, "profiles.json");
        }

        public static List<OverlayProfile> LoadProfiles()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new List<OverlayProfile>();
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<List<OverlayProfile>>(json) ?? new List<OverlayProfile>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定ファイルの読み込みに失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<OverlayProfile>();
            }
        }

        public static void SaveProfiles(List<OverlayProfile> profiles)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(profiles, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定ファイルの保存に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}