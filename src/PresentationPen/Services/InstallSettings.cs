using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PresentationPen.Services
{
    /// <summary>
    /// Opções escolhidas no instalador. O instalador grava install-settings.json ao lado do exe;
    /// na próxima vez que o app abre, elas são copiadas para o settings.json do usuário (uma vez por instalação).
    /// </summary>
    public static class InstallSettings
    {
        public static string FilePath => Path.Combine(AppContext.BaseDirectory, "install-settings.json");

        /// <summary>Guarda qual instalação já foi aplicada (data do arquivo), para não sobrescrever mudanças feitas depois.</summary>
        private static string MarkerPath => Path.Combine(SettingsService.Folder, "install-applied.txt");

        /// <returns>true se aplicou opções de uma instalação nova (inclusive "iniciar com o Windows").</returns>
        public static bool ApplyIfNew(SettingsService settings)
        {
            if (!File.Exists(FilePath)) return false;

            string stamp = File.GetLastWriteTimeUtc(FilePath).Ticks.ToString();
            if (File.Exists(MarkerPath) && File.ReadAllText(MarkerPath).Trim() == stamp) return false;

            JsonObject? json;
            try
            {
                json = JsonNode.Parse(File.ReadAllText(FilePath), documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }) as JsonObject;
            }
            catch (JsonException)
            {
                return false;
            }
            if (json == null) return false;

            var s = settings.Current;
            // pastas: vazio = padrão do app (Imagens / Documentos do usuário que estiver usando)
            if (TryString(json, "ScreenshotFolder", out var screenshots)) s.ScreenshotFolder = screenshots;
            if (TryString(json, "DraftsFolder", out var drafts)) s.DraftsFolder = drafts;
            // atalhos: vazio = mantém o atual
            if (TryString(json, "HotkeyDraw", out var draw) && draw.Length > 0) s.HotkeyDraw = draw;
            if (TryString(json, "HotkeyZoom", out var zoom) && zoom.Length > 0) s.HotkeyZoom = zoom;
            if (TryString(json, "HotkeyWhiteboard", out var white) && white.Length > 0) s.HotkeyWhiteboard = white;
            if (TryString(json, "HotkeyBlackboard", out var black) && black.Length > 0) s.HotkeyBlackboard = black;
            if (TryString(json, "HotkeyScreenshot", out var shot) && shot.Length > 0) s.HotkeyScreenshot = shot;
            if (TryBool(json, "HideToolbarFromCapture", out var hide)) s.HideToolbarFromCapture = hide;
            settings.Save();

            if (TryBool(json, "StartWithWindows", out var startup)) StartupService.SetEnabled(startup);

            Directory.CreateDirectory(SettingsService.Folder);
            File.WriteAllText(MarkerPath, stamp);
            return true;
        }

        private static bool TryString(JsonObject json, string key, out string value)
        {
            value = "";
            if (json[key] is not JsonValue v || !v.TryGetValue(out string? text)) return false;
            value = text.Trim();
            return true;
        }

        private static bool TryBool(JsonObject json, string key, out bool value)
        {
            value = false;
            return json[key] is JsonValue v && v.TryGetValue(out value);
        }
    }
}
