using System.Diagnostics;

namespace CamOnKeyLight
{
    internal static class ProjectLinks
    {
        public const string Documentation = "https://github.com/Techboy/CamOnKeyLight#readme";
        public const string ReportIssue = "https://github.com/Techboy/CamOnKeyLight/issues/new";
        public const string ReleaseNotes = "https://github.com/Techboy/CamOnKeyLight/releases";
        public const string GitHubProject = "https://github.com/Techboy/CamOnKeyLight";
        public const string ElgatoWebsite = "https://www.elgato.com/";

        public static void Open(string url)
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
    }
}
