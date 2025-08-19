using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace OverlayScope
{
    /// <summary>
    /// アプリケーションの設定（プロファイル一覧）をJSONファイルとして読み書きする静的クラス。
    /// </summary>
    public static class SettingsManager
    {
        /// <summary>
        /// 設定ファイルのフルパスを保持します。
        /// </summary>
        private static readonly string SettingsFilePath;

        /// <summary>
        /// 静的コンストラクタ。クラスが初めて使用される前に一度だけ実行されます。
        /// </summary>
        static SettingsManager()
        {
            // 設定ファイルをユーザーのアプリケーションデータフォルダ内に保存するパスを構築
            // 例: C:\Users\YourUser\AppData\Roaming\OverlayScope\profiles.json
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDir = Path.Combine(appDataPath, "OverlayScope");

            // 保存用フォルダが存在しない場合は作成する
            Directory.CreateDirectory(settingsDir);

            SettingsFilePath = Path.Combine(settingsDir, "profiles.json");
        }

        /// <summary>
        /// 全ての設定プロファイルをファイルから読み込みます。
        /// </summary>
        /// <returns>ファイルから読み込んだプロファイルのリスト。ファイルが存在しない、またはエラーの場合は空のリスト。</returns>
        public static List<OverlayProfile> LoadProfiles()
        {
            // 設定ファイルが存在しない場合は、空のリストを返して処理を終了
            if (!File.Exists(SettingsFilePath))
            {
                return new List<OverlayProfile>();
            }

            try
            {
                // JSONファイルを読み込み、OverlayProfileのリストに変換（デシリアライズ）する
                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<List<OverlayProfile>>(json) ?? new List<OverlayProfile>();
            }
            catch (Exception ex)
            {
                // 読み込みに失敗した場合はエラーメッセージを表示し、空のリストを返す
                MessageBox.Show($"設定ファイルの読み込みに失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<OverlayProfile>();
            }
        }

        /// <summary>
        /// 全ての設定プロファイルをファイルに保存します。
        /// </summary>
        /// <param name="profiles">保存するプロファイルのリスト。</param>
        public static void SaveProfiles(List<OverlayProfile> profiles)
        {
            try
            {
                // 人間が読みやすいようにインデントを付けてJSONに変換（シリアライズ）する
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(profiles, options);

                // ファイルに書き込む
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                // 保存に失敗した場合はエラーメッセージを表示する
                MessageBox.Show($"設定ファイルの保存に失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}