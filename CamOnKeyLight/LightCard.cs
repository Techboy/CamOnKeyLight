using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CamOnKeyLight
{
    public readonly record struct LightCardSnapshot(
        string Address,
        string Title,
        string Meta,
        string Status,
        string Kind,
        string ActionsName);

    public sealed class LightCard : INotifyPropertyChanged
    {
        private string title = "";
        private string meta = "";
        private string status = "";
        private string kind = "Pending";
        private string actionsName = "Actions";

        public LightCard(string address)
        {
            Address = address;
        }

        public string Address { get; }

        public string Title
        {
            get => title;
            private set => Set(ref title, value);
        }

        public string Meta
        {
            get => meta;
            private set => Set(ref meta, value);
        }

        public string Status
        {
            get => status;
            private set => Set(ref status, value);
        }

        public string Kind
        {
            get => kind;
            private set => Set(ref kind, value);
        }

        public string ActionsName
        {
            get => actionsName;
            private set => Set(ref actionsName, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Apply(LightCardSnapshot snapshot)
        {
            Title = snapshot.Title;
            Meta = snapshot.Meta;
            Status = snapshot.Status;
            Kind = snapshot.Kind;
            ActionsName = snapshot.ActionsName;
        }

        private void Set(ref string field, string value, [CallerMemberName] string? name = null)
        {
            if (field == value)
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
