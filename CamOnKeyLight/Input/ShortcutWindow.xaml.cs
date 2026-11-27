using System.Windows;
using System.Windows.Input;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    public partial class ShortcutWindow : Window
    {
        private readonly Func<LightMode, HotkeyChord?, HotkeyAssignment> assign;
        private readonly Func<LightMode, string> current;
        private LightMode? capturing;

        public ShortcutWindow(Func<LightMode, HotkeyChord?, HotkeyAssignment> assign, Func<LightMode, string> current)
        {
            this.assign = assign;
            this.current = current;
            InitializeComponent();
            RefreshDisplays();
        }

        private void SetAuto_Click(object sender, RoutedEventArgs e) => Begin(LightMode.Auto);
        private void SetOn_Click(object sender, RoutedEventArgs e) => Begin(LightMode.On);
        private void SetOff_Click(object sender, RoutedEventArgs e) => Begin(LightMode.Off);
        private void ClearAuto_Click(object sender, RoutedEventArgs e) => Clear(LightMode.Auto);
        private void ClearOn_Click(object sender, RoutedEventArgs e) => Clear(LightMode.On);
        private void ClearOff_Click(object sender, RoutedEventArgs e) => Clear(LightMode.Off);

        private void Begin(LightMode mode)
        {
            capturing = mode;
            Status.Text = "Press Ctrl, Alt, or Shift plus a key for " + mode + ". Esc cancels.";
        }

        private void Clear(LightMode mode)
        {
            capturing = null;
            HotkeyAssignment result = assign(mode, null);
            Status.Text = result.Applied ? mode + " shortcut cleared." : result.Error ?? "";
            RefreshDisplays();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (capturing == null)
                return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = true;
            if (key == Key.Escape)
            {
                capturing = null;
                Status.Text = "Cancelled.";
                return;
            }

            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            string? name = KeyName(key);
            if (name == null)
                return;

            ModifierKeys modifiers = Keyboard.Modifiers;
            if (!HotkeyGrammar.TryCreate(
                    modifiers.HasFlag(ModifierKeys.Control),
                    modifiers.HasFlag(ModifierKeys.Alt),
                    modifiers.HasFlag(ModifierKeys.Shift),
                    name,
                    out HotkeyChord? chord,
                    out string? error) || chord == null)
            {
                Status.Text = error ?? "That shortcut was not saved.";
                capturing = null;
                return;
            }

            HotkeyAssignment result = assign(capturing.Value, chord);
            Status.Text = result.Applied ? "Saved " + HotkeyGrammar.Format(chord) + "." : result.Error ?? "That shortcut was not saved.";
            capturing = null;
            RefreshDisplays();
        }

        private void RefreshDisplays()
        {
            AutoValue.Text = current(LightMode.Auto);
            OnValue.Text = current(LightMode.On);
            OffValue.Text = current(LightMode.Off);
        }

        private static string? KeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return ((char)('0' + (key - Key.NumPad0))).ToString();

            if (key is Key.Tab or Key.Enter or Key.Space or Key.None)
                return null;

            return key.ToString();
        }
    }
}
