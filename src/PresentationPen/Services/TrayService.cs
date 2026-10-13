using System;
using System.Diagnostics;
using System.IO;
using PresentationPen.Overlay;
using WinForms = System.Windows.Forms;

namespace PresentationPen.Services
{
    /// <summary>Ícone e menu na bandeja do sistema.</summary>
    public sealed class TrayService : IDisposable
    {
        private readonly AppController _app;
        private readonly WinForms.NotifyIcon _icon;
        private string? _balloonPath;

        public TrayService(AppController app)
        {
            _app = app;
            _icon = new WinForms.NotifyIcon
            {
                Icon = IconFactory.CreateTrayIcon(),
                Text = "PresentationPen",
                Visible = true,
            };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left) _app.ToggleDraw();
            };
            _icon.BalloonTipClicked += (_, _) => OpenBalloonTarget();
            BuildMenu();
        }

        public void RefreshMenu() => BuildMenu();

        public void ShowInfo(string title, string text, string? fileToSelect = null) =>
            ShowBalloon(title, text, WinForms.ToolTipIcon.Info, fileToSelect);

        public void ShowWarning(string title, string text) =>
            ShowBalloon(title, text, WinForms.ToolTipIcon.Warning, null);

        private void ShowBalloon(string title, string text, WinForms.ToolTipIcon icon, string? fileToSelect)
        {
            _balloonPath = fileToSelect;
            _icon.ShowBalloonTip(3000, title, text, icon);
        }

        private void OpenBalloonTarget()
        {
            if (_balloonPath == null || !File.Exists(_balloonPath)) return;
            Process.Start("explorer.exe", $"/select,\"{_balloonPath}\"");
        }

        private void BuildMenu()
        {
            var s = _app.Settings;
            var menu = new WinForms.ContextMenuStrip();

            menu.Items.Add(Item("Desenhar na tela", s.HotkeyDraw, _app.ToggleDraw, bold: true));
            menu.Items.Add(Item("Zoom", s.HotkeyZoom, _app.ToggleZoom));
            menu.Items.Add(Item("Quadro branco", s.HotkeyWhiteboard, () => _app.ToggleBoard(BoardMode.White)));
            menu.Items.Add(Item("Quadro preto", s.HotkeyBlackboard, () => _app.ToggleBoard(BoardMode.Black)));
            menu.Items.Add(Item("Salvar screenshot", s.HotkeyScreenshot, _app.TakeScreenshot));
            menu.Items.Add(Item("Abrir rascunho...", null, _app.OpenDraft));
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(Item("Abrir pasta de screenshots", null, _app.OpenScreenshotFolder));

            var startup = new WinForms.ToolStripMenuItem("Iniciar com o Windows") { Checked = StartupService.IsEnabled };
            startup.Click += (_, _) =>
            {
                StartupService.SetEnabled(!startup.Checked);
                startup.Checked = StartupService.IsEnabled;
            };
            menu.Items.Add(startup);

            menu.Items.Add(Item("Editar configurações...", null, _app.EditSettings));
            menu.Items.Add(Item("Recarregar configurações", null, _app.ReloadSettings));
            menu.Items.Add(Item("Atalhos de teclado", null, _app.ShowHelp));
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(Item("Sair", null, _app.Exit));

            var old = _icon.ContextMenuStrip;
            _icon.ContextMenuStrip = menu;
            old?.Dispose();
        }

        private static WinForms.ToolStripMenuItem Item(string text, string? shortcut, Action onClick, bool bold = false)
        {
            var item = new WinForms.ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut };
            if (bold) item.Font = new System.Drawing.Font(item.Font, System.Drawing.FontStyle.Bold);
            item.Click += (_, _) => onClick();
            return item;
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip?.Dispose();
            _icon.Icon?.Dispose();
            _icon.Dispose();
        }
    }
}
