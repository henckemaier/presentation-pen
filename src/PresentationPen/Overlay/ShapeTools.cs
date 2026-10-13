using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PresentationPen.Overlay
{
    /// <summary>Dados de uma forma desenhada (guardados em Path.Tag).</summary>
    public sealed record ShapeInfo(DrawTool Tool, Point Start, Point End);

    /// <summary>Cria e atualiza formas (linha, seta, retângulo, elipse) como Paths em coordenadas absolutas.</summary>
    public static class ShapeTools
    {
        public static bool IsShapeTool(DrawTool tool) =>
            tool is DrawTool.Line or DrawTool.Arrow or DrawTool.Rectangle or DrawTool.Ellipse;

        public static Path Create(DrawTool tool, Color color, double size)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return new Path
            {
                Stroke = brush,
                StrokeThickness = size,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = tool == DrawTool.Arrow ? brush : null,
                IsHitTestVisible = false,
            };
        }

        /// <param name="constrain">Shift: linhas em múltiplos de 45°, retângulo/elipse quadrados.</param>
        public static void Update(Path path, DrawTool tool, Point start, Point end, bool constrain)
        {
            if (constrain) end = Constrain(tool, start, end);
            path.Tag = new ShapeInfo(tool, start, end); // usado para salvar o rascunho

            path.Data = tool switch
            {
                DrawTool.Line => new LineGeometry(start, end),
                DrawTool.Arrow => ArrowGeometry(start, end, path.StrokeThickness),
                DrawTool.Rectangle => new RectangleGeometry(new Rect(start, end)),
                DrawTool.Ellipse => new EllipseGeometry(new Rect(start, end)),
                _ => null,
            };
        }

        private static Point Constrain(DrawTool tool, Point start, Point end)
        {
            Vector v = end - start;
            if (tool is DrawTool.Line or DrawTool.Arrow)
            {
                double step = Math.PI / 4;
                double angle = Math.Round(Math.Atan2(v.Y, v.X) / step) * step;
                return start + new Vector(Math.Cos(angle), Math.Sin(angle)) * v.Length;
            }

            double d = Math.Max(Math.Abs(v.X), Math.Abs(v.Y));
            return start + new Vector(Math.Sign(v.X) * d, Math.Sign(v.Y) * d);
        }

        private static Geometry ArrowGeometry(Point start, Point end, double size)
        {
            var group = new GeometryGroup();
            Vector v = end - start;
            double length = v.Length;
            if (length < 0.5) return group;

            v /= length;
            double head = Math.Min(length, Math.Max(14, size * 4));
            double halfWidth = head * 0.55;
            Point basePoint = end - v * head;
            var normal = new Vector(-v.Y, v.X);

            group.Children.Add(new LineGeometry(start, basePoint));
            var tip = new PathFigure(end, new PathSegment[]
            {
                new LineSegment(basePoint + normal * halfWidth, true),
                new LineSegment(basePoint - normal * halfWidth, true),
            }, closed: true);
            group.Children.Add(new PathGeometry(new[] { tip }));
            return group;
        }
    }
}
