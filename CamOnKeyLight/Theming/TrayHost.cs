using System.Runtime.InteropServices;
using CamOnKeyLight.Core;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace CamOnKeyLight
{
    internal sealed class TrayGroup
    {
        public string Label { get; init; } = "";
        public List<TrayGroupCommand> Commands { get; } = new List<TrayGroupCommand>();
    }

    internal sealed class TrayGroupCommand
    {
        public string Text { get; init; } = "";
        public List<string> Addresses { get; init; } = new List<string>();
        public string Body { get; init; } = "";
    }

    internal sealed class TrayHost : IDisposable
    {
        private readonly Forms.NotifyIcon icon;
        private readonly Forms.ContextMenuStrip menu;
        private readonly Forms.ToolStripMenuItem autoItem;
        private readonly Forms.ToolStripMenuItem onItem;
        private readonly Forms.ToolStripMenuItem offItem;
        private readonly Forms.ToolStripMenuItem quietItem;
        private readonly Forms.ToolStripMenuItem groupsItem;
        private readonly Drawing.Icon lit;
        private readonly Drawing.Icon unlit;
        private readonly Func<IReadOnlyList<TrayGroup>> groups;
        private bool disposed;

        public TrayHost(Func<IReadOnlyList<TrayGroup>> groups)
        {
            this.groups = groups;
            lit = CloneIcon(Properties.Resources.TrayIconLit);
            unlit = CloneIcon(Properties.Resources.TrayIconUnlit);
            menu = new Forms.ContextMenuStrip();
            autoItem = Item("Auto", "tsmiAuto");
            onItem = Item("On", "tsmiOn");
            offItem = Item("Off", "tsmiOff");
            quietItem = Item("Quiet hours off", "tsmiQuiet");
            quietItem.Enabled = false;
            groupsItem = Item("Groups", "tsmiGroups");
            groupsItem.Visible = false;
            Forms.ToolStripMenuItem find = Item("Find lights…", "tsmiFindLights");
            Forms.ToolStripMenuItem settings = Item("Settings…", "tsmiOptions");
            Forms.ToolStripMenuItem shortcuts = Item("Shortcuts…", "tsmiShortcuts");
            Forms.ToolStripMenuItem open = Item("Open", "tsmiOpen");
            Forms.ToolStripMenuItem exit = Item("Exit", "tsmiExit");
            menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                autoItem, onItem, offItem, quietItem, new Forms.ToolStripSeparator(),
                groupsItem, find, settings, shortcuts, open, exit
            });
            menu.Opening += (_, _) => RebuildGroups();
            menu.ItemClicked += MenuClicked;
            icon = new Forms.NotifyIcon
            {
                Icon = unlit,
                Text = "Cam On Key Light",
                Visible = true,
                ContextMenuStrip = menu
            };
            icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        }

        public event Action? OpenRequested;
        public event Action? ExitRequested;
        public event Action<LightMode>? ModeRequested;
        public event Action? SettingsRequested;
        public event Action? ShortcutsRequested;
        public event Action? FindRequested;
        public event Action<IReadOnlyList<string>, string>? GroupCommandRequested;

        public void Update(bool cameraOn, string tip, LightMode mode, string quiet, string autoShortcut, string onShortcut, string offShortcut)
        {
            icon.Icon = cameraOn ? lit : unlit;
            icon.Text = tip.Length <= 63 ? tip : tip[..63];
            autoItem.Checked = mode == LightMode.Auto;
            onItem.Checked = mode == LightMode.On;
            offItem.Checked = mode == LightMode.Off;
            quietItem.Text = quiet;
            autoItem.ShortcutKeyDisplayString = autoShortcut;
            onItem.ShortcutKeyDisplayString = onShortcut;
            offItem.ShortcutKeyDisplayString = offShortcut;
        }

        public void ApplyTheme(ThemePalette? palette)
        {
            menu.Renderer = palette == null
                ? new Forms.ToolStripProfessionalRenderer()
                : new PaletteMenuRenderer(palette.Value);
        }

        public void ShowBalloon(string title, string body, bool info, int milliseconds = 4000)
        {
            icon.ShowBalloonTip(milliseconds, title, body, info ? Forms.ToolTipIcon.Info : Forms.ToolTipIcon.Warning);
        }

        public void HideIcon()
        {
            icon.Visible = false;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
            lit.Dispose();
            unlit.Dispose();
        }

        private void RebuildGroups()
        {
            groupsItem.DropDownItems.Clear();
            IReadOnlyList<TrayGroup> rows = groups();
            groupsItem.Visible = rows.Count > 0;
            foreach (TrayGroup row in rows)
            {
                Forms.ToolStripMenuItem menuItem = new Forms.ToolStripMenuItem(row.Label);
                foreach (TrayGroupCommand command in row.Commands)
                {
                    TrayGroupCommand captured = command;
                    Forms.ToolStripMenuItem item = new Forms.ToolStripMenuItem(captured.Text);
                    item.Click += (_, _) => GroupCommandRequested?.Invoke(captured.Addresses, captured.Body);
                    menuItem.DropDownItems.Add(item);
                }

                groupsItem.DropDownItems.Add(menuItem);
            }
        }

        private void MenuClicked(object? sender, Forms.ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem?.Name)
            {
                case "tsmiExit":
                    ExitRequested?.Invoke();
                    break;
                case "tsmiOpen":
                    OpenRequested?.Invoke();
                    break;
                case "tsmiAuto":
                    ModeRequested?.Invoke(LightMode.Auto);
                    break;
                case "tsmiOn":
                    ModeRequested?.Invoke(LightMode.On);
                    break;
                case "tsmiOff":
                    ModeRequested?.Invoke(LightMode.Off);
                    break;
                case "tsmiShortcuts":
                    ShortcutsRequested?.Invoke();
                    break;
                case "tsmiFindLights":
                    FindRequested?.Invoke();
                    break;
                case "tsmiOptions":
                    SettingsRequested?.Invoke();
                    break;
            }
        }

        private static Forms.ToolStripMenuItem Item(string text, string name)
        {
            return new Forms.ToolStripMenuItem(text) { Name = name };
        }

        private static Drawing.Icon CloneIcon(Drawing.Bitmap bitmap)
        {
            IntPtr handle = bitmap.GetHicon();
            try
            {
                using Drawing.Icon icon = Drawing.Icon.FromHandle(handle);
                return (Drawing.Icon)icon.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        private sealed class PaletteColorTable : Forms.ProfessionalColorTable
        {
            private readonly ThemePalette palette;

            public PaletteColorTable(ThemePalette palette)
            {
                this.palette = palette;
            }

            public override Drawing.Color ToolStripDropDownBackground => Draw(palette.MenuBackground);
            public override Drawing.Color MenuBorder => Draw(palette.Border);
            public override Drawing.Color MenuItemBorder => Draw(palette.MenuBackground);
            public override Drawing.Color MenuItemSelected => Draw(palette.MenuHighlight);
            public override Drawing.Color MenuItemSelectedGradientBegin => Draw(palette.MenuHighlight);
            public override Drawing.Color MenuItemSelectedGradientEnd => Draw(palette.MenuHighlight);
            public override Drawing.Color ImageMarginGradientBegin => Draw(palette.MenuBackground);
            public override Drawing.Color ImageMarginGradientMiddle => Draw(palette.MenuBackground);
            public override Drawing.Color ImageMarginGradientEnd => Draw(palette.MenuBackground);
            public override Drawing.Color SeparatorDark => Draw(palette.Border);
            public override Drawing.Color SeparatorLight => Draw(palette.MenuBackground);
            public override Drawing.Color CheckBackground => Draw(palette.MenuHighlight);
            public override Drawing.Color CheckSelectedBackground => Draw(palette.MenuHighlight);
            public override Drawing.Color CheckPressedBackground => Draw(palette.MenuHighlight);

            private static Drawing.Color Draw(ThemeColor color)
            {
                return Drawing.Color.FromArgb(color.R, color.G, color.B);
            }
        }

        private sealed class PaletteMenuRenderer : Forms.ToolStripProfessionalRenderer
        {
            private readonly ThemePalette palette;

            public PaletteMenuRenderer(ThemePalette palette)
                : base(new PaletteColorTable(palette))
            {
                this.palette = palette;
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
            {
                using Drawing.SolidBrush brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(palette.MenuBackground.R, palette.MenuBackground.G, palette.MenuBackground.B));
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
            {
                bool highlighted = e.Item.Selected || e.Item.Pressed;
                ThemeColor color = highlighted ? palette.MenuHighlight : palette.MenuBackground;
                using Drawing.SolidBrush brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(color.R, color.G, color.B));
                e.Graphics.FillRectangle(brush, new Drawing.Rectangle(Drawing.Point.Empty, e.Item.Size));
            }

            protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
            {
                ThemeColor color = e.Item.Selected || e.Item.Pressed ? palette.MenuHighlightText : palette.MenuForeground;
                Forms.TextRenderer.DrawText(
                    e.Graphics,
                    e.Text,
                    e.TextFont,
                    e.TextRectangle,
                    Drawing.Color.FromArgb(color.R, color.G, color.B),
                    Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.Left | Forms.TextFormatFlags.EndEllipsis);
            }
        }
    }
}
