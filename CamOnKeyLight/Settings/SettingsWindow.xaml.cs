using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    public sealed class CheckItem : INotifyPropertyChanged
    {
        private bool isChecked;

        public CheckItem(string name, bool isChecked)
        {
            Name = name;
            this.isChecked = isChecked;
        }

        public string Name { get; }

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked == value)
                    return;

                isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class SettingsWindow : Window
    {
        private readonly AppUserSettings settings;
        private readonly RelayCredentials credentials;
        private readonly CheckBox[] days;
        private readonly ObservableCollection<CheckItem> apps = new ObservableCollection<CheckItem>();
        private readonly ObservableCollection<CheckItem> groups = new ObservableCollection<CheckItem>();

        public SettingsWindow(AppUserSettings settings, IReadOnlyList<string> groupNames, IReadOnlyList<string> observedApps, RelayCredentials credentials)
        {
            this.settings = settings;
            this.credentials = credentials;
            InitializeComponent();
            days = new CheckBox[QuietHours.DayTokens.Length];
            bool[] selectedDays = QuietHours.ParseDays(settings.QuietDays);
            for (int i = 0; i < days.Length; i++)
            {
                days[i] = new CheckBox { Content = QuietHours.DayTokens[i], IsChecked = selectedDays[i], Margin = new Thickness(0, 0, 12, 0) };
                DayPanel.Children.Add(days[i]);
            }

            QuietEnabled.IsChecked = settings.QuietHoursEnabled;
            QuietStart.Text = settings.QuietStart;
            QuietEnd.Text = settings.QuietEnd;
            CameraAppFilter filter = CameraAppFilterPreference.Parse(settings.CameraAppMode);
            ModeAll.IsChecked = filter == CameraAppFilter.All;
            ModeOnly.IsChecked = filter == CameraAppFilter.Only;
            ModeExcept.IsChecked = filter == CameraAppFilter.Except;
            foreach (string name in CameraAppNames.Clean(settings.KnownCameraApps.Concat(settings.SelectedCameraApps).Concat(observedApps)))
            {
                bool selected = settings.SelectedCameraApps.Any(app => app.Equals(name, StringComparison.OrdinalIgnoreCase));
                apps.Add(new CheckItem(name, selected));
            }

            AppList.ItemsSource = apps;
            AllGroups.IsChecked = settings.AllGroupsFollowCamera;
            foreach (string name in LightGrouping.Clean(groupNames.Concat(settings.CameraGroups)))
            {
                bool selected = settings.CameraGroups.Any(group => group.Equals(name, StringComparison.OrdinalIgnoreCase));
                groups.Add(new CheckItem(name, selected));
            }

            GroupList.ItemsSource = groups;
            GroupList.IsEnabled = settings.AllGroupsFollowCamera == false;
            NotifyPresence.IsChecked = settings.NotifyLightPresence;
            NotifyUpdate.IsChecked = settings.NotifyOnUpdate;
            RelayUrl.Text = settings.RelayUrl;
            ClientId.Text = credentials.ClientId;
            ClientSecret.Secret = credentials.ClientSecret;
            SharedSecret.Secret = credentials.SharedSecret;
            ClientSecret.FieldName = "Access client secret";
            SharedSecret.FieldName = "Relay shared secret";
            UseRelay.IsChecked = !string.IsNullOrWhiteSpace(settings.RelayUrl);
            RelayFields.IsEnabled = UseRelay.IsChecked == true;
        }

        public RelayCredentials Credentials => credentials;

        public void Select(string page)
        {
            RadioButton? match = page switch
            {
                "apps" => AppsRadio,
                "groups" => GroupsRadio,
                "notifications" => NotificationsRadio,
                "relay" => RelayRadio,
                "about" => AboutRadio,
                _ => QuietRadio
            };
            match.IsChecked = true;
            Show(page);
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.IsChecked == true)
                Show(radio.Tag as string ?? "quiet");
        }

        private void Show(string page)
        {
            if (QuietPage == null)
                return;

            QuietPage.Visibility = page == "quiet" ? Visibility.Visible : Visibility.Collapsed;
            AppsPage.Visibility = page == "apps" ? Visibility.Visible : Visibility.Collapsed;
            GroupsPage.Visibility = page == "groups" ? Visibility.Visible : Visibility.Collapsed;
            NotificationsPage.Visibility = page == "notifications" ? Visibility.Visible : Visibility.Collapsed;
            RelayPage.Visibility = page == "relay" ? Visibility.Visible : Visibility.Collapsed;
            AboutPage.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AllGroups_Changed(object sender, RoutedEventArgs e)
        {
            if (GroupList != null)
                GroupList.IsEnabled = AllGroups.IsChecked != true;
        }

        private void UseRelay_Changed(object sender, RoutedEventArgs e)
        {
            if (RelayFields != null)
                RelayFields.IsEnabled = UseRelay.IsChecked == true;
        }

        private void AddApp_Click(object sender, RoutedEventArgs e)
        {
            string name = CameraAppNames.Friendly(AppName.Text);
            if (name.Length == 0)
                return;

            CheckItem? existing = apps.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                existing.IsChecked = true;
            else
                apps.Add(new CheckItem(name, true));
            AppName.Clear();
        }

        private void RemoveApp_Click(object sender, RoutedEventArgs e)
        {
            if (AppList.SelectedItem is CheckItem item)
                apps.Remove(item);
        }

        private async void TestRelay_Click(object sender, RoutedEventArgs e)
        {
            TestRelay.IsEnabled = false;
            RelayError.Text = "";
            ShowRelayResult("Checking", "Checking the relay…", false);
            try
            {
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                string text = await RelayProbe.DescribeAsync(RelayUrl.Text, CurrentCredentials(), client, CancellationToken.None);
                bool ok = IsRelaySuccess(text);
                ShowRelayResult(ok ? "Connected" : "Not connected", text + " This test does not switch the lights.", ok);
            }
            catch (Exception ex)
            {
                ShowRelayResult("Not connected", "The relay did not answer.", false);
                AppLog.Write("relay", "test", ex.GetType().Name);
            }
            finally
            {
                TestRelay.IsEnabled = true;
            }
        }

        private void ShowRelayResult(string title, string detail, bool ok)
        {
            RelayResult.Visibility = Visibility.Visible;
            RelayTitle.Text = title;
            RelayDetail.Text = detail;
            Brush accent = (Brush)FindResource(ok ? "StatusOnBrush" : "ErrorBrush");
            RelayTitle.Foreground = accent;
            RelayResult.BorderBrush = accent;
            RelayResult.Background = (Brush)FindResource(ok ? "PillOnBrush" : "PillOffBrush");
        }

        private static bool IsRelaySuccess(string text)
        {
            return text.Contains("answered", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("did not", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("refused", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("HTTP", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("not valid", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("Enter the relay", StringComparison.OrdinalIgnoreCase);
        }

        private RelayCredentials CurrentCredentials()
        {
            return new RelayCredentials
            {
                ClientId = ClientId.Text.Trim(),
                ClientSecret = ClientSecret.Secret,
                SharedSecret = SharedSecret.Secret
            };
        }

        private void RelayGuide_Click(object sender, RoutedEventArgs e)
        {
            ProjectLinks.Open(ProjectLinks.RelayGuide);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!QuietHours.TryParseTime(QuietStart.Text, out int start) || !QuietHours.TryParseTime(QuietEnd.Text, out int end))
            {
                QuietError.Text = "Use 24-hour times such as 22:00 and 07:00.";
                Select("quiet");
                QuietStart.Focus();
                return;
            }

            QuietError.Text = "";
            settings.QuietHoursEnabled = QuietEnabled.IsChecked == true;
            settings.QuietStart = QuietHours.FormatTime(start);
            settings.QuietEnd = QuietHours.FormatTime(end);
            bool[] selectedDays = new bool[days.Length];
            for (int i = 0; i < days.Length; i++)
                selectedDays[i] = days[i].IsChecked == true;
            settings.QuietDays = QuietHours.FormatDays(selectedDays);
            settings.CameraAppMode = ModeOnly.IsChecked == true
                ? CameraAppFilter.Only.ToString()
                : ModeExcept.IsChecked == true ? CameraAppFilter.Except.ToString() : CameraAppFilter.All.ToString();
            settings.KnownCameraApps = apps.Select(item => item.Name).Where(name => name.Length > 0).ToList();
            settings.SelectedCameraApps = apps.Where(item => item.IsChecked).Select(item => item.Name).ToList();
            settings.AllGroupsFollowCamera = AllGroups.IsChecked == true;
            settings.CameraGroups = groups.Where(item => item.IsChecked).Select(item => item.Name).ToList();
            settings.NotifyLightPresence = NotifyPresence.IsChecked == true;
            settings.NotifyOnUpdate = NotifyUpdate.IsChecked == true;

            if (HasLineBreak(ClientId.Text) || HasLineBreak(ClientSecret.Secret) || HasLineBreak(SharedSecret.Secret))
            {
                RelayError.Text = "A credential contains a line break.";
                Select("relay");
                return;
            }

            if (UseRelay.IsChecked == true)
            {
                if (!RelayUrls.TryNormalize(RelayUrl.Text, out string relayUrl, out string? relayProblem) || relayUrl.Length == 0)
                {
                    RelayError.Text = relayUrl.Length == 0 && relayProblem == null
                        ? "Enter the relay address, or turn off Use a home relay."
                        : relayProblem ?? "The relay address is not valid.";
                    Select("relay");
                    RelayUrl.Focus();
                    return;
                }

                settings.RelayUrl = relayUrl;
            }
            else
            {
                settings.RelayUrl = "";
            }

            credentials.ClientId = ClientId.Text.Trim();
            credentials.ClientSecret = ClientSecret.Secret;
            credentials.SharedSecret = SharedSecret.Secret;
            DialogResult = true;
        }

        private static bool HasLineBreak(string value)
        {
            return value.Contains('\r') || value.Contains('\n');
        }
    }
}
