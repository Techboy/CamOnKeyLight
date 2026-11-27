using System.Windows;
using System.Windows.Controls;

namespace CamOnKeyLight
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
            VersionText.Text = "Version " + AppSession.CurrentVersion().ToString(3);
        }

        private void Documentation_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.Documentation);
        private void Issue_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.ReportIssue);
        private void Releases_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.ReleaseNotes);
        private void GitHub_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.GitHubProject);
        private void Elgato_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.ElgatoWebsite);
    }
}
