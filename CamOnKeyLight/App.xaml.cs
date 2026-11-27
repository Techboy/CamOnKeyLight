using System.Windows;

namespace CamOnKeyLight
{
    public partial class App : Application
    {
        public static bool IsSessionEnding { get; private set; }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            IsSessionEnding = true;
            base.OnSessionEnding(e);
        }
    }
}
