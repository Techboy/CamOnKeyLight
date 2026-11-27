using System.Windows;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    public partial class LightEditorWindow : Window
    {
        private readonly IReadOnlyList<string> existing;
        private string serial = "";
        private string product = "";

        public LightEditorWindow(LightRecord? existingLight, IReadOnlyList<string> existingAddresses)
        {
            existing = existingAddresses;
            InitializeComponent();
            Title = existingLight == null ? "Add light" : "Edit light";
            if (existingLight != null)
            {
                AddressBox.Text = existingLight.Address;
                DescriptionBox.Text = existingLight.Description;
                GroupBox.Text = existingLight.Group;
                serial = existingLight.Serial;
                product = existingLight.ProductName;
            }
        }

        public LightRecord? Result { get; private set; }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            FindLightsWindow found = new FindLightsWindow(existing, single: true);
            if (IsVisible)
                found.Owner = this;
            if (found.ShowDialog() != true || found.Chosen.Count == 0)
                return;

            DiscoveredLight pick = found.Chosen[0];
            AddressBox.Text = pick.Address;
            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
                DescriptionBox.Text = pick.Name;
            serial = pick.Serial;
            product = string.IsNullOrWhiteSpace(pick.ProductName) ? pick.Name : pick.ProductName;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AddressBox.Text))
            {
                ErrorText.Text = "Enter an address.";
                AddressBox.Focus();
                return;
            }

            Result = new LightRecord(AddressBox.Text, DescriptionBox.Text, GroupBox.Text, product, serial);
            DialogResult = true;
        }
    }
}
