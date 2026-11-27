using System.Diagnostics;
using System.Net.Http;
using System.Windows.Threading;
using System.Xml;
using CamOnKeyLight.Core;
using Microsoft.Win32;

namespace CamOnKeyLight
{
    internal sealed class AppSession : IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly string[] launchArgs;
        private readonly bool startInBackground;
        private readonly LightHttpClient lightHttp = new LightHttpClient();
        private readonly LightDispatch lightDispatch;
        private readonly LightModeController controller = new LightModeController(DateTime.UtcNow);
        private readonly LightStatusBoard board = new LightStatusBoard();
        private readonly PresenceTracker presence = new PresenceTracker();
        private readonly HashSet<string> seenCameraApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CancellationTokenSource> lightCalls = new Dictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        private readonly List<LightRecord> shown = new List<LightRecord>();
        private readonly HotkeyBook hotkeys = new HotkeyBook();
        private readonly DispatcherTimer cameraTimer;
        private readonly DispatcherTimer updateTimer;
        private Win32HotkeyRegistrar? registrar;
        private CameraActivityMonitor? cameraActivityMonitor = CameraActivityMonitor.TryCreate();
        private UpdateCoordinator? updateCoordinator;
        private decimal cameraOnDelay;
        private decimal cameraOffDelay;
        private bool ready;
        private bool applyingMode;
        private bool capturingShortcut;
        private bool hotkeysBound;
        private bool disposed;
        private int discoveryBusy;
        private EffectiveTheme effective = EffectiveTheme.Dark;

        public AppSession(Dispatcher dispatcher, string[]? args)
        {
            this.dispatcher = dispatcher;
            launchArgs = args ?? Array.Empty<string>();
            startInBackground = launchArgs.Contains("--background");
            ScreenshotDirectory = ReadScreenshotDirectory(launchArgs);
            lightDispatch = new LightDispatch(lightHttp);
            cameraTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            cameraTimer.Tick += (_, _) => CameraTick();
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            updateTimer.Tick += (_, _) => UpdateTick();
        }

        public event Action? UiChanged;
        public event Action? ExitRequested;
        public event Action<string, string, bool, int>? Balloon;

        public bool StartHidden { get; private set; }
        public string? ScreenshotDirectory { get; }
        public bool ScreenshotMode => ScreenshotDirectory != null;
        public string WindowTitle { get; private set; } = "Cam On Key Light";
        public string HeroHeadline { get; private set; } = "No lights added";
        public string HeroReason { get; private set; } = "";
        public string ModePill { get; private set; } = "Auto: lights follow the camera";
        public LightMode Mode => controller.Mode;
        public LightSummaryKind SummaryKind => board.Kind;
        public string ProblemDetail { get; private set; } = "";
        public bool HasProblem { get; private set; }
        public int LightCount => shown.Count;
        public IReadOnlyList<LightCardSnapshot> Lights { get; private set; } = Array.Empty<LightCardSnapshot>();
        public decimal OnDelaySeconds => DelayUnits.MillisecondsToSeconds(cameraOnDelay);
        public decimal OffDelaySeconds => DelayUnits.MillisecondsToSeconds(cameraOffDelay);
        public bool StartWithWindows { get; private set; }
        public ThemePreference Theme { get; private set; } = ThemePreference.Dark;
        public EffectiveTheme Effective => effective;
        public bool AutomaticUpdates { get; private set; } = true;
        public string UpdateStatus { get; private set; } = "";
        public bool UpdateProblem { get; private set; }
        public bool CheckEnabled { get; private set; } = true;
        public bool InstallEnabled { get; private set; }
        public bool RetryEnabled => shown.Count > 0;
        public string ShortcutButtonText { get; private set; } = "Shortcuts";
        public string QuietMenu { get; private set; } = "Quiet hours off";
        public string TrayTip { get; private set; } = "Cam On Key Light";
        public bool CameraOn { get; private set; }
        public string AutoShortcut { get; private set; } = "";
        public string OnShortcut { get; private set; } = "";
        public string OffShortcut { get; private set; } = "";

        private AppUserSettings userSettings = AppUserSettings.CreateDefault();
        private RelayCredentials relayCredentials = new RelayCredentials();

        public static Version CurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        }

        public void Start(IntPtr hwnd)
        {
            registrar = new Win32HotkeyRegistrar(() => hwnd);
            userSettings = UserSettingsFile.Load();
            relayCredentials = LoadRelayCredentials();
            foreach (string name in userSettings.KnownCameraApps)
                seenCameraApps.Add(name);
            Reload(importElgato: true);
            cameraOnDelay = DelayUnits.ClampMilliseconds(userSettings.CameraOnDelayMs, DelayUnits.MaxOnMilliseconds);
            cameraOffDelay = DelayUnits.ClampMilliseconds(userSettings.CameraOffDelayMs, DelayUnits.MaxOffMilliseconds);
            Theme = ThemePreferenceParser.Parse(userSettings.Theme);
            AutomaticUpdates = userSettings.AutomaticUpdates;
            StartWithWindows = StartupRegistry.RepairCurrentProcess();
            if (InstallHandoff.TryParseRetirement(launchArgs, out RetirementRequest retirement))
                LegacyLaunch.CompleteRetirement(retirement);

            updateCoordinator = new UpdateCoordinator(CurrentVersion(), AppContext.BaseDirectory, () => CameraOn, ShowUpdate);
            updateCoordinator.InstallLaunched += () =>
            {
                Ui(() => ExitRequested?.Invoke());
            };

            ready = true;
            ApplyTheme(Theme);
            LightMode persisted = LightModePreference.Parse(userSettings.LightMode);
            applyingMode = true;
            LightCommand? startup = controller.ApplyStartupMode(persisted, IsWebcamOn(), DateTime.UtcNow);
            applyingMode = false;
            if (startup != null)
                Dispatch(startup.Value, automatic: false);
            AnnounceInstalledUpdate();
            CameraOn = IsWebcamOn();
            if (ScreenshotMode)
                SeedScreenshotLights();
            else
                RefreshAll(userInitiated: true);
            ShowPendingUpdateError();
            CleanupStaging();
            if (!ScreenshotMode && AutomaticUpdates)
                updateTimer.Start();
            SystemEvents.UserPreferenceChanged += SystemThemeChanged;
            BindHotkeys();
            if (!ScreenshotMode)
                cameraTimer.Start();
            StartHidden = !ScreenshotMode && (StartWithWindows || startInBackground);
            Publish();
        }

        public void SetMode(LightMode mode)
        {
            if (applyingMode)
                return;

            applyingMode = true;
            try
            {
                LightCommand? command = controller.SetMode(mode, IsWebcamOn(), DateTime.UtcNow);
                if (ready)
                {
                    userSettings.LightMode = mode.ToString();
                    SaveUserSettings();
                }

                if (command != null)
                    Dispatch(command.Value, automatic: mode == LightMode.Auto);
                Publish();
            }
            finally
            {
                applyingMode = false;
            }
        }

        public void SetOnDelaySeconds(decimal seconds)
        {
            if (!ready)
                return;

            decimal ms = DelayUnits.ClampMilliseconds(DelayUnits.SecondsToMilliseconds(seconds), DelayUnits.MaxOnMilliseconds);
            if (ms == cameraOnDelay)
                return;

            cameraOnDelay = ms;
            userSettings.CameraOnDelayMs = ms;
            SaveUserSettings();
        }

        public void SetOffDelaySeconds(decimal seconds)
        {
            if (!ready)
                return;

            decimal ms = DelayUnits.ClampMilliseconds(DelayUnits.SecondsToMilliseconds(seconds), DelayUnits.MaxOffMilliseconds);
            if (ms == cameraOffDelay)
                return;

            cameraOffDelay = ms;
            userSettings.CameraOffDelayMs = ms;
            SaveUserSettings();
        }

        public void SetStartWithWindows(bool enabled)
        {
            if (!ready)
                return;

            StartWithWindows = StartupRegistry.ApplyForCurrentProcess(enabled);
            Publish();
        }

        public void SetTheme(ThemePreference preference)
        {
            if (!ready)
                return;

            Theme = preference;
            userSettings.Theme = preference.ToString();
            SaveUserSettings();
            ApplyTheme(preference);
            Publish();
        }

        public void SetAutomaticUpdates(bool enabled)
        {
            if (!ready)
                return;

            AutomaticUpdates = enabled;
            userSettings.AutomaticUpdates = enabled;
            SaveUserSettings();
            if (enabled)
            {
                updateTimer.Interval = TimeSpan.FromHours(6);
                updateTimer.Start();
                updateCoordinator?.CheckNow();
            }
            else
            {
                updateTimer.Stop();
                ShowUpdate(new UpdateUi(UpdateMessages.AutomaticOff + " Version " + ReleaseVersion.Format(CurrentVersion()) + ".", true, InstallEnabled, false));
            }
        }

        public void CheckForUpdates()
        {
            updateCoordinator?.CheckNow();
        }

        public void InstallUpdate()
        {
            updateCoordinator?.RequestInstall();
        }

        public void RetryLights()
        {
            board.SyncAddresses(shown.Select(light => light.Address));
            RefreshAll(userInitiated: true);
        }

        public void AddLight(LightRecord light)
        {
            userSettings.Lights.RemoveAll(existing => string.Equals(existing.Address, light.Address, StringComparison.OrdinalIgnoreCase));
            userSettings.Lights.Add(light);
            SaveUserSettings();
            Reload(importElgato: true);
            RefreshAll(true);
        }

        public void ReplaceLight(string previousAddress, LightRecord edited)
        {
            if (!string.Equals(previousAddress, edited.Address, StringComparison.OrdinalIgnoreCase))
                userSettings.Lights.RemoveAll(existing => string.Equals(existing.Address, previousAddress, StringComparison.OrdinalIgnoreCase));

            userSettings.Lights.RemoveAll(existing => string.Equals(existing.Address, edited.Address, StringComparison.OrdinalIgnoreCase));
            userSettings.Lights.Add(edited);
            SaveUserSettings();
            Reload(importElgato: true);
        }

        public void RemoveLight(string address)
        {
            userSettings.Lights.RemoveAll(existing => string.Equals(existing.Address, address, StringComparison.OrdinalIgnoreCase));
            SaveUserSettings();
            Reload(importElgato: false);
        }

        public void AddFound(IEnumerable<DiscoveredLight> lights)
        {
            bool changed = false;
            foreach (DiscoveredLight light in lights)
            {
                bool exists = userSettings.Lights.Any(saved => LightEndpoint.Host(saved.Address).Equals(LightEndpoint.Host(light.Address), StringComparison.OrdinalIgnoreCase));
                if (exists)
                    continue;

                string product = string.IsNullOrWhiteSpace(light.ProductName) ? light.Name : light.ProductName;
                userSettings.Lights.Add(new LightRecord(light.Address, light.Name, "", product, light.Serial));
                changed = true;
            }

            if (!changed)
                return;

            SaveUserSettings();
            Reload(importElgato: false);
            RefreshAll(true);
        }

        public LightRecord LightOrNew(string address)
        {
            return Record(address) ?? new LightRecord(address, Description(address));
        }

        public IReadOnlyList<string> ExistingAddresses()
        {
            return userSettings.Lights.Select(light => light.Address).ToList();
        }

        public (AppUserSettings Settings, IReadOnlyList<string> Groups, IReadOnlyList<string> Observed, RelayCredentials Credentials) OptionsDraft()
        {
            List<string> observed = seenCameraApps.Concat(WebcamConsent.Read(inUseOnly: false)).ToList();
            return (userSettings.Clone(), LightGrouping.Names(userSettings.Lights), observed, relayCredentials.Copy());
        }

        public void ApplyOptions(AppUserSettings edited, RelayCredentials credentials)
        {
            userSettings = edited;
            relayCredentials = credentials.Copy();
            seenCameraApps.Clear();
            foreach (string name in userSettings.KnownCameraApps)
                seenCameraApps.Add(name);
            SaveUserSettings();
            SaveRelayCredentials();
            Publish();
        }

        public IReadOnlyList<TrayGroup> Groups()
        {
            List<TrayGroup> menus = new List<TrayGroup>();
            IReadOnlyList<string> names = LightGrouping.Names(userSettings.Lights);
            bool ungrouped = userSettings.Lights.Any(light => string.IsNullOrWhiteSpace(light.Group));
            foreach (string name in names)
                menus.Add(GroupMenu(name, name));
            if (names.Count > 0 && ungrouped)
                menus.Add(GroupMenu("", LightGrouping.UngroupedLabel));
            return menus;
        }

        public void SendGroup(IReadOnlyList<string> addresses, string body)
        {
            SendBody(addresses, body);
        }

        public HotkeyAssignment AssignShortcut(LightMode mode, HotkeyChord? chord)
        {
            HotkeyAssignment result = hotkeys.Assign(mode, chord, registrar!);
            string text = result.ActiveChord == null ? "" : HotkeyGrammar.Format(result.ActiveChord);
            switch (mode)
            {
                case LightMode.On:
                    userSettings.HotkeyOn = text;
                    break;
                case LightMode.Off:
                    userSettings.HotkeyOff = text;
                    break;
                default:
                    userSettings.HotkeyAuto = text;
                    break;
            }

            SaveUserSettings();
            RefreshShortcutLabels();
            if (!result.Applied && result.Error != null)
                ShowUpdate(new UpdateUi(result.Error, CheckEnabled, InstallEnabled, true));
            else
                Publish();
            return result;
        }

        public string ShortcutDisplay(LightMode mode)
        {
            return hotkeys.Display(mode);
        }

        public void SetCapturingShortcut(bool capturing)
        {
            capturingShortcut = capturing;
        }

        public bool HandleHotkey(int id)
        {
            if (capturingShortcut || !HotkeyIds.TryMode(id, out LightMode mode))
                return false;

            SetMode(mode);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            cameraTimer.Stop();
            updateTimer.Stop();
            SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
            cameraActivityMonitor?.Dispose();
            cameraActivityMonitor = null;
            if (registrar != null)
                hotkeys.ReleaseAll(registrar);
            foreach (CancellationTokenSource call in lightCalls.Values)
                call.Cancel();
            updateCoordinator?.Dispose();
        }

        private void CameraTick()
        {
            bool cameraOn = IsWebcamOn();
            CameraOn = cameraOn;
            LightCommand? command = controller.OnCameraSample(
                cameraOn,
                DateTime.UtcNow,
                TimeSpan.FromMilliseconds((double)cameraOnDelay),
                TimeSpan.FromMilliseconds((double)cameraOffDelay),
                blockAutomaticOn: QuietHours.IsQuiet(userSettings, DateTime.Now));
            if (command != null)
                Dispatch(command.Value, automatic: true);
            RefreshAll(userInitiated: false);
            updateCoordinator?.NotifyCamera(cameraOn, AutomaticUpdates);
            Publish();
        }

        private void UpdateTick()
        {
            updateTimer.Interval = TimeSpan.FromHours(6);
            if (AutomaticUpdates)
                updateCoordinator?.CheckNow();
        }

        private bool IsWebcamOn()
        {
            IReadOnlyList<string> active = ActiveCameraApps();
            RememberApps(active);
            return CameraAppPolicy.CountsAsOn(
                CameraAppFilterPreference.Parse(userSettings.CameraAppMode),
                userSettings.SelectedCameraApps,
                active);
        }

        private IReadOnlyList<string> ActiveCameraApps()
        {
            if (cameraActivityMonitor != null)
            {
                if (!cameraActivityMonitor.IsCameraStreaming)
                    return Array.Empty<string>();

                List<string> names = new List<string>();
                foreach (int processId in cameraActivityMonitor.CopyStreamingProcessIds())
                {
                    string? file = ProcessFileName(processId);
                    if (file != null)
                        names.Add(file);
                }

                List<string> cleaned = CameraAppNames.Clean(names);
                return cleaned.Count == 0 ? new[] { CameraAppNames.Unknown } : cleaned;
            }

            return WebcamConsent.Read(inUseOnly: true);
        }

        private static string? ProcessFileName(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (string.IsNullOrWhiteSpace(process.ProcessName))
                    return null;

                string name = process.ProcessName;
                return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        private void RememberApps(IEnumerable<string> active)
        {
            bool added = false;
            foreach (string name in CameraAppNames.Clean(active))
            {
                if (name.Equals(CameraAppNames.Unknown, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!seenCameraApps.Add(name))
                    continue;
                if (userSettings.KnownCameraApps.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                userSettings.KnownCameraApps.Add(name);
                added = true;
            }

            if (added && ready)
                SaveUserSettings();
        }

        private void Dispatch(LightCommand command, bool automatic)
        {
            if (!controller.IsCurrent(command.Sequence))
                return;

            if (automatic && command.TurnOn && QuietHours.IsQuiet(userSettings, DateTime.Now))
                return;

            SendPower(command.TurnOn, automatic);
        }

        private void SendPower(bool turnOn, bool automatic)
        {
            IEnumerable<string> addresses = automatic
                ? LightGrouping.AutomaticAddresses(userSettings.Lights, userSettings.AllGroupsFollowCamera, userSettings.CameraGroups)
                : userSettings.Lights.Select(light => light.Address);
            SendBody(addresses, LightEndpoint.PutBody(turnOn));
        }

        private void SendBody(IEnumerable<string> addresses, string body)
        {
            foreach (LightTarget target in LightEndpoint.ForEachLight(addresses, LightPower.On))
                _ = PutBodyAsync(target.Address, body);
        }

        private async Task PutBodyAsync(string address, string body)
        {
            if (!LightAddressRules.TryCreateUrl(address, out _, out string? problem))
            {
                ReportLightFailure(address, LightLinkState.InvalidAddress, problem ?? LightDiagnostics.Invalid(address), "put");
                return;
            }

            try
            {
                LightExchange exchange = await lightDispatch.SendAsync(new LightExchangeRequest(
                    address, false, body, userSettings.RelayUrl, relayCredentials, DateTime.UtcNow), CancellationToken.None);
                if (!exchange.ConnectionFailed && exchange.StatusCode is >= 200 and < 300)
                    return;

                LightExchangeMeaning.Describe(exchange, address, out LightLinkState state, out string detail);
                ReportLightFailure(address, state, detail, "put", route: exchange.Route);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                ReportLightFailure(address, LightLinkState.Unreachable, LightDiagnostics.Unreachable(address), "put", ex);
            }
        }

        private void ReportLightFailure(string address, LightLinkState state, string detail, string operation, Exception? error = null, LightRoute route = LightRoute.Direct)
        {
            AppLog.Write(address, operation, error == null ? detail : error.GetType().Name + " " + detail);
            Ui(() =>
            {
                if (disposed)
                    return;

                board.Report(address, state, detail, route);
                Publish();
            });
        }

        private void RefreshAll(bool userInitiated)
        {
            board.SyncAddresses(shown.Select(light => light.Address));
            foreach (LightRecord light in shown.ToList())
                RefreshOne(light.Address, userInitiated);
        }

        private void RefreshOne(string address, bool userInitiated)
        {
            if (!userInitiated && lightCalls.ContainsKey(address))
                return;

            if (userInitiated && lightCalls.TryGetValue(address, out CancellationTokenSource? previous))
            {
                lightCalls.Remove(address);
                previous.Cancel();
                previous.Dispose();
            }

            int attempt = userInitiated ? board.BeginUserRetry(address) : board.BeginBackgroundRefresh(address);
            Publish();
            TimeSpan budget = RelayUrls.IsConfigured(userSettings.RelayUrl)
                ? LightTiming.DirectAttemptWhenRelayConfigured + LightTiming.RelayRequestTimeout + TimeSpan.FromSeconds(1)
                : LightTiming.LightRequestTimeout;
            CancellationTokenSource cts = new CancellationTokenSource(budget);
            lightCalls[address] = cts;
            _ = RefreshOneAsync(address, attempt, cts);
        }

        private async Task RefreshOneAsync(string address, int attempt, CancellationTokenSource cts)
        {
            LightLinkState state = LightLinkState.Unreachable;
            string detail = LightDiagnostics.Unreachable(address);
            LightRoute route = LightRoute.Direct;
            if (!LightAddressRules.TryCreateUrl(address, out _, out string? problem))
            {
                state = LightLinkState.InvalidAddress;
                detail = problem ?? LightDiagnostics.Invalid(address);
                AppLog.Write(address, "check", detail);
            }
            else
            {
                try
                {
                    LightExchange exchange = await lightDispatch.SendAsync(new LightExchangeRequest(
                        address, true, null, userSettings.RelayUrl, relayCredentials, DateTime.UtcNow), cts.Token);
                    LightExchangeMeaning.Describe(exchange, address, out state, out detail);
                    route = exchange.Route;
                }
                catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
                {
                    AppLog.Write(address, "check", ex.GetType().Name + " " + detail);
                }
                catch (Exception ex)
                {
                    AppLog.Write(address, "check", ex.GetType().Name + " " + detail);
                }
            }

            Ui(() =>
            {
                if (disposed)
                    return;

                board.TryComplete(address, attempt, state, detail, route);
                NotePresence(address, state);
                if (lightCalls.TryGetValue(address, out CancellationTokenSource? current) && current == cts)
                {
                    lightCalls.Remove(address);
                    cts.Dispose();
                }

                Publish();
            });
        }

        private void Reload(bool importElgato)
        {
            shown.Clear();
            foreach (LightRecord light in userSettings.Lights)
            {
                if (string.IsNullOrWhiteSpace(light.Address))
                    continue;
                if (shown.Any(existing => existing.Address.Equals(light.Address, StringComparison.OrdinalIgnoreCase)))
                    continue;

                shown.Add(light);
            }

            if (importElgato)
                ImportElgato();
            board.SyncAddresses(shown.Select(light => light.Address));
            Publish();
        }

        private void ImportElgato()
        {
            string elgatoSettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elgato", "ControlCenter", "Settings.xml");
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(elgatoSettingsFile);
                foreach (XmlNode outer in doc.ChildNodes)
                {
                    if (outer.Name != "AppSettings")
                        continue;

                    foreach (XmlNode node in outer.ChildNodes)
                    {
                        if (node.Name != "Application")
                            continue;

                        foreach (XmlNode inner in node.ChildNodes)
                        {
                            if (inner.Name != "Accessories")
                                continue;

                            foreach (XmlNode accessory in inner.ChildNodes)
                            {
                                foreach (XmlNode attribute in accessory.ChildNodes)
                                {
                                    if (attribute.Name == "IpAddress")
                                        AddShown(attribute.InnerText.Trim());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
            }
        }

        private void AddShown(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return;
            if (shown.Any(existing => existing.Address.Equals(address, StringComparison.OrdinalIgnoreCase)))
                return;

            shown.Add(new LightRecord(address, ""));
        }

        private void SeedScreenshotLights()
        {
            if (shown.Count > 0)
                return;

            shown.Add(new LightRecord("192.168.1.5", "Left light"));
            shown.Add(new LightRecord("192.168.1.6", "Right light"));
            board.SyncAddresses(shown.Select(light => light.Address));
            foreach (LightRecord light in shown)
                board.Report(light.Address, LightLinkState.Off, LightDiagnostics.OffDetail(light.Address));
        }

        private string Description(string address)
        {
            return Record(address)?.Description ?? "";
        }

        private LightRecord? Record(string address)
        {
            foreach (LightRecord light in userSettings.Lights)
            {
                if (string.Equals(light.Address, address, StringComparison.OrdinalIgnoreCase))
                    return light;
            }

            foreach (LightRecord light in shown)
            {
                if (string.Equals(light.Address, address, StringComparison.OrdinalIgnoreCase))
                    return light;
            }

            return null;
        }

        private void Publish()
        {
            if (disposed)
                return;

            LightControlSnapshot snapshot = LightModeController.Describe(controller.Mode);
            WindowTitle = snapshot.Title;
            bool quiet = QuietHours.IsQuiet(userSettings, DateTime.Now);
            HeroHeadline = DashboardCopy.Headline(board.Kind, board.Summary);
            HeroReason = DashboardCopy.Reason(controller.Mode, CameraOn, quiet, shown.Count > 0);
            ModePill = DashboardCopy.ModePill(controller.Mode);
            ProblemDetail = board.FirstProblem ?? "";
            HasProblem = board.FirstProblem != null;
            Lights = board.Rows.Select(CardFor).ToList();
            QuietMenu = QuietHours.MenuLabel(userSettings, DateTime.Now);
            string tip = QuietHours.TrayTip(snapshot.Title, quiet);
            if (board.UsesRelay)
            {
                string withRelay = tip + " · relay";
                if (withRelay.Length <= 63)
                    tip = withRelay;
            }

            TrayTip = tip;
            RefreshShortcutLabels();
            UiChanged?.Invoke();
        }

        private LightCardSnapshot CardFor(LightRow row)
        {
            LightRecord? record = Record(row.Address);
            string description = record?.Description ?? "";
            string group = record?.Group ?? "";
            string title = string.IsNullOrWhiteSpace(description) ? row.Address : description;
            string meta;
            if (string.IsNullOrWhiteSpace(description))
                meta = group;
            else if (string.IsNullOrWhiteSpace(group))
                meta = row.Address;
            else
                meta = row.Address + " · " + group;

            string kind = row.State switch
            {
                LightLinkState.On => "On",
                LightLinkState.Off => "Off",
                LightLinkState.Unreachable or LightLinkState.UnexpectedReply or LightLinkState.InvalidAddress => "Problem",
                _ => "Pending"
            };
            return new LightCardSnapshot(row.Address, title, meta, row.StatusText, kind, "Actions for " + title);
        }

        private TrayGroup GroupMenu(string group, string label)
        {
            TrayGroup menu = new TrayGroup { Label = label };
            List<string> addresses = LightGrouping.Members(userSettings.Lights, group).Select(light => light.Address).ToList();
            menu.Commands.Add(Command("On", addresses, LightEndpoint.PutBody(true)));
            menu.Commands.Add(Command("Off", addresses, LightEndpoint.PutBody(false)));
            foreach (int level in new[] { 25, 50, 75, 100 })
                menu.Commands.Add(Command("Brightness " + level + "%", addresses, LightEndpoint.BrightnessBody(level)));
            return menu;
        }

        private static TrayGroupCommand Command(string text, List<string> addresses, string body)
        {
            return new TrayGroupCommand { Text = text, Addresses = addresses, Body = body };
        }

        private void NotePresence(string address, LightLinkState state)
        {
            if (state is not (LightLinkState.On or LightLinkState.Off or LightLinkState.Unreachable or LightLinkState.UnexpectedReply))
                return;

            bool reachable = state is LightLinkState.On or LightLinkState.Off;
            PresenceNotice? notice = presence.Observe(address, reachable, DateTime.UtcNow);
            if (notice == null)
                return;

            if (userSettings.NotifyLightPresence)
            {
                string label = Description(notice.Value.Address);
                Balloon?.Invoke(
                    PresenceText.Title(notice.Value.Online),
                    PresenceText.Body(string.IsNullOrWhiteSpace(label) ? notice.Value.Address : label, notice.Value.Online),
                    notice.Value.Online,
                    4000);
            }

            if (!notice.Value.Online)
                _ = RediscoverOfflineAsync();
        }

        private async Task RediscoverOfflineAsync()
        {
            if (Interlocked.CompareExchange(ref discoveryBusy, 1, 0) != 0)
                return;

            try
            {
                DiscoveryResult result = await ElgatoBrowser.SearchAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
                if (disposed || result.Lights.Count == 0)
                    return;

                Ui(() =>
                {
                    if (disposed)
                        return;

                    bool changed = false;
                    foreach (LightRecord light in userSettings.Lights.ToList())
                    {
                        string? replacement = ElgatoDiscovery.ReplacementAddress(light, userSettings.Lights, result.Lights);
                        if (replacement == null)
                            continue;

                        light.Address = replacement;
                        changed = true;
                    }

                    if (!changed)
                        return;

                    SaveUserSettings();
                    Reload(importElgato: false);
                    RefreshAll(true);
                });
            }
            catch (Exception ex) when (ex is OperationCanceledException or System.Net.Sockets.SocketException)
            {
            }
            finally
            {
                Interlocked.Exchange(ref discoveryBusy, 0);
            }
        }

        private void AnnounceInstalledUpdate()
        {
            try
            {
                string path = Path.Combine(UpdateInstaller.UpdatesRoot(), "just-installed.txt");
                if (!File.Exists(path))
                    return;

                string version = File.ReadAllText(path).Trim();
                File.Delete(path);
                if (userSettings.NotifyOnUpdate && version.Length > 0)
                    Balloon?.Invoke("Update installed", "Cam On Key Light " + version + " is now running.", true, 5000);
            }
            catch (IOException)
            {
            }
        }

        private void BindHotkeys()
        {
            if (hotkeysBound || registrar == null)
                return;

            hotkeysBound = true;
            IReadOnlyList<string> errors = hotkeys.BindSaved(userSettings.HotkeyAuto, userSettings.HotkeyOn, userSettings.HotkeyOff, registrar);
            RefreshShortcutLabels();
            if (errors.Count > 0)
                ShowUpdate(new UpdateUi(string.Join(" ", errors), true, InstallEnabled, true));
        }

        private void RefreshShortcutLabels()
        {
            AutoShortcut = hotkeys.Current(LightMode.Auto) == null ? "" : hotkeys.Display(LightMode.Auto);
            OnShortcut = hotkeys.Current(LightMode.On) == null ? "" : hotkeys.Display(LightMode.On);
            OffShortcut = hotkeys.Current(LightMode.Off) == null ? "" : hotkeys.Display(LightMode.Off);
            bool problem = UpdateStatus.Contains("another program", StringComparison.OrdinalIgnoreCase)
                || UpdateStatus.Contains("was not registered", StringComparison.OrdinalIgnoreCase);
            ShortcutButtonText = problem ? "Shortcuts…" : "Shortcuts";
        }

        private void ApplyTheme(ThemePreference preference)
        {
            effective = ThemeResolver.Resolve(preference, WindowsTheme.AppsUseLightTheme(), SystemParametersHighContrast());
        }

        private static bool SystemParametersHighContrast()
        {
            try
            {
                return System.Windows.SystemParameters.HighContrast;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility))
                return;
            if (disposed || Theme != ThemePreference.FollowWindows)
                return;

            Ui(() =>
            {
                ApplyTheme(ThemePreference.FollowWindows);
                Publish();
            });
        }

        private void ShowUpdate(UpdateUi state)
        {
            Ui(() =>
            {
                UpdateProblem = state.Problem;
                UpdateStatus = state.Status;
                CheckEnabled = state.CheckEnabled;
                InstallEnabled = state.InstallEnabled;
                RefreshShortcutLabels();
                if (ready)
                    Publish();
            });
        }

        private void ShowPendingUpdateError()
        {
            try
            {
                string path = UpdateInstaller.LastErrorPath();
                if (!File.Exists(path))
                    return;

                string message = File.ReadAllText(path).Trim();
                File.Delete(path);
                if (message.Length > 0)
                    ShowUpdate(new UpdateUi(message, true, false, true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static void CleanupStaging()
        {
            try
            {
                string staging = Path.Combine(UpdateInstaller.UpdatesRoot(), "staging");
                if (Directory.Exists(staging) && !PackageRules.IsInside(AppContext.BaseDirectory, staging))
                    Directory.Delete(staging, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static RelayCredentials LoadRelayCredentials()
        {
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return RelaySecretFile.Load(SettingsLocations.RelaySecretFile(local), new DpapiSecretProtector());
            }
            catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException or PlatformNotSupportedException)
            {
                AppLog.Write("relay", "load", ex.GetType().Name);
                return new RelayCredentials();
            }
        }

        private void SaveRelayCredentials()
        {
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                RelaySecretFile.Save(SettingsLocations.RelaySecretFile(local), relayCredentials, new DpapiSecretProtector());
            }
            catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException or PlatformNotSupportedException)
            {
                AppLog.Write("relay", "save", ex.GetType().Name);
            }
        }

        private void SaveUserSettings()
        {
            UserSettingsFile.Save(userSettings);
        }

        private void Ui(Action action)
        {
            if (disposed)
                return;

            if (dispatcher.CheckAccess())
                action();
            else
            {
                try
                {
                    dispatcher.BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private static string? ReadScreenshotDirectory(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--screenshot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];
            }

            return null;
        }
    }
}
