using System.Windows;

namespace OverlayScope
{
    /// <summary>
    /// ダイアログの結果（どのボタンが押されたか）を示すためのenum。
    /// </summary>
    public enum UpdateActionResult
    {
        None,
        GoToGitHub,
        GoToWebsite
    }

    /// <summary>
    /// 更新通知を表示するためのカスタムダイアログウィンドウ。
    /// </summary>
    public partial class UpdateNotificationWindow : Window
    {
        /// <summary>
        /// ユーザーが選択した結果を格納します。
        /// </summary>
        public UpdateActionResult Result { get; private set; } = UpdateActionResult.None;

        /// <summary>
        /// コンストラクタ。バージョンとリリースノートをUIに表示します。
        /// </summary>
        public UpdateNotificationWindow(string version, string releaseNotes)
        {
            InitializeComponent();
            VersionText.Text = version;
            ReleaseNotesText.Text = releaseNotes;
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateActionResult.GoToGitHub;
            this.DialogResult = true; // ShowDialog()がtrueを返すように設定
            this.Close();
        }

        private void WebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateActionResult.GoToWebsite;
            this.DialogResult = true;
            this.Close();
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateActionResult.None;
            this.DialogResult = false; // ShowDialog()がfalseを返すように設定
            this.Close();
        }
    }
}