using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PresentationPen.Services
{
    public sealed class AppSettings
    {
        public string HotkeyDraw { get; set; } = "Ctrl+Shift+D";
        public string HotkeyZoom { get; set; } = "Ctrl+Shift+Z";
        public string HotkeyWhiteboard { get; set; } = "Ctrl+Shift+W";
        public string HotkeyBlackboard { get; set; } = "Ctrl+Shift+K";
        public string HotkeyScreenshot { get; set; } = "Ctrl+Shift+S";

        /// <summary>Vazio = Imagens\PresentationPen.</summary>
        public string ScreenshotFolder { get; set; } = "";
        public bool CopyScreenshotToClipboard { get; set; } = true;

        /// <summary>Toca um som ao salvar (desligado: o som iria junto se você compartilha o áudio do PC).</summary>
        public bool ScreenshotSound { get; set; } = false;

        /// <summary>Vazio = Documentos\PresentationPen\Rascunhos.</summary>
        public string DraftsFolder { get; set; } = "";

        /// <summary>Ao sair do modo desenho, grava as alterações no rascunho aberto/salvo (se houver).</summary>
        public bool AutoSaveOpenDraftOnClose { get; set; } = true;

        /// <summary>Se true, os desenhos continuam lá ao fechar e reabrir o modo desenho.</summary>
        public bool KeepDrawingsOnClose { get; set; } = false;

        /// <summary>Se true, a barra de ferramentas, os avisos e a lista de rascunhos não aparecem no compartilhamento de tela nem nos screenshots.</summary>
        public bool HideToolbarFromCapture { get; set; } = true;

        public double DefaultPenSize { get; set; } = 4;
    }

    public sealed class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // grava "Ctrl+Shift+D" e acentos como estão (o padrão escaparia para +), já que o arquivo é editado à mão
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Folder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PresentationPen");

        public string FilePath => Path.Combine(Folder, "settings.json");

        public AppSettings Current { get; private set; } = new();

        /// <summary>Último erro de leitura do arquivo (null se ok).</summary>
        public string? LastError { get; private set; }

        /// <summary>Carrega o arquivo; retorna true se ele foi criado agora (primeira execução).</summary>
        public bool Load()
        {
            LastError = null;
            if (!File.Exists(FilePath))
            {
                Current = new AppSettings();
                Save();
                return true;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                // acrescenta ao arquivo as opções de versões novas, para aparecerem em "Editar configurações"
                string updated = JsonSerializer.Serialize(Current, JsonOptions);
                if (updated != json.Trim()) File.WriteAllText(FilePath, updated);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Current = new AppSettings();
            }
            return false;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
        }

        public string DraftsFolder =>
            string.IsNullOrWhiteSpace(Current.DraftsFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PresentationPen", "Rascunhos")
                : Environment.ExpandEnvironmentVariables(Current.DraftsFolder);

        public string ScreenshotFolder =>
            string.IsNullOrWhiteSpace(Current.ScreenshotFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PresentationPen")
                : Environment.ExpandEnvironmentVariables(Current.ScreenshotFolder);
    }
}
