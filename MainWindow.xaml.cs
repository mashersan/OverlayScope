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
    public partial class MainWindow : Window
    {
        #region Fields
        private readonly DispatcherTimer _captureTimer;
        // ★修正: Nullを許容する型に変更
        private System.Drawing.Rectangle? _captureArea = null;
        private bool _isInOperationMode = false;
        private bool _isTemporarilyHidden = false;
        private double _lastOpacityBeforeHiding = 1.0;
        private const double HIDDEN_OPACITY = 0.05;
        private double _dpiScale = 1.0;
        private readonly OverlayProfile _profile;
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
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _dpiScale = source.CompositionTarget.TransformToDevice.M11;
            }
            ApplyProfileSettings();
            EnterTransparentMode();
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
        private void ApplyProfileSettings()
        {
            _captureArea = new System.Drawing.Rectangle(
                (int)(_profile.CaptureArea.X * _dpiScale),
                (int)(_profile.CaptureArea.Y * _dpiScale),
                (int)(_profile.CaptureArea.Width * _dpiScale),
                (int)(_profile.CaptureArea.Height * _dpiScale)
            );
            this.Left = _profile.WindowPosition.X;
            this.Top = _profile.WindowPosition.Y;
            this.Opacity = _profile.OpacityLevel;
            double scale = _profile.ScaleFactor;
            this.Width = _profile.CaptureArea.Width * scale;
            this.Height = _profile.CaptureArea.Height * scale;
            var opacitySlider = (System.Windows.Controls.Slider)FindName("OpacitySlider");
            opacitySlider.Value = this.Opacity;
            var scaleSlider = (System.Windows.Controls.Slider)FindName("ScaleSlider");
            scaleSlider.Value = scale;
        }

        private void UpdateProfileFromCurrentState()
        {
            if (!_captureArea.HasValue) return;

            _profile.CaptureArea = new System.Drawing.Rectangle(
                (int)(_captureArea.Value.X / _dpiScale),
                (int)(_captureArea.Value.Y / _dpiScale),
                (int)(_captureArea.Value.Width / _dpiScale),
                (int)(_captureArea.Value.Height / _dpiScale)
            );
            _profile.WindowPosition = new System.Windows.Point(this.Left, this.Top);
            _profile.OpacityLevel = _isTemporarilyHidden ? _lastOpacityBeforeHiding : this.Opacity;
            var scaleSlider = (System.Windows.Controls.Slider)FindName("ScaleSlider");
            _profile.ScaleFactor = scaleSlider.Value;
        }
        #endregion

        #region Mode Switching & Keyboard Logic
        private void EnterOperationMode()
        {
            if (_isInOperationMode) return;
            SetClickThrough(false);
            var controlBar = (System.Windows.Controls.Border)FindName("ControlBar");
            controlBar.Visibility = Visibility.Visible;
            var highlightBorder = (System.Windows.Controls.Border)FindName("HighlightBorder");
            highlightBorder.Visibility = Visibility.Visible;
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            _isInOperationMode = true;
        }

        private void EnterTransparentMode()
        {
            if (!_isInOperationMode && Background == null) return;
            UpdateProfileFromCurrentState();
            SetClickThrough(true);
            var controlBar = (System.Windows.Controls.Border)FindName("ControlBar");
            controlBar.Visibility = Visibility.Collapsed;
            var highlightBorder = (System.Windows.Controls.Border)FindName("HighlightBorder");
            highlightBorder.Visibility = Visibility.Collapsed;
            this.Background = null;
            _isInOperationMode = false;
            if (!_captureTimer.IsEnabled)
            {
                _captureTimer.Start();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isInOperationMode) return;
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                switch (e.Key)
                {
                    case Key.W: SelectCaptureArea(); e.Handled = true; break;
                    case Key.Q: ToggleTemporaryHide(); e.Handled = true; break;
                    case Key.Up:
                        var scaleSliderUp = (System.Windows.Controls.Slider)FindName("ScaleSlider");
                        scaleSliderUp.Value = Math.Min(scaleSliderUp.Maximum, scaleSliderUp.Value + 0.1);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        var scaleSliderDown = (System.Windows.Controls.Slider)FindName("ScaleSlider");
                        scaleSliderDown.Value = Math.Max(scaleSliderDown.Minimum, scaleSliderDown.Value - 0.1);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        var opacitySliderRight = (System.Windows.Controls.Slider)FindName("OpacitySlider");
                        opacitySliderRight.Value = Math.Min(opacitySliderRight.Maximum, opacitySliderRight.Value + 0.05);
                        e.Handled = true;
                        break;
                    case Key.Left:
                        var opacitySliderLeft = (System.Windows.Controls.Slider)FindName("OpacitySlider");
                        opacitySliderLeft.Value = Math.Max(opacitySliderLeft.Minimum, opacitySliderLeft.Value - 0.05);
                        e.Handled = true;
                        break;
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
        private void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            UpdateCaptureImage();
        }

        private void SelectCaptureArea()
        {
            _captureTimer.Stop();
            this.Visibility = Visibility.Hidden;
            AreaSelectorWindow selector = new AreaSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedArea.Width > 0)
            {
                var rect = selector.SelectedArea;
                _profile.CaptureArea = new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                _profile.WindowPosition = new System.Windows.Point(rect.X, rect.Y);
                ApplyProfileSettings();
            }
            this.Visibility = Visibility.Visible;
            EnterOperationMode();
            this.Activate();
        }

        private void UpdateCaptureImage()
        {
            if (!_captureArea.HasValue) return;

            try
            {
                using (var bmp = new Bitmap(_captureArea.Value.Width, _captureArea.Value.Height))
                {
                    var captureImageControl = (System.Windows.Controls.Image)FindName("CaptureImageControl");
                    using (var g = Graphics.FromImage(bmp)) { g.CopyFromScreen(_captureArea.Value.Left, _captureArea.Value.Top, 0, 0, bmp.Size); }
                    captureImageControl.Source = ConvertBitmapToBitmapSource(bmp);
                }
            }
            catch { }
        }

        private void ToggleTemporaryHide()
        {
            var opacitySlider = (System.Windows.Controls.Slider)FindName("OpacitySlider");
            if (_isTemporarilyHidden)
            {
                this.Opacity = _lastOpacityBeforeHiding;
                opacitySlider.Value = this.Opacity;
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
        private void CloseButton_Click(object sender, RoutedEventArgs e) { this.Close(); }
        private void ControlBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { this.DragMove(); } }
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.Opacity = e.NewValue;
            _isTemporarilyHidden = false;
        }
        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // ★修正: 安全確認（nullチェック）を追加
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