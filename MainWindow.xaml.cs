using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OverlayScope.Properties;

namespace OverlayScope
{
    /// <summary>
    /// メインのオーバーレイウィンドウのロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields & Constants

        private const int CAPTURE_INTERVAL_MS = 16; // 約60fpsで画面をキャプチャする間隔
        private readonly DispatcherTimer _captureTimer; // 画面キャプチャを定期的に実行するタイマー

        private System.Drawing.Rectangle? _captureArea = null; // キャプチャ対象の画面領域
        private bool _isInOperationMode = false; // 現在が操作モードかどうかを保持するフラグ
        private bool _isCtrlToggleCandidate = false; // Ctrlキーの単独押しを判定するためのフラグ

        // Shift+Qによる一時的な非表示状態を管理するための変数
        private bool _isTemporarilyHidden = false;
        private double _lastOpacityBeforeHiding = 1.0;
        private const double HIDDEN_OPACITY = 0.05; // 一時的に非表示にする際の透過率(5%)

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            // 画面キャプチャ用のタイマーを初期化
            _captureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CAPTURE_INTERVAL_MS) };
            _captureTimer.Tick += CaptureTimer_Tick;
        }

        #endregion

        #region Window Events

        /// <summary>
        /// ウィンドウがロードされた時の初期化処理
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 起動時はクリックできない透過モードから開始
            EnterTransparentMode();
            // 保存された設定の読み込み、または初回起動の処理を実行
            HandleStartupSettings();
        }

        /// <summary>
        /// ウィンドウが非アクティブになった時の処理
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 操作モード中に他のウィンドウをクリックした場合などに、安全のために透過モードに戻す
            if (_isInOperationMode)
            {
                EnterTransparentMode();
            }
        }

        #endregion

        #region Settings Logic

        /// <summary>
        /// アプリ起動時の設定処理。初回か2回目以降かで動作を分岐します。
        /// </summary>
        private void HandleStartupSettings()
        {
            // 初回起動の場合
            if (Settings.Default.IsFirstLaunch)
            {
                MessageBox.Show("最初にキャプチャする範囲をドラッグして選択してください。\n\nヒント: ウィンドウを選択後、Ctrlキーで操作モードを切り替えられます。", "OverlayScopeへようこそ", MessageBoxButton.OK, MessageBoxImage.Information);
                SelectCaptureArea();
            }
            // 2回目以降の起動の場合
            else
            {
                MessageBoxResult result = MessageBox.Show("前回の設定を読み込みますか？", "設定の読み込み", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // 「はい」が選択されたら、保存された設定を適用
                    ApplySavedSettings();
                    _captureTimer.Start();
                }
                else
                {
                    // 「いいえ」が選択されたら、新しく範囲を選択
                    MessageBox.Show("キャプチャする範囲をドラッグして選択してください。", "範囲選択", MessageBoxButton.OK, MessageBoxImage.Information);
                    SelectCaptureArea();
                }
            }
        }

        /// <summary>
        /// 保存された設定を読み込み、ウィンドウの状態を復元します。
        /// </summary>
        private void ApplySavedSettings()
        {
            // 設定ファイルから各値を読み込む
            _captureArea = new System.Drawing.Rectangle(
                (int)Settings.Default.CaptureAreaLeft,
                (int)Settings.Default.CaptureAreaTop,
                (int)Settings.Default.CaptureAreaWidth,
                (int)Settings.Default.CaptureAreaHeight);
            this.Left = Settings.Default.WindowPositionLeft;
            this.Top = Settings.Default.WindowPositionTop;
            this.Opacity = Settings.Default.OpacityLevel;
            double scale = Settings.Default.ScaleFactor;

            // ウィンドウとスライダーのサイズ・値を設定
            this.Width = _captureArea.Value.Width * scale;
            this.Height = _captureArea.Value.Height * scale;
            OpacitySlider.Value = this.Opacity;
            ScaleSlider.Value = scale;
        }

        /// <summary>
        /// 現在のウィンドウの状態をファイルに保存します。
        /// </summary>
        private void SaveSettings()
        {
            // ウィンドウがロード済みで、キャプチャ範囲が存在する場合のみ保存
            if (this.IsLoaded && _captureArea.HasValue)
            {
                // 現在の状態をSettingsオブジェクトに書き込む
                Settings.Default.CaptureAreaLeft = _captureArea.Value.Left;
                Settings.Default.CaptureAreaTop = _captureArea.Value.Top;
                Settings.Default.CaptureAreaWidth = _captureArea.Value.Width;
                Settings.Default.CaptureAreaHeight = _captureArea.Value.Height;
                Settings.Default.WindowPositionLeft = this.Left;
                Settings.Default.WindowPositionTop = this.Top;
                // 一時的に非表示状態の場合は、その前の透過率を保存する
                Settings.Default.OpacityLevel = _isTemporarilyHidden ? _lastOpacityBeforeHiding : this.Opacity;
                Settings.Default.ScaleFactor = ScaleSlider.Value;

                // 次回起動時が初回起動でないことを記録
                Settings.Default.IsFirstLaunch = false;

                // ファイルに保存
                Settings.Default.Save();
            }
        }

        #endregion

        #region Mode Switching & Keyboard Logic

        /// <summary>
        /// ユーザーが操作できる「操作モード」に切り替えます。
        /// </summary>
        private void EnterOperationMode()
        {
            if (_isInOperationMode) return; // 既に操作モードなら何もしない

            SetClickThrough(false); // クリック透過を解除
            ControlBar.Visibility = Visibility.Visible; // 上部操作バーを表示
            HighlightBorder.Visibility = Visibility.Visible; // 黄色い枠線を表示
            this.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)); // ドラッグ移動のためのごく薄い背景
            _isInOperationMode = true;
        }

        /// <summary>
        /// クリックが背面に透過される「透過モード」に切り替えます。
        /// </summary>
        private void EnterTransparentMode()
        {
            if (!_isInOperationMode && Background == null) return; // 既に透過モードなら何もしない

            SetClickThrough(true); // クリック透過を有効に
            ControlBar.Visibility = Visibility.Collapsed; // 上部操作バーを非表示
            HighlightBorder.Visibility = Visibility.Collapsed; // 黄色い枠線を非表示
            this.Background = null; // 背景を完全に無くす
            _isInOperationMode = false;
        }

        /// <summary>
        /// Ctrlキーが押されたことを検知し、トグル操作の候補とします。
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // キーの長押しによる連続発生は無視する
            if (!e.IsRepeat && (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl))
            {
                // Ctrl以外の修飾キーが押されていないことを確認
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    _isCtrlToggleCandidate = true;
                }
            }
        }

        /// <summary>
        /// Ctrlキーが離されたことを検知し、単独押しだった場合にモードを切り替えます。
        /// </summary>
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (_isCtrlToggleCandidate && (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl))
            {
                // モードをトグル（反転）させる
                if (_isInOperationMode) EnterTransparentMode();
                else EnterOperationMode();
            }
            // 候補状態をリセット
            _isCtrlToggleCandidate = false;
        }

        /// <summary>
        /// 操作モード中の各種キーボードショートカットを処理します。
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 操作モード中でなければ、ショートカットは無効
            if (!_isInOperationMode) return;

            // --- Shiftキーが押されている場合の処理 ---
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                switch (e.Key)
                {
                    case Key.W: // 範囲の再設定
                        SelectCaptureArea();
                        e.Handled = true; // 他のコントロールにキーイベントが渡らないようにする
                        break;
                    case Key.Q: // 表示のON/OFF（透過率の切替）
                        ToggleTemporaryHide();
                        e.Handled = true;
                        break;
                    case Key.E: // アプリの終了
                        MessageBoxResult result = MessageBox.Show("アプリケーションを終了しますか？", "終了の確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes) { this.Close(); }
                        e.Handled = true;
                        break;
                    case Key.Up: ScaleSlider.Value = Math.Min(ScaleSlider.Maximum, ScaleSlider.Value + 0.1); e.Handled = true; break;
                    case Key.Down: ScaleSlider.Value = Math.Max(ScaleSlider.Minimum, ScaleSlider.Value - 0.1); e.Handled = true; break;
                    case Key.Right: OpacitySlider.Value = Math.Min(OpacitySlider.Maximum, OpacitySlider.Value + 0.05); e.Handled = true; break;
                    case Key.Left: OpacitySlider.Value = Math.Max(OpacitySlider.Minimum, OpacitySlider.Value - 0.05); e.Handled = true; break;
                }
            }
            // --- Shiftキーが押されていない場合の処理 ---
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    // ウィンドウ位置の微調整
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
        /// 画面のキャプチャと表示の更新を定期的に行います。
        /// </summary>
        private void CaptureTimer_Tick(object? sender, EventArgs e)
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

        /// <summary>
        /// 範囲選択ウィンドウを表示し、キャプチャ範囲を設定します。
        /// </summary>
        private void SelectCaptureArea()
        {
            SaveSettings(); // 範囲選択前に現在の設定を一度保存する
            _captureTimer.Stop();
            this.Visibility = Visibility.Hidden;

            AreaSelectorWindow selector = new AreaSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedArea.Width > 0)
            {
                // 新しい範囲が選択された場合
                var rect = selector.SelectedArea;
                _captureArea = new System.Drawing.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                this.Left = rect.X; this.Top = rect.Y; this.Width = rect.Width; this.Height = rect.Height;
                OpacitySlider.Value = 1.0; ScaleSlider.Value = 1.0;
                _captureTimer.Start();
            }
            else
            {
                // 範囲選択がキャンセルされた場合
                if (_captureArea.HasValue)
                {
                    // 前のキャプチャ範囲があれば、キャプチャを再開
                    _captureTimer.Start();
                }
                else
                {
                    // 前の範囲すらない（初回起動でキャンセルした）場合はアプリを閉じる
                    this.Close();
                }
            }
            this.Visibility = Visibility.Visible;
            // 範囲選択後は、すぐに調整ができるよう操作モードで開始する
            EnterOperationMode();
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
                OpacitySlider.Value = this.Opacity; // スライダーの値も復元
                _isTemporarilyHidden = false;
            }
            else
            {
                // ほぼ非表示の状態にする
                _lastOpacityBeforeHiding = this.Opacity; // 現在の透過率を記憶
                this.Opacity = HIDDEN_OPACITY; // 透過率を5%に設定
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
            _isTemporarilyHidden = false; // ユーザーが明示的に操作したら非表示状態を解除
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_captureArea.HasValue) { this.Width = _captureArea.Value.Width * e.NewValue; this.Height = _captureArea.Value.Height * e.NewValue; }
        }
        #endregion

        #region Win32 API
        // Win32 APIの定数を定義
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;

        // Win32 API関数をインポート
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