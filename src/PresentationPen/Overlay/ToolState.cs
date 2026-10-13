using System.Windows.Media;

namespace PresentationPen.Overlay
{
    public enum DrawTool { Pen, Highlighter, Eraser, Line, Arrow, Rectangle, Ellipse, Text }

    public enum BoardMode { None, White, Black }

    public static class Palette
    {
        public static readonly Color[] Swatches =
        {
            Color.FromRgb(0xE5, 0x39, 0x35), // vermelho
            Color.FromRgb(0x43, 0xA0, 0x47), // verde
            Color.FromRgb(0x1E, 0x88, 0xE5), // azul
            Color.FromRgb(0xFD, 0xD8, 0x35), // amarelo
            Color.FromRgb(0xFB, 0x8C, 0x00), // laranja
            Colors.Black,                    // preto (vira branco no quadro preto)
        };

        public const int AdaptiveIndex = 5;

        public static Color Resolve(int index, BoardMode board) =>
            index == AdaptiveIndex && board == BoardMode.Black ? Colors.White : Swatches[index];
    }
}
