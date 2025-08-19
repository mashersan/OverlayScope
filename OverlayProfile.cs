using System.Drawing;
using System.Text.Json.Serialization;

namespace OverlayScope
{
    public class OverlayProfile
    {
        // public int Id { get; set; } // ★この行を削除
        public string Name { get; set; } = "新規設定";
        public Rectangle CaptureArea { get; set; }
        public System.Windows.Point WindowPosition { get; set; }
        public double OpacityLevel { get; set; } = 1.0;
        public double ScaleFactor { get; set; } = 1.0;

        [JsonIgnore]
        public bool IsActive { get; set; } = false;

        [JsonIgnore]
        public MainWindow? ActiveWindow { get; set; }
    }
}