using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PresentationPen.Interop;
using Drawing = System.Drawing;

namespace PresentationPen.Notifications
{
    /// <summary>
    /// Aviso próprio no canto da tela (screenshot salvo, rascunho salvo, erros). Não depende das notificações do Windows
    /// (que o "Não incomodar" bloqueia durante o compartilhamento) e não aparece na captura.
    /// </summary>
    public partial class ScreenshotToast : Window
    {
        private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(4) };
        private string? _path;

        public ScreenshotToast()
        {
            InitializeComponent();

            SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                    ex | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
                ApplyCaptureExclusion(HideFromCapture);
            };

            _hideTimer.Tick += (_, _) => FadeOut();
            MouseEnter += (_, _) => _hideTimer.Stop();
            MouseLeave += (_, _) => _hideTimer.Start();

            Card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (_canOpen) OpenFile();
                else ShowInFolder();
            };
            OpenButton.Click += (_, _) => OpenFile();
            FolderButton.Click += (_, _) => ShowInFolder();
            CloseButton.Click += (_, _) => HideNow();
        }

        private bool _canOpen;

        /// <summary>Se true (padrão), o aviso não aparece no compartilhamento de tela.</summary>
        public bool HideFromCapture { get; set; } = true;

        private void ApplyCaptureExclusion(bool hide)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            NativeMethods.SetWindowDisplayAffinity(hwnd,
                hide ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);
        }

        /// <param name="path">Arquivo relacionado (mostra nome e "Mostrar na pasta"); null para só uma mensagem.</param>
        /// <param name="canOpen">Mostra o botão "Abrir" (abre o arquivo no programa padrão).</param>
        /// <param name="workArea">Área útil do monitor (pixels físicos), sem a barra de tarefas.</param>
        /// <param name="owner">Overlay visível, para o aviso ficar acima dele; null se não houver.</param>
        public void ShowFor(string title, string? path, BitmapSource? thumbnail, string? note, bool canOpen,
            Drawing.Rectangle workArea, Window? owner, bool isError = false)
        {
            _path = path;
            _canOpen = canOpen && path != null;
            TitleText.Text = title;
            IconText.Text = isError ? "" : ""; // aviso / check
            IconText.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            Thumb.Source = thumbnail;
            ThumbBorder.Visibility = thumbnail != null ? Visibility.Visible : Visibility.Collapsed;
            FileText.Text = path != null ? Path.GetFileName(path) : "";
            FileText.Visibility = path != null ? Visibility.Visible : Visibility.Collapsed;
            NoteText.Text = note ?? "";
            NoteText.Visibility = string.IsNullOrEmpty(note) ? Visibility.Collapsed : Visibility.Visible;
            OpenButton.Visibility = _canOpen ? Visibility.Visible : Visibility.Collapsed;
            FolderButton.Visibility = path != null ? Visibility.Visible : Visibility.Collapsed;
            Card.Cursor = path != null ? Cursors.Hand : Cursors.Arrow;

            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            if (Owner != owner && !IsVisible) Owner = owner;

            Show();
            ApplyCaptureExclusion(HideFromCapture);
            UpdateLayout();

            double scale = NativeMethods.GetDpiScale(workArea);
            int width = (int)(ActualWidth * scale);
            int height = (int)(ActualHeight * scale);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
                workArea.Right - width, workArea.Bottom - height, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void FadeOut()
        {
            _hideTimer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) =>
            {
                if (Opacity == 0) HideNow();
            };
            BeginAnimation(OpacityProperty, fade);
        }

        private void HideNow()
        {
            _hideTimer.Stop();
            BeginAnimation(OpacityProperty, null);
            Hide();
            Thumb.Source = null;
        }

        private void OpenFile()
        {
            if (_path == null || !File.Exists(_path)) return;
            Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
            HideNow();
        }

        private void ShowInFolder()
        {
            if (_path == null || !File.Exists(_path)) return;
            Process.Start("explorer.exe", $"/select,\"{_path}\"");
            HideNow();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // evita que o clique "atravesse" para o overlay
            e.Handled = true;
            base.OnMouseLeftButtonDown(e);
        }
    }
}
