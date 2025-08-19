using System.Drawing;
using System.Text.Json.Serialization;

namespace OverlayScope
{
    /// <summary>
    /// 一つのオーバーレイ設定（プロファイル）を保持するデータクラス。
    /// このクラスのインスタンスのリストがJSONファイルに保存されます。
    /// </summary>
    public class OverlayProfile
    {
        /// <summary>
        /// プロファイルの表示名。
        /// </summary>
        public string Name { get; set; } = "新規設定";

        /// <summary>
        /// キャプチャする画面領域（論理ピクセル）。
        /// </summary>
        public Rectangle CaptureArea { get; set; }

        /// <summary>
        /// オーバーレイウィンドウの表示位置（論理ピクセル）。
        /// </summary>
        public System.Windows.Point WindowPosition { get; set; }

        /// <summary>
        /// ウィンドウの透過率 (0.0 - 1.0)。
        /// </summary>
        public double OpacityLevel { get; set; } = 1.0;

        /// <summary>
        /// 表示の拡大率。
        /// </summary>
        public double ScaleFactor { get; set; } = 1.0;

        /// <summary>
        /// このプロファイルが現在アクティブ（表示中）かどうか。
        /// このプロパティはJSONには保存されません。
        /// </summary>
        [JsonIgnore]
        public bool IsActive { get; set; } = false;

        /// <summary>
        /// このプロファイルに対応する、現在表示中のウィンドウのインスタンス。
        /// このプロパティはJSONには保存されません。
        /// </summary>
        [JsonIgnore]
        public MainWindow? ActiveWindow { get; set; }
    }
}