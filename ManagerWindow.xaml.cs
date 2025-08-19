using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace OverlayScope
{
    /// <summary>
    /// 複数のオーバーレイプロファイルを管理するためのメインウィンドウ。
    /// </summary>
    public partial class ManagerWindow : Window
    {
        #region Win32 API

        /// <summary>
        /// 指定されたウィンドウをフォアグラウンドにし、アクティブ化するためのWin32 API関数。
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(System.IntPtr hWnd);

        #endregion

        /// <summary>
        /// UIにバインドされるプロファイルのコレクション。
        /// </summary>
        public ObservableCollection<OverlayProfile> Profiles { get; set; } = new();

        public ManagerWindow()
        {
            InitializeComponent();
            LoadProfiles();
            this.DataContext = this; // XAMLがこのクラスのプロパティを参照できるように設定
        }

        /// <summary>
        /// JSONファイルからプロファイルの一覧を読み込みます。
        /// </summary>
        private void LoadProfiles()
        {
            var loadedProfiles = SettingsManager.LoadProfiles();
            Profiles = new ObservableCollection<OverlayProfile>(loadedProfiles);
        }

        /// <summary>
        /// 「新規作成」ボタンがクリックされた時の処理。
        /// </summary>
        private void CreateNew_Click(object sender, RoutedEventArgs e)
        {
            var newProfile = new OverlayProfile();

            // 範囲選択中は管理ウィンドウを非表示にする
            this.Hide();
            AreaSelectorWindow selector = new AreaSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedArea.Width > 0)
            {
                // 選択された範囲で新しいプロファイルを作成
                newProfile.CaptureArea = new System.Drawing.Rectangle(
                    (int)selector.SelectedArea.X, (int)selector.SelectedArea.Y,
                    (int)selector.SelectedArea.Width, (int)selector.SelectedArea.Height);

                newProfile.WindowPosition = new System.Windows.Point(selector.SelectedArea.X, selector.SelectedArea.Y);

                Profiles.Add(newProfile);
                newProfile.IsActive = true;
                ToggleOverlay(newProfile); // 新しいウィンドウを表示
                ActivateOverlay(newProfile); // 作成したウィンドウをアクティブ化
            }
            // 範囲選択が終わったら管理ウィンドウを再表示
            this.Show();
        }

        /// <summary>
        /// 「ON/OFF」ボタンがクリックされた時の処理。
        /// </summary>
        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.Tag is OverlayProfile profile)
            {
                profile.IsActive = toggleButton.IsChecked ?? false;
                ToggleOverlay(profile);
            }
        }

        /// <summary>
        /// 「設定変更」ボタンがクリックされた時の処理。
        /// </summary>
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is OverlayProfile profile)
            {
                ActivateOverlay(profile);
            }
        }

        /// <summary>
        /// 「削除」ボタンがクリックされた時の処理。
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is OverlayProfile profile)
            {
                if (MessageBox.Show($"「{profile.Name}」を削除しますか？", "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    // もし表示中なら、まずウィンドウを閉じる
                    if (profile.IsActive)
                    {
                        profile.IsActive = false;
                        ToggleOverlay(profile);
                    }
                    Profiles.Remove(profile);
                }
            }
        }

        /// <summary>
        /// プロファイルの状態に基づいてオーバーレイウィンドウの表示/非表示を切り替えます。
        /// </summary>
        private void ToggleOverlay(OverlayProfile profile)
        {
            if (profile.IsActive)
            {
                // ウィンドウがまだ存在しない場合のみ、新しく作成する
                if (profile.ActiveWindow == null)
                {
                    var overlay = new MainWindow(profile);
                    overlay.Owner = this; // 管理ウィンドウをオーナーに設定
                    profile.ActiveWindow = overlay;
                    overlay.Show();
                }
            }
            else
            {
                // ウィンドウを閉じる
                profile.ActiveWindow?.Close();
                profile.ActiveWindow = null;
            }
        }

        /// <summary>
        /// 指定されたプロファイルのオーバーレイウィンドウをアクティブ化します。
        /// </summary>
        private void ActivateOverlay(OverlayProfile profile)
        {
            if (profile.IsActive && profile.ActiveWindow != null)
            {
                var helper = new WindowInteropHelper(profile.ActiveWindow);
                if (helper.Handle != System.IntPtr.Zero)
                {
                    SetForegroundWindow(helper.Handle);
                }
            }
        }

        /// <summary>
        /// 管理ウィンドウが閉じられる時の処理。
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 現在のプロファイル一覧をファイルに保存
            SettingsManager.SaveProfiles(Profiles.ToList());

            // 開いているすべてのオーバーレイウィンドウを閉じる
            foreach (var profile in Profiles.ToList())
            {
                profile.ActiveWindow?.Close();
            }
        }
    }
}