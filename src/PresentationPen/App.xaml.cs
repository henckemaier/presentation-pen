using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PresentationPen.Services;
using WinForms = System.Windows.Forms;

namespace PresentationPen
{
    public partial class App : Application
    {
        private Mutex? _mutex;
        private bool _ownsMutex;
        private AppController? _controller;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, @"Local\PresentationPen.SingleInstance", out _ownsMutex);
            if (!_ownsMutex)
            {
                MessageBox.Show("O PresentationPen já está em execução (veja o ícone na bandeja do sistema).",
                    "PresentationPen", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);
            WinForms.Application.EnableVisualStyles();
            DispatcherUnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            {
                if (a.ExceptionObject is Exception ex) LogError(ex);
            };

            _controller = new AppController();
            _controller.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _controller?.Dispose();
            if (_ownsMutex) _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }

        private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            e.Handled = true;
        }

        /// <summary>Registra o erro em %AppData%\PresentationPen\error.log.</summary>
        public static void LogError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(SettingsService.Folder);
                File.AppendAllText(Path.Combine(SettingsService.Folder, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // sem onde registrar; ignora
            }
        }
    }
}
