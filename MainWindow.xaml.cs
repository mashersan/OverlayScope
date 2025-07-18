using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OverlayScope.Properties;

namespace OverlayScope
{
    public partial class MainWindow : Window
    {
        #region Fields & Constants
        private const int CAPTURE_INTERVAL_MS = 16;
        private readonly DispatcherTimer _captureTimer;
        private System.Drawing.Rectangle? _captureArea = null;
        private bool _isInOperationMode = false;
        private bool _isTemporarilyHidden = false;
        private double _lastOpacityBeforeHiding = 1.0;
        private const double HIDDEN_OPACITY = 0.05;
        private double _dpiScale = 1.0;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CAPTURE_INTERVAL_MS) };
            _captureTimer.Tick += CaptureTimer_Tick;
        }
        #endregion

        #region Window Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _dpiScale = source.CompositionTarget.TransformToDevice.M11;
            }
            EnterTransparentMode();
            HandleStartupSettings();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            EnterOperationMode();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_isInOperationMode)
            {
                EnterTransparentMode();
            }
        }
        #endregion

        #region Settings Logic
        private void HandleStartupSettings()
        {
            if (Settings.Default.IsFirstLaunch)
            {
                MessageBox.Show("最初にキャプチャする範囲をドラッグして選択してください。\n\nヒント: Alt+Tabキーでウィンドウを選択すると操作モードになります。", "OverlayScopeへようこそ", MessageBoxButton.OK, MessageBoxImage.Information);
                SelectCaptureArea();
            }
            else
            {
                MessageBoxResult result = MessageBox.Show("前回の設定を読み込みますか？", "設定の読み込み", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    ApplySavedSettings();
                    _captureTimer.Start();
                }
                else
                {
                    MessageBox.Show("キャプチャする範囲をドラッグして選択してください。", "範囲選択", MessageBoxButton.OK, MessageBoxImage.Information);
                    SelectCaptureArea();
                }
            }
        }

        private void ApplySavedSettings()
        {
            double scale = Settings.Default.ScaleFactor;
            _captureArea = new System.Drawing.Rectangle(
                (int)Settings.Default.CaptureAreaLeft,
                (int)Settings.Default.CaptureAreaTop,
                (int)Settings.Default.CaptureAreaWidth,
                (int)Settings.Default.CaptureAreaHeight
            );
            this.Left = Settings.Default.WindowPositionLeft;
            this.Top = Settings.Default.WindowPositionTop;
            this.Opacity = Settings.Default.OpacityLevel;
            this.Width = (_captureArea.Value.Width / _dpiScale) * scale;
            this.Height = (_captureArea.Value.Height / _dpiScale) * scale;
            OpacitySlider.Value = this.Opacity;
            ScaleSlider.Value = scale;
        }

        private void SaveSettings()
        {
            if (this.IsLoaded && _captureArea.HasValue)
            {
                Settings.Default.CaptureAreaLeft = _captureArea.Value.Left;
                Settings.Default.CaptureAreaTop = _captureArea.Value.Top;
                Settings.Default.CaptureAreaWidth = _captureArea.Value.Width;
                Settings.Default.CaptureAreaHeight = _captureArea.Value.Height;
                Settings.Default.WindowPositionLeft = this.Left;
                Settings.Default.WindowPositionTop = this.Top;
                Settings.Default.OpacityLevel = _isTemporarilyHidden ? _lastOpacityBeforeHiding : this.Opacity;
                Settings.Default.ScaleFactor = ScaleSlider.Value;
                Settings.Default.IsFirstLaunch = false;
                Settings.Default.Save();
            }
        }
        #endregion

        #region Mode Switching & Keyboard Logic
        private void EnterOperationMode()
        {
            if (_isInOperationMode) return;
            SetClickThrough(false);
            ControlBar.Visibility = Visibility.Visible;
            HighlightBorder.Visibility = Visibility.Visible;
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            _isInOperationMode = true;
        }

        private void EnterTransparentMode()
        {
            if (!_isInOperationMode && Background == null) return;
            SetClickThrough(true);
            ControlBar.Visibility = Visibility.Collapsed;
            HighlightBorder.Visibility = Visibility.Collapsed;
            this.Background = null;
            _isInOperationMode = false;

            if (!_captureTimer.IsEnabled && _captureArea.HasValue)
            {
                _captureTimer.Start();
            }
        }

        // Ctrlキーによるトグルは廃止したため、KeyDownとKeyUpは不要

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
                    case Key.E:
                        MessageBoxResult result = MessageBox.Show("アプリケーションを終了しますか？", "終了の確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes) { this.Close(); }
                        e.Handled = true;
                        break;
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
        private void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            UpdateCaptureImage();
        }

        private void SelectCaptureArea()
        {
            SaveSettings();
            _captureTimer.Stop();
            this.Visibility = Visibility.Hidden;

            AreaSelectorWindow selector = new AreaSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedArea.Width > 0)
            {
                var rect = selector.SelectedArea;
                _captureArea = new System.Drawing.Rectangle(
                    (int)(rect.X * _dpiScale), (int)(rect.Y * _dpiScale),
                    (int)(rect.Width * _dpiScale), (int)(rect.Height * _dpiScale)
                );

                this.Left = rect.X; this.Top = rect.Y;
                this.Width = rect.Width; this.Height = rect.Height;

                OpacitySlider.Value = 1.0; ScaleSlider.Value = 1.0;

                UpdateCaptureImage();
            }
            else
            {
                if (_captureArea.HasValue) { _captureTimer.Start(); }
                else { this.Close(); }
            }
            this.Visibility = Visibility.Visible;
            EnterOperationMode();

            // ★★★ ここが修正箇所です ★★★
            // 処理が一段落したタイミングで、強制的にフォーカスを設定する
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Keyboard.Focus(this);
            }), DispatcherPriority.Input);
        }

        private void UpdateCaptureImage()
        {
            if (_captureArea == null) return;
            try
            {
                using (var bmp = new Bitmap(_captureArea.Value.Width, _captureArea.Value.Height))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.CopyFromScreen(_captureArea.Value.Left, _captureArea.Value.Top, 0, 0, bmp.Size); }
                    CaptureImageControl.Source = ConvertBitmapToBitmapSource(bmp);
                }
            }
            catch (Exception ex)
            {
                _captureTimer.Stop();
                MessageBox.Show($"画面のキャプチャ中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleTemporaryHide()
        {
            if (_isTemporarilyHidden)
            {
                this.Opacity = _lastOpacityBeforeHiding;
                OpacitySlider.Value = this.Opacity;
                _isTemporarilyHidden = false;
            }
            else
            {
                _lastOpacityBeforeHiding = this.Opacity;
                this.Opacity = HIDDEN_OPACITY;
                _isTemporarilyHidden = true;
            }
        }
        #endregion

        #region UI Event Handlers
        private void ChangeAreaButton_Click(object sender, RoutedEventArgs e) { SelectCaptureArea(); }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("アプリケーションを終了しますか？", "終了の確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) { this.Close(); }
        }

        private void ControlBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { this.DragMove(); } }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.Opacity = e.NewValue;
            _isTemporarilyHidden = false;
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_captureArea.HasValue)
            {
                this.Width = (_captureArea.Value.Width / _dpiScale) * e.NewValue;
                this.Height = (_captureArea.Value.Height / _dpiScale) * e.NewValue;
            }
        }
        #endregion

        #region Win32 API
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int style);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        private void SetClickThrough(bool enabled)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (enabled) { SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT); }
            else { SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT); }
        }
        #endregion

        #region Helper Methods
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Bmp); stream.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit(); bitmapImage.StreamSource = stream; bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit(); bitmapImage.Freeze();
                return bitmapImage;
            }
        }
        #endregion
    }
}