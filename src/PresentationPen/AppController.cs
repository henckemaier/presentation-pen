using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using PresentationPen.Notifications;
using PresentationPen.Overlay;
using PresentationPen.Services;
using WinForms = System.Windows.Forms;

namespace PresentationPen
{
    /// <summary>Liga bandeja, atalhos globais, overlay e screenshots.</summary>
    public sealed class AppController : IDisposable
    {
        private readonly SettingsService _settings = new();
        private HotkeyService? _hotkeys;
        private TrayService? _tray;
        private OverlayWindow? _overlay;
        private ScreenshotToast? _toast;
        private bool _disposed;

        public AppSettings Settings => _settings.Current;

        private OverlayWindow Overlay => _overlay!;

        public void Start()
        {
            bool firstRun = _settings.Load();
            // instalado pelo Setup: as opções escolhidas lá (incluindo iniciar com o Windows) valem.
            // sem instalador: na primeira execução liga o "iniciar com o Windows"; depois só atualiza o caminho do exe
            bool fromInstaller = InstallSettings.ApplyIfNew(_settings);
            if (!fromInstaller && (firstRun || StartupService.IsEnabled)) StartupService.SetEnabled(true);

            _overlay = new OverlayWindow(() => Settings, () => _settings.DraftsFolder);
            _overlay.SessionCleared += () => RememberSessionDraft(null);
            // desenhos da última vez (inclusive antes de reiniciar o PC) voltam na primeira abertura
            if (Settings.ReopenDrawings) _pendingRestore = ReadSessionDraft();
            _overlay.ScreenshotRequested += TakeScreenshot;
            _overlay.HelpRequested += ShowHelp;
            _overlay.DraftSaveRequested += SaveDraft;
            _overlay.DraftOpenRequested += OpenDraft;
            _overlay.DraftAutoSaved += OnDraftAutoSaved;
            _overlay.DraftAutoSaveFailed += message =>
                ShowToast("Falha ao salvar rascunho", null, null, message, false, isError: true);

            _toast = new ScreenshotToast();
            _hotkeys = new HotkeyService();
            _tray = new TrayService(this);
            RegisterHotkeys();

            if (_settings.LastError != null)
                _tray.ShowWarning("Erro em settings.json", $"Usando configurações padrão. {_settings.LastError}");
            else
                _tray.ShowInfo("PresentationPen ativo", $"{Settings.HotkeyDraw} para desenhar na tela.");
        }

        private void RegisterHotkeys()
        {
            _hotkeys!.UnregisterAll();
            var failed = new List<string>();

            void Register(string gesture, Action action)
            {
                if (!_hotkeys.Register(gesture, action)) failed.Add(gesture);
            }

            Register(Settings.HotkeyDraw, ToggleDraw);
            Register(Settings.HotkeyZoom, ToggleZoom);
            Register(Settings.HotkeyWhiteboard, () => ToggleBoard(BoardMode.White));
            Register(Settings.HotkeyBlackboard, () => ToggleBoard(BoardMode.Black));
            Register(Settings.HotkeyScreenshot, TakeScreenshot);

            if (failed.Count > 0)
                _tray!.ShowWarning("Atalhos indisponíveis",
                    $"Não foi possível registrar: {string.Join(", ", failed)}. Outro programa pode estar usando. Altere em settings.json.");
        }

        // ---------------------------------------------------------------- modos

        public void ToggleDraw()
        {
            if (Overlay.IsVisible) Overlay.CloseOverlay();
            else Open(BoardMode.None, zoom: false);
        }

        public void ToggleZoom()
        {
            if (Overlay.IsVisible) Overlay.ToggleZoom();
            else Open(BoardMode.None, zoom: true);
        }

        public void ToggleBoard(BoardMode mode)
        {
            if (Overlay.IsVisible) Overlay.ToggleBoard(mode);
            else Open(mode, zoom: false);
        }

        private void Open(BoardMode board, bool zoom)
        {
            RestorePendingSession();
            var screen = WinForms.Screen.FromPoint(WinForms.Cursor.Position);
            BitmapSource? snapshot = null;
            if (zoom)
            {
                using var bmp = ScreenCaptureService.CaptureBitmap(screen.Bounds);
                snapshot = ScreenCaptureService.ToBitmapSource(bmp);
            }
            // voltando com desenhos de antes: mantém o quadro (branco/preto) em que eles foram feitos
            if (board == BoardMode.None && Overlay.HasDrawings) board = Overlay.Board;
            Overlay.ShowOn(screen.Bounds, board, snapshot);
        }

        // ---------------------------------------------------------------- sessão (desenhos que voltam ao reabrir)

        private string? _pendingRestore;

        private static string SessionFile => Path.Combine(SettingsService.Folder, "last-session.txt");

        /// <summary>Guarda qual rascunho tem os desenhos atuais, para reabri-los depois de reiniciar o app.</summary>
        private static void RememberSessionDraft(string? path)
        {
            try
            {
                Directory.CreateDirectory(SettingsService.Folder);
                if (path == null) File.Delete(SessionFile);
                else File.WriteAllText(SessionFile, path);
            }
            catch (IOException) { }
        }

        private static string? ReadSessionDraft()
        {
            try
            {
                string path = File.Exists(SessionFile) ? File.ReadAllText(SessionFile).Trim() : "";
                return path.Length > 0 && File.Exists(path) ? path : null;
            }
            catch (IOException) { return null; }
        }

        /// <summary>Na primeira abertura depois de iniciar o app, carrega os desenhos da última sessão.</summary>
        private void RestorePendingSession()
        {
            string? path = _pendingRestore;
            _pendingRestore = null;
            if (path == null || Overlay.HasDrawings || Overlay.CurrentDraftPath != null) return;
            try
            {
                // sem a imagem de fundo: os desenhos voltam sobre a tela atual, como ao sair e voltar (B mostra o fundo)
                Overlay.LoadDraft(DraftStore.Load(path), path, withBackground: false);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
            }
        }

        // ---------------------------------------------------------------- screenshot

        public async void TakeScreenshot()
        {
            bool overlayVisible = Overlay.IsVisible;
            bool hideToolbar = overlayVisible && !Overlay.Bar.IsExcludedFromCapture;
            try
            {
                var bounds = overlayVisible
                    ? Overlay.MonitorBounds
                    : WinForms.Screen.FromPoint(WinForms.Cursor.Position).Bounds;

                if (overlayVisible) Overlay.PrepareForCapture();
                if (hideToolbar) Overlay.Bar.Hide();
                await Task.Delay(hideToolbar ? 150 : 30); // deixa a tela redesenhar

                string path;
                BitmapSource image;
                using (var bmp = ScreenCaptureService.CaptureBitmap(bounds))
                {
                    path = ScreenCaptureService.SavePng(bmp, _settings.ScreenshotFolder);
                    image = ScreenCaptureService.ToBitmapSource(bmp);
                }
                bool copied = Settings.CopyScreenshotToClipboard && ScreenCaptureService.CopyToClipboard(image);

                if (hideToolbar && Overlay.IsVisible) Overlay.Bar.Show();
                hideToolbar = false;

                ShowToast("Screenshot salvo", path, image,
                    copied ? "Copiado para a área de transferência" : null, canOpen: true, bounds);
                if (Settings.ScreenshotSound) SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                _tray!.ShowWarning("Falha ao salvar screenshot", ex.Message);
            }
            finally
            {
                if (hideToolbar && Overlay.IsVisible) Overlay.Bar.Show();
            }
        }

        private void ShowToast(string title, string? path, BitmapSource? thumbnail, string? note, bool canOpen,
            System.Drawing.Rectangle? monitor = null, bool isError = false)
        {
            var bounds = monitor
                ?? (Overlay.IsVisible ? Overlay.MonitorBounds : WinForms.Screen.FromPoint(WinForms.Cursor.Position).Bounds);
            var workArea = WinForms.Screen.FromRectangle(bounds).WorkingArea;
            _toast!.HideFromCapture = Settings.HideToolbarFromCapture;
            _toast.ShowFor(title, path, thumbnail, note, canOpen, workArea, Overlay.IsVisible ? Overlay : null, isError);
        }

        // ---------------------------------------------------------------- rascunhos

        public async void SaveDraft()
        {
            if (!Overlay.IsVisible || !Overlay.HasDrawings)
            {
                ShowToast("Nada para salvar", null, null, "Desenhe algo no modo desenho e use Ctrl+S.", false, isError: true);
                return;
            }
            try
            {
                bool updating = Overlay.CurrentDraftPath != null;
                var (path, thumbnail) = await Overlay.SaveDraftAsync(_settings.DraftsFolder);
                RememberSessionDraft(path);
                ShowToast(updating ? "Rascunho atualizado" : "Rascunho salvo", path, thumbnail,
                    "Abra depois com Ctrl+O no modo desenho ou pelo menu da bandeja.", canOpen: false);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                ShowToast("Falha ao salvar rascunho", null, null, ex.Message, false, isError: true);
            }
        }

        public void OpenDraft()
        {
            Window? owner = Overlay.IsVisible ? Overlay : null;
            var picker = new DraftPickerWindow(_settings.DraftsFolder, owner, Settings.HideToolbarFromCapture);
            bool accepted = picker.ShowDialog() == true;
            foreach (var (oldPath, newPath) in picker.Renames) Overlay.OnDraftRenamed(oldPath, newPath);
            if (!accepted || picker.SelectedPath == null) return;

            DraftDocument doc;
            try
            {
                doc = DraftStore.Load(picker.SelectedPath);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                ShowToast("Não foi possível abrir o rascunho", picker.SelectedPath, null, ex.Message, false, isError: true);
                return;
            }

            if (Overlay.IsVisible && Overlay.IsDirty && Overlay.CurrentDraftPath != picker.SelectedPath)
            {
                var answer = MessageBox.Show(Overlay,
                    "Os desenhos atuais não estão salvos em um rascunho.\n\nDescartar e abrir o rascunho escolhido?",
                    "PresentationPen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
            }

            if (!Overlay.IsVisible) Open(doc.Board, zoom: false);
            Overlay.LoadDraft(doc, picker.SelectedPath, picker.UseBackground);
            RememberSessionDraft(picker.SelectedPath);
            Overlay.Activate();
        }

        private void OnDraftAutoSaved(string path, BitmapSource? thumbnail)
        {
            RememberSessionDraft(path);
            ShowToast("Rascunho salvo", path, thumbnail,
                Settings.ReopenDrawings
                    ? "Os desenhos voltam quando você reabrir o modo desenho. Delete limpa e começa outro."
                    : "Abra depois com Ctrl+O no modo desenho ou pelo menu da bandeja.",
                canOpen: false);
        }

        // ---------------------------------------------------------------- menu

        public void OpenScreenshotFolder()
        {
            Directory.CreateDirectory(_settings.ScreenshotFolder);
            Process.Start("explorer.exe", $"\"{_settings.ScreenshotFolder}\"");
        }

        public void EditSettings()
        {
            if (!File.Exists(_settings.FilePath)) _settings.Save();
            Process.Start("notepad.exe", $"\"{_settings.FilePath}\"");
            _tray!.ShowInfo("Configurações", "Depois de salvar o arquivo, use \"Recarregar configurações\" no menu.");
        }

        public void ReloadSettings()
        {
            _settings.Load();
            RegisterHotkeys();
            _tray!.RefreshMenu();
            if (_settings.LastError != null)
                _tray.ShowWarning("Erro em settings.json", _settings.LastError);
            else
                _tray.ShowInfo("Configurações recarregadas", "Atalhos atualizados.");
        }

        public void ShowHelp()
        {
            var s = Settings;
            string text =
                "ATALHOS GLOBAIS\n" +
                $"  {s.HotkeyDraw}\tDesenhar na tela (liga/desliga)\n" +
                $"  {s.HotkeyZoom}\tZoom\n" +
                $"  {s.HotkeyWhiteboard}\tQuadro branco\n" +
                $"  {s.HotkeyBlackboard}\tQuadro preto\n" +
                $"  {s.HotkeyScreenshot}\tSalvar screenshot (tela + desenhos)\n\n" +
                "NO MODO DESENHO\n" +
                "  P  caneta        H  marca-texto     X  borracha\n" +
                "  L  linha         A  seta            R  retângulo\n" +
                "  E  elipse        T  texto           Shift  reto/quadrado\n" +
                "  1-6  cores       + / -  ou roda do mouse  espessura\n" +
                "  Ctrl+Z / Ctrl+Y  desfazer / refazer    Delete  limpar tudo\n" +
                "  W / K  quadro branco / preto      Z  zoom\n" +
                "  F1  ajuda     Esc  sair\n\n" +
                "RASCUNHOS\n" +
                "  Ctrl+S  salvar rascunho (grava no rascunho aberto, se houver)\n" +
                "  Ctrl+O  abrir um rascunho salvo para continuar (F2 na lista renomeia)\n" +
                "  B  mostrar/ocultar a imagem de tela salva com o rascunho\n" +
                "  Ao sair (Esc ou o atalho de desenhar), os desenhos são salvos como rascunho\n" +
                "  e voltam quando você reabrir. Delete limpa e começa um rascunho novo.\n\n" +
                "NO ZOOM\n" +
                "  Roda do mouse  aproximar/afastar   Ctrl+roda  espessura\n" +
                "  Mover o mouse  navegar             Espaço ou botão direito  travar/soltar\n" +
                "  Ao começar a desenhar, a imagem trava sozinha (não anda enquanto você mira).\n" +
                "  O traço mantém na tela a espessura escolhida, em qualquer nível de zoom.";

            if (Overlay.IsVisible)
                MessageBox.Show(Overlay, text, "PresentationPen — atalhos", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(text, "PresentationPen — atalhos", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Exit()
        {
            Dispose();
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hotkeys?.Dispose();
            _tray?.Dispose();
            _toast?.Close();
            _overlay?.ForceClose();
        }
    }
}
