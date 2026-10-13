using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using PresentationPen.Overlay;

namespace PresentationPen.Services
{
    /// <summary>Uma forma ou texto do rascunho.</summary>
    public sealed class DraftElement
    {
        public string Kind { get; set; } = "shape"; // "shape" | "text"
        public DrawTool Tool { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public string Color { get; set; } = "#FF000000";
        public double Size { get; set; }
        public string? Text { get; set; }
    }

    /// <summary>Conteúdo de um rascunho (arquivo .ppen = zip com draft.json, ink.isf, background.png, thumb.png).</summary>
    public sealed class DraftDocument
    {
        public int Version { get; set; } = 1;
        public BoardMode Board { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<DraftElement> Elements { get; set; } = new();

        [JsonIgnore] public byte[] Ink { get; set; } = Array.Empty<byte>();
        [JsonIgnore] public BitmapSource? Background { get; set; }
        [JsonIgnore] public BitmapSource? Thumbnail { get; set; }
    }

    public sealed record DraftInfo(string Path, string Name, DateTime Modified, BitmapSource? Thumbnail, bool HasBackground);

    public static class DraftStore
    {
        public const string Extension = ".ppen";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public static string NewPath(string folder)
        {
            Directory.CreateDirectory(folder);
            string baseName = $"Rascunho {DateTime.Now:yyyy-MM-dd HH-mm}";
            string path = Path.Combine(folder, baseName + Extension);
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(folder, $"{baseName} ({i}){Extension}");
            return path;
        }

        public static void Save(string path, DraftDocument doc)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // grava num temporário e troca no fim, para não corromper o rascunho se algo falhar no meio
            string temp = path + ".tmp";
            try
            {
                using (var file = File.Create(temp))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    WriteEntry(zip, "draft.json", s => JsonSerializer.Serialize(s, doc, JsonOptions));
                    WriteEntry(zip, "ink.isf", s => s.Write(doc.Ink));
                    if (doc.Background != null) WriteEntry(zip, "background.png", s => EncodePng(doc.Background, s));
                    if (doc.Thumbnail != null) WriteEntry(zip, "thumb.png", s => EncodePng(doc.Thumbnail, s));
                }
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                File.Delete(temp);
                throw;
            }
        }

        public static DraftDocument Load(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            var json = zip.GetEntry("draft.json") ?? throw new InvalidDataException("Arquivo de rascunho inválido.");
            DraftDocument doc;
            using (var s = json.Open())
                doc = JsonSerializer.Deserialize<DraftDocument>(s, JsonOptions) ?? new DraftDocument();

            var ink = zip.GetEntry("ink.isf");
            if (ink != null)
            {
                using var s = ink.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                doc.Ink = ms.ToArray();
            }
            doc.Background = ReadImage(zip, "background.png");
            doc.Thumbnail = ReadImage(zip, "thumb.png");
            return doc;
        }

        /// <summary>Lista os rascunhos da pasta, do mais recente para o mais antigo.</summary>
        public static List<DraftInfo> List(string folder)
        {
            if (!Directory.Exists(folder)) return new List<DraftInfo>();
            var result = new List<DraftInfo>();
            foreach (var file in new DirectoryInfo(folder).GetFiles("*" + Extension).OrderByDescending(f => f.LastWriteTime))
            {
                try
                {
                    using var zip = ZipFile.OpenRead(file.FullName);
                    result.Add(new DraftInfo(file.FullName, Path.GetFileNameWithoutExtension(file.Name),
                        file.LastWriteTime, ReadImage(zip, "thumb.png"), zip.GetEntry("background.png") != null));
                }
                catch (Exception)
                {
                    // arquivo corrompido ou de outro programa: ignora na lista
                }
            }
            return result;
        }

        private static void WriteEntry(ZipArchive zip, string name, Action<Stream> write)
        {
            using var s = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
            write(s);
        }

        private static void EncodePng(BitmapSource image, Stream s)
        {
            // o encoder do WPF precisa de um stream com Seek; a entrada do zip não tem
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(ms);
            ms.Position = 0;
            ms.CopyTo(s);
        }

        private static BitmapSource? ReadImage(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            if (entry == null) return null;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
