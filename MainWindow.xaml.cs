using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OverlayScope
{
    /// <summary>
    /// オーバーレイ表示を行うウィンドウのロジック。
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly DispatcherTimer _captureTimer; // 画面キャプチャを定期的に実行するタイマー
        private System.Drawing.Rectangle _captureArea;  // キャプチャ対象の画面領域（物理ピクセル）
        private bool _isInOperationMode = false;        // 現在が操作モードかどうかを保持するフラグ

        // Shift+Qによる一時的な非表示状態を管理するための変数
        private bool _isTemporarilyHidden = false;
        private double _lastOpacityBeforeHiding = 1.0;
        private const double HIDDEN_OPACITY = 0.05; // 一時的に非表示にする際の透過率(5%)

        private double _dpiScale = 1.0;          // ディスプレイのDPIスケール値
        private readonly OverlayProfile _profile; // このウィンドウに対応する設定プロファイル

        #endregion

        #region Constructor

        public MainWindow(OverlayProfile profile)
        {
            InitializeComponent();
            _profile = profile;
            _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _captureTimer.Tick += CaptureTimer_Tick;
        }

        #endregion

        #region Window Events

        /// <summary>
        /// ウィンドウがロードされた時の初期化処理。
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 現在のモニターのDPIスケールを取得
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _dpiScale = source.CompositionTarget.TransformToDevice.M11;
            }

            // プロファイル設定をウィンドウに適用
            ApplyProfileSettings();
            // 起動時はクリックできない透過モードから開始
            EnterTransparentMode();
        }

        /// <summary>
        /// ウィンドウがアクティブになった時の処理 (Alt+Tabなどで選択された時)。
        /// </summary>
        private void Window_Activated(object sender, EventArgs e)
        {
            EnterOperationMode();
        }

        /// <summary>
        /// ウィンドウが非アクティブになった時の処理 (他のウィンドウがクリックされた時)。
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 操作モード中であったなら、安全のために透過モードに戻る
            if (_isInOperationMode)
            {
                EnterTransparentMode();
            }
        }

        #endregion

        #region Settings Logic

        /// <summary>
        /// プロファイルの設定情報に基づいて、ウィンドウの見た目やキャプチャ範囲を適用します。
        /// </summary>
        private void ApplyProfileSettings()
        {
            // DPIスケーリングを考慮して、キャプチャ用の物理ピクセル座標を計算
            _captureArea = new System.Drawing.Rectangle(
                (int)(_profile.CaptureArea.X * _dpiScale),
                (int)(_profile.CaptureArea.Y * _dpiScale),
                (int)(_profile.CaptureArea.Width * _dpiScale),
                (int)(_profile.CaptureArea.Height * _dpiScale)
            );

            // ウィンドウの位置とサイズは、WPFが管理する論理ピクセルで設定
            this.Left = _profile.WindowPosition.X;
            this.Top = _profile.WindowPosition.Y;
            this.Opacity = _profile.OpacityLevel;
            double scale = _profile.ScaleFactor;
            this.Width = _profile.CaptureArea.Width * scale;
            this.Height = _profile.CaptureArea.Height * scale;

            // UIスライダーの値を更新
            OpacitySlider.Value = this.Opacity;
            ScaleSlider.Value = scale;
        }

        /// <summary>
        /// 現在のウィンドウの状態（位置、サイズなど）を設定プロファイルオブジェクトに保存します。
        /// </summary>
        private void UpdateProfileFromCurrentState()
        {
            // キャプチャ範囲は、DPIスケールを考慮して論理ピクセルに戻してから保存
            _profile.CaptureArea = new System.Drawing.Rectangle(
                (int)(_captureArea.X / _dpiScale),
                (int)(_captureArea.Y / _dpiScale),
                (int)(_captureArea.Width / _dpiScale),
                (int)(_captureArea.Height / _dpiScale)
            );
            _profile.WindowPosition = new System.Windows.Point(this.Left, this.Top);
            _profile.OpacityLevel = _isTemporarilyHidden ? _lastOpacityBeforeHiding : this.Opacity;
            _profile.ScaleFactor = ScaleSlider.Value;
        }

        #endregion

        #region Mode Switching & Keyboard Logic

        /// <summary>
        /// ユーザーが操作できる「操作モード」に切り替えます。
        /// </summary>
        private void EnterOperationMode()
        {
            if (_isInOperationMode) return;
            SetClickThrough(false);
            ControlBar.Visibility = Visibility.Visible;
            HighlightBorder.Visibility = Visibility.Visible;
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            _isInOperationMode = true;
        }

        /// <summary>
        /// クリックが背面に透過される「透過モード」に切り替えます。
        /// </summary>
        private void EnterTransparentMode()
        {
            if (!_isInOperationMode && Background == null) return;
            UpdateProfileFromCurrentState(); // 透過モードに入る前に現在の状態を保存
            SetClickThrough(true);
            ControlBar.Visibility = Visibility.Collapsed;
            HighlightBorder.Visibility = Visibility.Collapsed;
            this.Background = null;
            _isInOperationMode = false;

            // リアルタイムキャプチャを開始/再開
            if (!_captureTimer.IsEnabled)
            {
                _captureTimer.Start();
            }
        }

        /// <summary>
        /// 操作モード中の各種キーボードショートカットを処理します。
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isInOperationMode) return;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                switch (e.Key)
                {
                    case Key.W: SelectCaptureArea(); e.Handled = true; break;
                    case Key.Q: ToggleTemporaryHide(); e.Handled = true; break;
                    case Key.Up: ScaleSlider.Value = Math.Min(ScaleSlider.Maximum, ScaleSlider.Value + 0.1); e.Handled = true; break;
                    case Key.Down: ScaleSlider.Value = Math.Max(ScaleSlider.Minimum, ScaleSlider.Value - 0.1); e.Handled = true; break;
                    case Key.Right: OpacitySlider.Value = Math.Min(OpacitySlider.Maximum, OpacitySlider.Value + 0.05); e.Handled = true; break;
                    case Key.Left: OpacitySlider.Value = Math.Max(OpacitySlider.Minimum, OpacitySlider.Value - 0.05); e.Handled = true; break;
                    case Key.E: this.Close(); e.Handled = true; break;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Up: this.Top -= 1; e.Handled = true; break;
                    case Key.Down: this.Top += 1; e.Handled = true; break;
                    case Key.Left: this.Left -= 1; e.Handled = true; break;
                    case Key.Right: this.Left += 1; e.Handled = true; break;
                }
            }
        }

        #endregion

        #region Core Application Logic

        /// <summary>
        /// タイマーによって定期的に呼び出され、画面をキャプチャして表示を更新します。
        /// </summary>
        private void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            UpdateCaptureImage();
        }

        /// <summary>
        /// 範囲選択ウィンドウを表示し、キャプチャ範囲を再設定します。
        /// </summary>
        private void SelectCaptureArea()
        {
            _captureTimer.Stop();
            this.Visibility = Visibility.Hidden;

            AreaSelectorWindow selector = new AreaSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedArea.Width > 0)
            {
                var rect = selector.SelectedArea;
                // 新しい範囲をプロファイルに反映
                _profile.CaptureArea = new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                // 表示位置もキャプチャ範囲の左上にリセット
                _profile.WindowPosition = new System.Windows.Point(rect.X, rect.Y);
                // 新しい設定をウィンドウに再適用
                ApplyProfileSettings();
            }

            this.Visibility = Visibility.Visible;
            EnterOperationMode();
            this.Activate();
        }

        /// <summary>
        /// 現在のキャプチャ範囲のスクリーンショットを1枚取得して表示します。
        /// </summary>
        private void UpdateCaptureImage()
        {
            try
            {
                using (var bmp = new Bitmap(_captureArea.Width, _captureArea.Height))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.CopyFromScreen(_captureArea.Left, _captureArea.Top, 0, 0, bmp.Size); }
                    CaptureImageControl.Source = ConvertBitmapToBitmapSource(bmp);
                }
            }
            catch
            {
                // キャプチャエラーが発生した場合 (対象領域が画面外など)、タイマーを停止する
                _captureTimer.Stop();
            }
        }

        /// <summary>
        /// Shift+Qが押された際に、ウィンドウの透過率を「ほぼ非表示」と「設定値」とで切り替えます。
        /// </summary>
        private void ToggleTemporaryHide()
        {
            if (_isTemporarilyHidden)
            {
                // 表示状態に戻す
                this.Opacity = _lastOpacityBeforeHiding;
                OpacitySlider.Value = this.Opacity;
                _isTemporarilyHidden = false;
            }
            else
            {
                // ほぼ非表示の状態にする
                _lastOpacityBeforeHiding = this.Opacity;
                this.Opacity = HIDDEN_OPACITY;
                _isTemporarilyHidden = true;
            }
        }

        #endregion

        #region UI Event Handlers
        private void ChangeAreaButton_Click(object sender, RoutedEventArgs e) { SelectCaptureArea(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { this.Close(); }
        private void ControlBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { this.DragMove(); } }
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.Opacity = e.NewValue;
            _isTemporarilyHidden = false; // ユーザーが明示的に操作したら非表示状態を解除
        }
        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 拡大率は論理ピクセルベースのキャプチャ範囲から計算
            this.Width = (_profile.CaptureArea.Width) * e.NewValue;
            this.Height = (_profile.CaptureArea.Height) * e.NewValue;
        }
        #endregion

        #region Win32 API

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int style);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        /// <summary>
        /// ウィンドウのクリックスルー設定を有効または無効にします。
        /// </summary>
        private void SetClickThrough(bool enabled)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enabled) { SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT); }
            else { SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT); }
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// System.Drawing.BitmapをWPFで表示可能なBitmapSourceに変換します。
        /// </summary>
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Bmp);
                stream.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = stream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // UIスレッド以外からのアクセスに備える
                return bitmapImage;
            }
        }
        #endregion
    }
}