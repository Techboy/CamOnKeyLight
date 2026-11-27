using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    public partial class MainWindow : Window
    {
        private readonly AppSession session;
        private readonly TrayHost tray;
        private readonly ObservableCollection<LightCard> cards = new ObservableCollection<LightCard>();
        private readonly string[] launchArgs;
        private bool applying;
        private bool allowExit;
        private EffectiveTheme? appliedTheme;

        public MainWindow(string[]? args = null)
        {
            launchArgs = args ?? Array.Empty<string>();
            InitializeComponent();
            OnDelay.Maximum = DelayUnits.MaxOnSeconds;
            OffDelay.Maximum = DelayUnits.MaxOffSeconds;
            LightList.ItemsSource = cards;
            session = new AppSession(Dispatcher, launchArgs);
            OnDelay.SecondsChanged += (_, _) => { if (!applying) session.SetOnDelaySeconds(OnDelay.Seconds); };
            OffDelay.SecondsChanged += (_, _) => { if (!applying) session.SetOffDelaySeconds(OffDelay.Seconds); };
            tray = new TrayHost(() => session.Groups());
            session.UiChanged += RefreshUi;
            session.ExitRequested += RequestExit;
            session.Balloon += (title, body, info, milliseconds) => tray.ShowBalloon(title, body, info, milliseconds);
            tray.OpenRequested += ShowMain;
            tray.ExitRequested += RequestExit;
            tray.ModeRequested += mode => session.SetMode(mode);
            tray.SettingsRequested += () => ShowSettings(null);
            tray.ShortcutsRequested += ShowShortcuts;
            tray.FindRequested += () => FindLights(fromTray: true);
            tray.GroupCommandRequested += (addresses, body) => session.SendGroup(addresses, body);
        }

        public bool StartHidden => session.StartHidden;

        public void Start()
        {
            IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
            session.Start(handle);
            ShowInTaskbar = !session.StartHidden;
            if (session.ScreenshotDirectory != null)
                Dispatcher.BeginInvoke(CaptureScreenshots, DispatcherPriority.ApplicationIdle);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!allowExit && !App.IsSessionEnding && !Environment.HasShutdownStarted)
            {
                e.Cancel = true;
                ShowInTaskbar = false;
                Hide();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            tray.Dispose();
            session.Dispose();
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && session.HandleHotkey(wParam.ToInt32()))
                handled = true;
            return IntPtr.Zero;
        }

        private void RefreshUi()
        {
            if (applying)
                return;

            applying = true;
            try
            {
                Title = session.WindowTitle;
                HeroHeadline.Text = session.HeroHeadline;
                HeroReason.Text = session.HeroReason;
                ModePill.Text = session.ModePill;
                ApplyPill(session.Mode);
                AutoMode.IsChecked = session.Mode == LightMode.Auto;
                OnMode.IsChecked = session.Mode == LightMode.On;
                OffMode.IsChecked = session.Mode == LightMode.Off;
                OnDelay.SetSeconds(session.OnDelaySeconds);
                OffDelay.SetSeconds(session.OffDelaySeconds);
                StartWithWindows.IsChecked = session.StartWithWindows;
                ThemeDark.IsChecked = session.Theme == ThemePreference.Dark;
                ThemeLight.IsChecked = session.Theme == ThemePreference.Light;
                ThemeSystem.IsChecked = session.Theme == ThemePreference.FollowWindows;
                AutomaticUpdates.IsChecked = session.AutomaticUpdates;
                UpdateStatus.Text = session.UpdateStatus;
                UpdateStatus.Foreground = (Brush)FindResource(session.UpdateProblem ? "ErrorBrush" : "MutedBrush");
                CheckUpdatesButton.IsEnabled = session.CheckEnabled;
                InstallUpdateButton.IsEnabled = session.InstallEnabled;
                RetryButton.IsEnabled = session.RetryEnabled;
                ShortcutsButton.Content = session.ShortcutButtonText;
                LightCount.Text = session.LightCount == 1 ? "1 light" : session.LightCount + " lights";
                EmptyLights.Visibility = session.LightCount == 0 ? Visibility.Visible : Visibility.Collapsed;
                ProblemDetail.Text = session.ProblemDetail;
                ProblemDetail.Visibility = session.HasProblem ? Visibility.Visible : Visibility.Collapsed;
                bool lit = session.SummaryKind == LightSummaryKind.AllOn;
                Brush lamp = (Brush)FindResource(lit ? "AccentBrush" : "InputBrush");
                HeroLamp1.Background = lamp;
                HeroLamp2.Background = lamp;
                SyncLights();
                ApplyChrome();
                tray.Update(session.CameraOn, session.TrayTip, session.Mode, session.QuietMenu, session.AutoShortcut, session.OnShortcut, session.OffShortcut);
            }
            finally
            {
                applying = false;
            }
        }

        private void ApplyPill(LightMode mode)
        {
            string kind = mode switch
            {
                LightMode.On => "On",
                LightMode.Off => "Off",
                _ => "Auto"
            };
            ModePillBorder.Background = (Brush)FindResource("Pill" + kind + "Brush");
            ModePill.Foreground = (Brush)FindResource("Pill" + kind + "TextBrush");
        }

        private void ApplyChrome()
        {
            if (appliedTheme == session.Effective)
                return;

            appliedTheme = session.Effective;
            BrandTheme.Apply(session.Effective);
            TitleBarTheme.Apply(new WindowInteropHelper(this).Handle, session.Effective == EffectiveTheme.Dark);
            tray.ApplyTheme(session.Effective == EffectiveTheme.System ? null : ThemeResolver.PaletteFor(session.Effective));
        }

        private void SyncLights()
        {
            IReadOnlyList<LightCardSnapshot> next = session.Lights;
            string? selected = (LightList.SelectedItem as LightCard)?.Address;
            bool same = cards.Count == next.Count;
            for (int i = 0; same && i < next.Count; i++)
                same = cards[i].Address.Equals(next[i].Address, StringComparison.OrdinalIgnoreCase);

            if (!same)
            {
                cards.Clear();
                foreach (LightCardSnapshot snapshot in next)
                {
                    LightCard card = new LightCard(snapshot.Address);
                    card.Apply(snapshot);
                    cards.Add(card);
                }

                LightList.SelectedItem = cards.FirstOrDefault(card => card.Address.Equals(selected, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                for (int i = 0; i < next.Count; i++)
                    cards[i].Apply(next[i]);
            }
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (applying || sender is not RadioButton radio || radio.IsChecked != true)
                return;

            LightMode mode = radio == OnMode ? LightMode.On : radio == OffMode ? LightMode.Off : LightMode.Auto;
            session.SetMode(mode);
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (applying || sender is not RadioButton radio || radio.IsChecked != true)
                return;

            ThemePreference preference = radio == ThemeLight ? ThemePreference.Light : radio == ThemeSystem ? ThemePreference.FollowWindows : ThemePreference.Dark;
            session.SetTheme(preference);
        }

        private void StartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            if (!applying)
                session.SetStartWithWindows(StartWithWindows.IsChecked == true);
        }

        private void AutomaticUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!applying)
                session.SetAutomaticUpdates(AutomaticUpdates.IsChecked == true);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            LightEditorWindow editor = new LightEditorWindow(null, session.ExistingAddresses());
            Own(editor);
            if (editor.ShowDialog() == true && editor.Result != null)
                session.AddLight(editor.Result);
        }

        private void Find_Click(object sender, RoutedEventArgs e) => FindLights(false);

        private void Retry_Click(object sender, RoutedEventArgs e) => session.RetryLights();

        private void CheckUpdates_Click(object sender, RoutedEventArgs e) => session.CheckForUpdates();

        private void InstallUpdate_Click(object sender, RoutedEventArgs e) => session.InstallUpdate();

        private void Shortcuts_Click(object sender, RoutedEventArgs e) => ShowShortcuts();

        private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings(null);

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                Dispatcher.BeginInvoke(new Action(() => button.ContextMenu.IsOpen = true));
            }
        }

        private void Documentation_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.Documentation);
        private void Issue_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.ReportIssue);
        private void Releases_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.ReleaseNotes);
        private void GitHub_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.GitHubProject);
        private void Elgato_Click(object sender, RoutedEventArgs e) => ProjectLinks.Open(ProjectLinks.ElgatoWebsite);

        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow about = new AboutWindow();
            Own(about);
            about.ShowDialog();
        }

        private void LightActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not string address)
                return;

            ContextMenu menu = new ContextMenu();
            MenuItem edit = new MenuItem { Header = "Edit" };
            edit.Click += (_, _) => Edit(address);
            MenuItem remove = new MenuItem { Header = "Remove" };
            remove.Click += (_, _) => session.RemoveLight(address);
            menu.Items.Add(edit);
            menu.Items.Add(remove);
            button.ContextMenu = menu;
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            Dispatcher.BeginInvoke(new Action(() => menu.IsOpen = true));
            e.Handled = true;
        }

        private void LightList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                RemoveSelected();
                e.Handled = true;
            }
            else if (e.Key is Key.Enter or Key.F2)
            {
                EditSelected();
                e.Handled = true;
            }
        }

        private void LightList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject? cursor = source;
                while (cursor != null)
                {
                    if (cursor is Button)
                        return;
                    cursor = VisualTreeHelper.GetParent(cursor);
                }
            }

            EditSelected();
        }

        private void EditSelected()
        {
            if (LightList.SelectedItem is LightCard card)
                Edit(card.Address);
        }

        private void RemoveSelected()
        {
            if (LightList.SelectedItem is LightCard card)
                session.RemoveLight(card.Address);
        }

        private void Edit(string address)
        {
            LightEditorWindow editor = new LightEditorWindow(session.LightOrNew(address), session.ExistingAddresses());
            Own(editor);
            if (editor.ShowDialog() == true && editor.Result != null)
                session.ReplaceLight(address, editor.Result);
        }

        private void FindLights(bool fromTray)
        {
            FindLightsWindow found = new FindLightsWindow(session.ExistingAddresses(), single: false);
            if (!fromTray)
                Own(found);
            if (found.ShowDialog() == true)
                session.AddFound(found.Chosen);
        }

        private void ShowShortcuts()
        {
            session.SetCapturingShortcut(true);
            try
            {
                ShortcutWindow dialog = new ShortcutWindow(session.AssignShortcut, session.ShortcutDisplay);
                Own(dialog);
                dialog.ShowDialog();
            }
            finally
            {
                session.SetCapturingShortcut(false);
            }
        }

        private void ShowSettings(string? page)
        {
            (AppUserSettings settings, IReadOnlyList<string> groups, IReadOnlyList<string> observed, RelayCredentials credentials) = session.OptionsDraft();
            SettingsWindow dialog = new SettingsWindow(settings, groups, observed, credentials);
            Own(dialog);
            if (page != null)
                dialog.Select(page);
            if (dialog.ShowDialog() == true)
                session.ApplyOptions(settings, dialog.Credentials);
        }

        private void ShowMain()
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
        }

        private void RequestExit()
        {
            if (allowExit)
                return;

            allowExit = true;
            tray.HideIcon();
            Close();
            Application.Current?.Shutdown();
        }

        private void Own(Window dialog)
        {
            if (!IsVisible)
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private void CaptureScreenshots()
        {
            string directory = session.ScreenshotDirectory ?? "";
            try
            {
                Show();
                UpdateLayout();
                WindowCapture.Save(this, System.IO.Path.Combine(directory, "camonkeylight-main-window.png"));
                (AppUserSettings settings, IReadOnlyList<string> groups, IReadOnlyList<string> observed, RelayCredentials credentials) = session.OptionsDraft();
                SettingsWindow settingsWindow = new SettingsWindow(settings, groups, observed, credentials)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                settingsWindow.Show();
                foreach ((string page, string file) in new[]
                {
                    ("quiet", "camonkeylight-options-quiet-hours.png"),
                    ("apps", "camonkeylight-options-camera-apps.png"),
                    ("groups", "camonkeylight-options-groups.png"),
                    ("notifications", "camonkeylight-options-notifications.png"),
                    ("relay", "camonkeylight-options-relay.png"),
                    ("about", "camonkeylight-about.png")
                })
                {
                    settingsWindow.Select(page);
                    settingsWindow.UpdateLayout();
                    WindowCapture.Save(settingsWindow, System.IO.Path.Combine(directory, file));
                }

                settingsWindow.Close();
                allowExit = true;
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "screenshot-error.txt"), ex.ToString());
                }
                catch (IOException)
                {
                }

                AppLog.Write("screenshot", "save", ex.GetType().Name);
                allowExit = true;
                Application.Current.Shutdown(1);
            }
        }
    }
}
