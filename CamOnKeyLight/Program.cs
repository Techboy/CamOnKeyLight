using System.Windows;
using CamOnKeyLight.Core;

namespace CamOnKeyLight
{
    internal static class Program
    {
        private static Mutex? instanceMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            if (UpdateInstaller.IsApplyRequest(args))
            {
                UpdateInstaller.Run(args);
                return;
            }

            if (LegacyLaunch.TryHandOff(args))
                return;

            bool createdNew = false;
            try
            {
                instanceMutex = new Mutex(true, SettingsLocations.SingleInstanceMutex, out createdNew);
            }
            catch (AbandonedMutexException)
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                instanceMutex?.Dispose();
                return;
            }

            try
            {
                SettingsMigrator.Migrate();
            }
            catch (System.Configuration.ConfigurationErrorsException)
            {
            }

            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            App app = new App();
            app.InitializeComponent();
            BrandTheme.Apply(EffectiveTheme.Dark);
            MainWindow window = new MainWindow(args);
            app.MainWindow = window;
            window.Start();
            if (!window.StartHidden)
                window.Show();
            app.Run();
            instanceMutex?.Dispose();
        }
    }
}
