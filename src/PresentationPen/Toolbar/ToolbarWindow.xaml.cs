using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using PresentationPen.Interop;
using PresentationPen.Overlay;
using Drawing = System.Drawing;

namespace PresentationPen.Toolbar
{
    /// <summary>Barra flutuante que não rouba o foco do overlay e (opcionalmente) não aparece na captura.</summary>
    public partial class ToolbarWindow : Window
    {
        private const string IconFont = "Segoe MDL2 Assets";
        private const string SymbolFont = "Segoe UI Symbol";

        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x7E, 0xFF));
        private static readonly Brush SwatchBorder = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));

        private readonly OverlayWindow _overlay;
        private readonly Dictionary<DrawTool, Button> _toolButtons = new();
        private readonly List<Ellipse> _swatches = new();
        private Button _whiteboardButton = null!;
        private Button _blackboardButton = null!;
        private Button _zoomButton = null!;
        private Button _backgroundButton = null!;
        private bool _excludeFromCapture = true;
        private bool _syncing;
        private bool _allowClose;

        public ToolbarWindow(OverlayWindow overlay)
        {
            InitializeComponent();
            _overlay = overlay;
            BuildButtons();

            SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                    ex | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
                ApplyCaptureExclusion(_excludeFromCapture);
            };
            DragHandle.MouseLeftButtonDown += (_, _) => DragMove();
            SizeSlider.ValueChanged += (_, e) =>
            {
                if (!_syncing) _overlay.SetPenSize(e.NewValue);
            };
            _overlay.ToolStateChanged += Sync;
            Sync();
        }

        /// <summary>True se a barra está de fato oculta do compartilhamento de tela.</summary>
        public bool IsExcludedFromCapture { get; private set; }

        public void ApplyCaptureExclusion(bool exclude)
        {
            _excludeFromCapture = exclude;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            IsExcludedFromCapture = NativeMethods.SetWindowDisplayAffinity(hwnd,
                exclude ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE) && exclude;
        }

        /// <summary>Centraliza no topo do monitor (coordenadas físicas).</summary>
        public void PlaceOn(Drawing.Rectangle monitor, double dpiScale)
        {
            UpdateLayout();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int width = (int)(ActualWidth * dpiScale);
            int x = monitor.Left + (monitor.Width - width) / 2;
            int y = monitor.Top + (int)(16 * dpiScale);
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose) e.Cancel = true;
            base.OnClosing(e);
        }

        private void BuildButtons()
        {
            AddTool(DrawTool.Pen, "", IconFont, "Caneta (P)");
            AddTool(DrawTool.Highlighter, "", IconFont, "Marca-texto (H)");
            AddTool(DrawTool.Eraser, "", IconFont, "Borracha (X)");
            AddTool(DrawTool.Line, "╱", SymbolFont, "Linha (L) — Shift: ângulos de 45°");
            AddTool(DrawTool.Arrow, "↗", SymbolFont, "Seta (A) — Shift: ângulos de 45°");
            AddTool(DrawTool.Rectangle, "▭", SymbolFont, "Retângulo (R) — Shift: quadrado");
            AddTool(DrawTool.Ellipse, "◯", SymbolFont, "Elipse (E) — Shift: círculo");
            AddTool(DrawTool.Text, "T", "Segoe UI", "Texto (T) — Esc ou Ctrl+Enter para concluir");

            for (int i = 0; i < Palette.Swatches.Length; i++)
            {
                int index = i;
                Brush fill = i == Palette.AdaptiveIndex
                    ? new LinearGradientBrush(new GradientStopCollection
                    {
                        new GradientStop(Colors.Black, 0), new GradientStop(Colors.Black, 0.5),
                        new GradientStop(Colors.White, 0.5), new GradientStop(Colors.White, 1),
                    }, 45)
                    : new SolidColorBrush(Palette.Swatches[i]);
                var swatch = new Ellipse { Width = 20, Height = 20, Fill = fill, Stroke = SwatchBorder, StrokeThickness = 1 };
                _swatches.Add(swatch);
                string tip = i == Palette.AdaptiveIndex ? "Preto / branco no quadro preto (6)" : $"Cor {i + 1}";
                ColorsPanel.Children.Add(MakeButton(swatch, tip, () => _overlay.SetColor(index), width: 28));
            }

            ActionsPanel.Children.Add(MakeButton("", "Desfazer (Ctrl+Z)", _overlay.Undo, IconFont));
            ActionsPanel.Children.Add(MakeButton("", "Refazer (Ctrl+Y)", _overlay.Redo, IconFont));
            ActionsPanel.Children.Add(MakeButton("", "Limpar tudo (Delete)", _overlay.ClearAll, IconFont));

            _whiteboardButton = MakeButton(BoardIcon(Brushes.White), "Quadro branco (W)",
                () => _overlay.ToggleBoard(BoardMode.White));
            _blackboardButton = MakeButton(BoardIcon(Brushes.Black), "Quadro preto (K)",
                () => _overlay.ToggleBoard(BoardMode.Black));
            _zoomButton = MakeButton("", "Zoom (Z) — roda do mouse ajusta, Espaço trava", _overlay.ToggleZoom, IconFont);
            ActionsPanel.Children.Add(_whiteboardButton);
            ActionsPanel.Children.Add(_blackboardButton);
            ActionsPanel.Children.Add(_zoomButton);
            _backgroundButton = MakeButton("", "Mostrar/ocultar a imagem de tela salva com o rascunho (B)",
                _overlay.ToggleSavedBackground, IconFont);
            ActionsPanel.Children.Add(_backgroundButton);
            ActionsPanel.Children.Add(MakeButton("", "Salvar rascunho para continuar depois (Ctrl+S)", _overlay.RequestDraftSave, IconFont));
            ActionsPanel.Children.Add(MakeButton("", "Abrir rascunho salvo (Ctrl+O)", _overlay.RequestDraftOpen, IconFont));
            ActionsPanel.Children.Add(MakeButton("", "Salvar screenshot (Ctrl+Shift+S)", _overlay.RequestScreenshot, IconFont));
            ActionsPanel.Children.Add(MakeButton("", "Atalhos (F1)", _overlay.RequestHelp, IconFont));
            ActionsPanel.Children.Add(MakeButton("", "Sair do modo desenho (Esc)", _overlay.CloseOverlay, IconFont));
        }

        private void AddTool(DrawTool tool, string glyph, string font, string tip)
        {
            var button = MakeButton(glyph, tip, () => _overlay.SetTool(tool), font);
            _toolButtons[tool] = button;
            ToolsPanel.Children.Add(button);
        }

        private Button MakeButton(object content, string tip, Action onClick, string? font = null, double width = 34)
        {
            var button = new Button
            {
                Content = content,
                ToolTip = tip,
                Style = (Style)FindResource("BarButton"),
                Width = width,
            };
            if (font != null) button.FontFamily = new FontFamily(font);
            button.Click += (_, _) => onClick();
            return button;
        }

        private static UIElement BoardIcon(Brush fill) =>
            new Border
            {
                Width = 20,
                Height = 15,
                CornerRadius = new CornerRadius(2),
                Background = fill,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
            };

        private void Sync()
        {
            foreach (var (tool, button) in _toolButtons)
                button.Background = tool == _overlay.Tool ? ActiveBrush : Brushes.Transparent;

            for (int i = 0; i < _swatches.Count; i++)
            {
                bool selected = i == _overlay.ColorIndex;
                _swatches[i].Stroke = selected ? Brushes.White : SwatchBorder;
                _swatches[i].StrokeThickness = selected ? 3 : 1;
            }

            _whiteboardButton.Background = _overlay.Board == BoardMode.White ? ActiveBrush : Brushes.Transparent;
            _blackboardButton.Background = _overlay.Board == BoardMode.Black ? ActiveBrush : Brushes.Transparent;
            _zoomButton.Background = _overlay.IsZoomed ? ActiveBrush : Brushes.Transparent;
            _zoomButton.Content = _overlay.IsZoomed && _overlay.IsPanLocked ? "" : ""; // cadeado quando travado
            // só faz sentido com um rascunho que tenha imagem de tela salva
            _backgroundButton.Visibility = _overlay.HasSavedBackground ? Visibility.Visible : Visibility.Collapsed;
            _backgroundButton.Background = _overlay.IsSavedBackgroundVisible ? ActiveBrush : Brushes.Transparent;

            _syncing = true;
            SizeSlider.Value = _overlay.PenSize;
            _syncing = false;
            SizeLabel.Text = _overlay.PenSize.ToString("0");
        }
    }
}
