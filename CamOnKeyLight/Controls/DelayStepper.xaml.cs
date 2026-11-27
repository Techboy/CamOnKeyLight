using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    public partial class DelayStepper : UserControl
    {
        private decimal current;
        private bool updating;

        public DelayStepper()
        {
            InitializeComponent();
            Loaded += (_, _) => CopyName();
            SetSeconds(0);
        }

        public decimal Maximum { get; set; } = DelayUnits.MaxOffSeconds;

        public decimal Seconds => current;

        public event EventHandler? SecondsChanged;

        public void SetSeconds(decimal seconds)
        {
            current = Clamp(seconds);
            updating = true;
            Box.Text = current.ToString("0.0", CultureInfo.CurrentCulture);
            updating = false;
        }

        private void CopyName()
        {
            string name = AutomationProperties.GetName(this);
            if (name.Length == 0)
                return;

            AutomationProperties.SetName(Box, name);
            AutomationProperties.SetName(Up, "Increase " + name);
            AutomationProperties.SetName(Down, "Decrease " + name);
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            Step(DelayUnits.StepSeconds);
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            Step(-DelayUnits.StepSeconds);
        }

        private void Step(decimal delta)
        {
            decimal next = Clamp(current + delta);
            if (next == current)
                return;

            SetSeconds(next);
            SecondsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Box_LostFocus(object sender, RoutedEventArgs e)
        {
            Commit();
        }

        private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            Commit();
            e.Handled = true;
        }

        private void Commit()
        {
            if (updating)
                return;

            if (!decimal.TryParse(Box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal seconds)
                && !decimal.TryParse(Box.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out seconds))
            {
                SetSeconds(current);
                return;
            }

            seconds = Math.Round(seconds, 1, MidpointRounding.AwayFromZero);
            decimal next = Clamp(seconds);
            bool changed = next != current;
            SetSeconds(next);
            if (changed)
                SecondsChanged?.Invoke(this, EventArgs.Empty);
        }

        private decimal Clamp(decimal seconds)
        {
            if (seconds < 0)
                return 0;
            if (seconds > Maximum)
                return Maximum;
            return seconds;
        }
    }
}
