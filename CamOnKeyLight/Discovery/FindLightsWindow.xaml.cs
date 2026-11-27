using System.Windows;
using System.Windows.Controls;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    public partial class FindLightsWindow : Window
    {
        private readonly IReadOnlyList<string> existing;
        private readonly bool single;
        private CancellationTokenSource search = new CancellationTokenSource();
        private int searchGeneration;
        private bool closed;

        public FindLightsWindow(IReadOnlyList<string> existingAddresses, bool single)
        {
            existing = existingAddresses;
            this.single = single;
            InitializeComponent();
            Lights.SelectionMode = single ? SelectionMode.Single : SelectionMode.Extended;
            UseButton.Content = single ? "Use" : "Add";
            AutomationPropertiesName();
            Loaded += async (_, _) =>
            {
                try
                {
                    await SearchAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (!closed)
                        Status.Text = "Couldn't search the network. " + ex.Message;
                }
            };
        }

        public IReadOnlyList<DiscoveredLight> Chosen { get; private set; } = Array.Empty<DiscoveredLight>();

        protected override void OnClosed(EventArgs e)
        {
            closed = true;
            try
            {
                search.Cancel();
                search.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            base.OnClosed(e);
        }

        private void AutomationPropertiesName()
        {
            System.Windows.Automation.AutomationProperties.SetName(UseButton, single ? "Use the selected light" : "Add the selected lights");
        }

        private async void Again_Click(object sender, RoutedEventArgs e)
        {
            await SearchAsync();
        }

        private async Task SearchAsync()
        {
            int generation = ++searchGeneration;
            CancellationTokenSource previous = search;
            search = new CancellationTokenSource();
            CancellationToken token = search.Token;
            try
            {
                previous.Cancel();
                previous.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            Lights.Items.Clear();
            Status.Text = "Searching the network…";
            UseButton.IsEnabled = false;
            try
            {
                DiscoveryResult result = await ElgatoBrowser.SearchAsync(TimeSpan.FromSeconds(3), token);
                if (closed || generation != searchGeneration)
                    return;

                IReadOnlyList<DiscoveredLight> fresh = ElgatoDiscovery.ExceptAdded(result.Lights, existing);
                foreach (DiscoveredLight light in fresh)
                    Lights.Items.Add(light);

                int skipped = result.Lights.Count - fresh.Count;
                if (result.Error != null && fresh.Count == 0)
                    Status.Text = result.Error;
                else if (fresh.Count == 0 && skipped == 0)
                    Status.Text = "No Elgato lights answered. They need to be on this network.";
                else if (fresh.Count == 0)
                    Status.Text = "No new lights. " + skipped + " already added.";
                else
                    Status.Text = fresh.Count + (fresh.Count == 1 ? " light." : " lights.") + (skipped == 0 ? "" : " " + skipped + " already added.");

                UseButton.IsEnabled = fresh.Count > 0;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Use_Click(object sender, RoutedEventArgs e)
        {
            List<DiscoveredLight> chosen = new List<DiscoveredLight>();
            foreach (object item in Lights.SelectedItems)
            {
                if (item is DiscoveredLight light)
                    chosen.Add(light);
            }

            if (chosen.Count == 0)
            {
                Status.Text = "Select a light first.";
                return;
            }

            Chosen = chosen;
            DialogResult = true;
        }
    }
}
