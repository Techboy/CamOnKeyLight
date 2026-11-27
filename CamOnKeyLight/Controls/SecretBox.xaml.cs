using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace CamOnKeyLight
{
    public partial class SecretBox : UserControl
    {
        private bool showing;
        private string fieldName = "Secret";

        public SecretBox()
        {
            InitializeComponent();
        }

        public string FieldName
        {
            get => fieldName;
            set
            {
                fieldName = string.IsNullOrWhiteSpace(value) ? "Secret" : value;
                AutomationProperties.SetName(Hidden, fieldName);
                AutomationProperties.SetName(Clear, fieldName);
                AutomationProperties.SetName(Toggle, (showing ? "Hide " : "Show ") + fieldName);
            }
        }

        public string Secret
        {
            get => (showing ? Clear.Text : Hidden.Password).Trim();
            set
            {
                string text = value ?? "";
                Clear.Text = text;
                Hidden.Password = text;
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (showing)
                Hidden.Password = Clear.Text;
            else
                Clear.Text = Hidden.Password;

            showing = !showing;
            Clear.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
            Hidden.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
            Toggle.Content = showing ? "Hide" : "Show";
            AutomationProperties.SetName(Toggle, (showing ? "Hide " : "Show ") + fieldName);
            if (showing)
                Clear.Focus();
            else
                Hidden.Focus();
        }
    }
}
