using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using PresentationPen.Interop;
using PresentationPen.Services;
using PresentationPen.Toolbar;
using Drawing = System.Drawing;

namespace PresentationPen.Overlay
{
    /// <summary>Janela transparente sobre um monitor onde ficam os desenhos, o zoom e o quadro.</summary>
    public partial class OverlayWindow : Window
    {
        public const double MinPenSize = 1;
        public const double MaxPenSize = 30;
        private const double MinZoom = 1;
        private const double MaxZoom = 8;
        private const double InitialZoom = 2;

        private readonly Func<AppSettings> _settings;
        private readonly Func<string> _draftsFolder;
        private readonly UndoManager _undo = new();
        private readonly System.Threading.SemaphoreSlim _saveLock = new(1, 1);
        private IntPtr _hwnd;
        private Drawing.Rectangle _bounds;
        private bool _allowClose;
        private bool _toolbarOwned;
        private double _zoom = 1;

        private Path? _currentShape;
        private Point _shapeStart;
        private TextBox? _editingText;

        private string? _draftPath;
        private BitmapSource? _lastDraftBackground;
        private bool _dirty;

        public OverlayWindow(Func<AppSettings> settings, Func<string> draftsFolder)
        {
            InitializeComponent();
            _settings = settings;
            _draftsFolder = draftsFolder;
            PenSize = Math.Clamp(settings().DefaultPenSize, MinPenSize, MaxPenSize);
            Bar = new ToolbarWindow(this);

            Ink.Strokes.StrokesChanged += OnStrokesChanged;
            _undo.Changed += () => _dirty = true;
            ShapeCanvas.MouseLeftButtonDown += ShapeCanvas_MouseDown;
            ShapeCanvas.MouseMove += ShapeCanvas_MouseMove;
            ShapeCanvas.MouseLeftButtonUp += ShapeCanvas_MouseUp;
            PreviewKeyDown += OnPreviewKeyDown;
            PreviewMouseWheel += OnPreviewMouseWheel;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            SourceInitialized += (_, _) => EnableDwmTransparency();

            ApplyTool();
        }

        /// <summary>
        /// Deixa o fundo da janela transparente pela composição do DWM ("vidro" em toda a área).
        /// É acelerado por GPU, ao contrário de AllowsTransparency, que reenvia a tela inteira a cada quadro.
        /// </summary>
        private void EnableDwmTransparency()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
                target.BackgroundColor = Colors.Transparent;

            var margins = new NativeMethods.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Windows 11: sem cantos arredondados nem borda colorida em volta da tela
            int corner = NativeMethods.DWMWCP_DONOTROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            uint border = NativeMethods.DWMWA_COLOR_NONE;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref border, sizeof(uint));
        }

        public ToolbarWindow Bar { get; }
        public DrawTool Tool { get; private set; } = DrawTool.Pen;
        public int ColorIndex { get; private set; }
        public double PenSize { get; private set; }
        public BoardMode Board { get; private set; } = BoardMode.None;
        public bool IsZoomed { get; private set; }
        public bool IsPanLocked { get; private set; }
        public Drawing.Rectangle MonitorBounds => _bounds;

        public bool HasDrawings => Ink.Strokes.Count > 0 || ShapeCanvas.Children.Count > 0;

        /// <summary>Há alterações que ainda não estão em nenhum arquivo de rascunho.</summary>
        public bool IsDirty => _dirty && HasDrawings;

        /// <summary>Rascunho aberto/salvo nesta sessão (Ctrl+S grava nele).</summary>
        public string? CurrentDraftPath => _draftPath;

        public event Action? ToolStateChanged;
        public event Action? ScreenshotRequested;
        public event Action? HelpRequested;
        public event Action? DraftSaveRequested;
        public event Action? DraftOpenRequested;
        /// <summary>Rascunho gravado automaticamente ao sair (caminho, miniatura).</summary>
        public event Action<string, BitmapSource?>? DraftAutoSaved;
        public event Action<string>? DraftAutoSaveFailed;
        /// <summary>Saiu sem nenhum desenho na tela (a próxima abertura começa vazia).</summary>
        public event Action? SessionCleared;

        private Color CurrentColor => Palette.Resolve(ColorIndex, Board);
        /// <summary>
        /// Os desenhos ficam dentro do conteúdo ampliado; dividir pelo zoom faz o traço ter na tela
        /// a espessura escolhida (sem isso, uma caneta de 4 px viraria 16 px com zoom de 4×).
        /// </summary>
        private double ScreenScale => IsZoomed ? _zoom : 1;
        private double EffectivePenSize => PenSize / ScreenScale;
        private double TextFontSize => (12 + PenSize * 3) / ScreenScale;

        // ---------------------------------------------------------------- abrir / fechar

        /// <param name="zoomSnapshot">Se informado, abre já no modo zoom sobre esta captura.</param>
        public void ShowOn(Drawing.Rectangle bounds, BoardMode board, BitmapSource? zoomSnapshot)
        {
            _bounds = bounds;
            _hwnd = new WindowInteropHelper(this).EnsureHandle();

            Place();
            Show();
            Place(); // de novo: a troca de DPI entre monitores pode ter redimensionado a janela
            Activate();
            NativeMethods.SetForegroundWindow(_hwnd);
            UpdateLayout();

            SetBoard(board);
            if (zoomSnapshot != null) EnterZoom(zoomSnapshot);

            ShowToolbar();
            Focus();
            Dispatcher.BeginInvoke(Place, DispatcherPriority.Loaded);
            RaiseState();
        }

        /// <summary>
        /// Sai do modo desenho. Os desenhos alterados são salvos como rascunho (o atual ou um novo) e,
        /// por padrão, continuam lá ao voltar (ReopenDrawings).
        /// </summary>
        public void CloseOverlay()
        {
            if (!IsVisible) return;
            FinishText();
            CancelShape();

            // fundo para o rascunho: o que já está na tela (rascunho aberto ou zoom); senão captura depois de esconder
            BitmapSource? background = Board == BoardMode.None
                ? DraftBackground.Source as BitmapSource ?? (IsZoomed ? SnapshotImage.Source as BitmapSource : null)
                : null;
            bool save = _settings().AutoSaveDraftOnClose && _dirty && HasDrawings;
            if (IsZoomed) ExitZoom();

            Bar.Hide();
            Hide();

            if (!HasDrawings) SessionCleared?.Invoke();
            if (save) SaveOnCloseAsync(background);
            else if (!_settings().ReopenDrawings) ResetContent();
            RaiseState();
        }

        /// <summary>Limpa tudo e esquece o rascunho atual (próxima abertura começa do zero).</summary>
        private void ResetContent()
        {
            _undo.Apply(() =>
            {
                Ink.Strokes.Clear();
                ShapeCanvas.Children.Clear();
            });
            _undo.Clear();
            DraftBackground.Source = null;
            _lastDraftBackground = null;
            _draftPath = null;
            _dirty = false;
            SetBoard(BoardMode.None);
        }

        /// <summary>Fecha de verdade (ao sair do aplicativo).</summary>
        public void ForceClose()
        {
            _allowClose = true;
            Bar.ForceClose();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true; // Alt+F4 só esconde
                CloseOverlay();
            }
            base.OnClosing(e);
        }

        private void Place()
        {
            if (_hwnd == IntPtr.Zero) return;
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST,
                _bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height, NativeMethods.SWP_NOACTIVATE);
        }

        private void ShowToolbar()
        {
            if (!_toolbarOwned)
            {
                Bar.Owner = this;
                _toolbarOwned = true;
            }
            Bar.ApplyCaptureExclusion(_settings().HideToolbarFromCapture);
            Bar.Show();
            Bar.PlaceOn(_bounds, VisualTreeHelper.GetDpi(this).DpiScaleX);
        }

        /// <summary>Finaliza edições em andamento antes de um screenshot (tira o cursor de texto).</summary>
        public void PrepareForCapture() => FinishText();

        private void RaiseState() => ToolStateChanged?.Invoke();

        // ---------------------------------------------------------------- ferramentas

        public void SetTool(DrawTool tool)
        {
            FinishText();
            CancelShape();
            Tool = tool;
            ApplyTool();
            RaiseState();
        }

        public void SetColor(int index)
        {
            if (index < 0 || index >= Palette.Swatches.Length) return;
            ColorIndex = index;
            ApplyDrawingAttributes();
            if (_editingText != null)
            {
                var brush = new SolidColorBrush(CurrentColor);
                _editingText.Foreground = brush;
                _editingText.CaretBrush = brush;
            }
            RaiseState();
        }

        public void SetPenSize(double size)
        {
            PenSize = Math.Round(Math.Clamp(size, MinPenSize, MaxPenSize));
            ApplyDrawingAttributes();
            if (_editingText != null) _editingText.FontSize = TextFontSize;
            RaiseState();
        }

        private void ApplyTool()
        {
            switch (Tool)
            {
                case DrawTool.Pen:
                case DrawTool.Highlighter:
                    Ink.EditingMode = InkCanvasEditingMode.Ink;
                    Ink.IsHitTestVisible = true;
                    break;
                case DrawTool.Eraser:
                    Ink.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    Ink.IsHitTestVisible = true;
                    break;
                default:
                    Ink.EditingMode = InkCanvasEditingMode.None;
                    Ink.IsHitTestVisible = false;
                    break;
            }
            ShapeCanvas.Cursor = Tool == DrawTool.Text ? Cursors.IBeam : Cursors.Cross;
            ApplyDrawingAttributes();
        }

        private void ApplyDrawingAttributes()
        {
            bool highlighter = Tool == DrawTool.Highlighter;
            double size = (highlighter ? Math.Max(12, PenSize * 4) : PenSize) / ScreenScale;
            Ink.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = CurrentColor,
                Width = size,
                Height = size,
                FitToCurve = !highlighter,
                IsHighlighter = highlighter,
                IgnorePressure = true,
                StylusTip = StylusTip.Ellipse,
            };
        }

        // ---------------------------------------------------------------- desfazer / limpar

        public void Undo()
        {
            FinishText();
            _undo.Undo();
        }

        public void Redo()
        {
            FinishText();
            _undo.Redo();
        }

        public void ClearAll()
        {
            FinishText();
            CancelShape();
            if (Ink.Strokes.Count == 0 && ShapeCanvas.Children.Count == 0) return;
            var action = new ClearAction(Ink.Strokes, ShapeCanvas);
            _undo.Apply(action.Redo);
            _undo.Push(action);
            // depois de limpar, o próximo Ctrl+S cria um rascunho novo em vez de sobrescrever o anterior
            _draftPath = null;
        }

        private void OnStrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
        {
            if (_undo.IsApplying) return;
            _undo.Push(new StrokesChangedAction(Ink.Strokes, e.Added, e.Removed));
        }

        // ---------------------------------------------------------------- quadro branco / preto

        public void ToggleBoard(BoardMode mode)
        {
            SetBoard(Board == mode ? BoardMode.None : mode);
            RaiseState();
        }

        private void SetBoard(BoardMode mode)
        {
            Board = mode;
            BoardRect.Visibility = mode == BoardMode.None ? Visibility.Collapsed : Visibility.Visible;
            BoardRect.Fill = mode == BoardMode.Black
                ? new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14))
                : Brushes.White;

            // zoom aberto em cima do quadro não tem captura da tela por baixo
            if (mode == BoardMode.None && IsZoomed && SnapshotImage.Source == null && DraftBackground.Source == null)
                ExitZoom();

            UpdateSnapshotVisibility();
            ApplyDrawingAttributes();
        }

        // ---------------------------------------------------------------- zoom

        public async void ToggleZoom()
        {
            if (IsZoomed)
            {
                ExitZoom();
                RaiseState();
                return;
            }

            // com quadro ou imagem de fundo do rascunho não é preciso capturar a tela: eles já ampliam junto
            bool needsSnapshot = Board == BoardMode.None && DraftBackground.Source == null;
            BitmapSource? snapshot = needsSnapshot ? await CaptureBehindOverlayAsync() : null;
            EnterZoom(snapshot);
        }

        private void EnterZoom(BitmapSource? snapshot)
        {
            FinishText();
            CancelShape();
            IsZoomed = true;
            IsPanLocked = false;
            _zoom = InitialZoom;
            SnapshotImage.Source = snapshot;
            UpdateSnapshotVisibility();
            UpdateLayout();
            UpdatePan(Mouse.GetPosition(RootGrid));
            ApplyDrawingAttributes();
            RaiseState();
        }

        private void ExitZoom()
        {
            IsZoomed = false;
            IsPanLocked = false;
            _zoom = 1;
            ZoomScale.ScaleX = ZoomScale.ScaleY = 1;
            ZoomTranslate.X = ZoomTranslate.Y = 0;
            SnapshotImage.Source = null;
            UpdateSnapshotVisibility();
            ApplyDrawingAttributes();
            RefreshInkInputTransform();
        }

        /// <summary>Trava/destrava a imagem ampliada (Espaço, botão direito, ou sozinho ao começar a desenhar).</summary>
        private void SetPanLocked(bool locked)
        {
            if (!IsZoomed || IsPanLocked == locked) return;
            IsPanLocked = locked;
            if (!locked) UpdatePan(Mouse.GetPosition(RootGrid));
            RaiseState();
        }

        /// <summary>
        /// Muda o nível de zoom. Destravado, a imagem segue o mouse; travado, o ponto sob o cursor
        /// fica parado (dá para aproximar onde está desenhando sem a imagem "fugir").
        /// </summary>
        private void ZoomTo(double newZoom, Point mouse)
        {
            double old = _zoom;
            _zoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
            if (IsPanLocked)
            {
                double w = RootGrid.ActualWidth, h = RootGrid.ActualHeight;
                ZoomScale.ScaleX = ZoomScale.ScaleY = _zoom;
                ZoomTranslate.X = Math.Clamp(mouse.X - (mouse.X - ZoomTranslate.X) * (_zoom / old), -(w * _zoom - w), 0);
                ZoomTranslate.Y = Math.Clamp(mouse.Y - (mouse.Y - ZoomTranslate.Y) * (_zoom / old), -(h * _zoom - h), 0);
                RefreshInkInputTransform();
            }
            else
            {
                UpdatePan(mouse);
            }
            ApplyDrawingAttributes(); // mantém a espessura na tela
        }

        private void UpdateSnapshotVisibility()
        {
            SnapshotImage.Visibility = IsZoomed && Board == BoardMode.None && SnapshotImage.Source != null
                ? Visibility.Visible
                : Visibility.Collapsed;
            DraftBackground.Visibility = Board == BoardMode.None && DraftBackground.Source != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// O deslocamento acompanha o mouse (estilo ZoomIt): o ponto sob o cursor é sempre
        /// o mesmo ponto da tela original, então dá para "passear" pela tela ampliada.
        /// </summary>
        private void UpdatePan(Point mouse)
        {
            double w = RootGrid.ActualWidth, h = RootGrid.ActualHeight;
            if (w <= 0 || h <= 0) return;
            ZoomScale.ScaleX = ZoomScale.ScaleY = _zoom;
            double fx = Math.Clamp(mouse.X / w, 0, 1);
            double fy = Math.Clamp(mouse.Y / h, 0, 1);
            ZoomTranslate.X = -fx * (w * _zoom - w);
            ZoomTranslate.Y = -fy * (h * _zoom - h);
            RefreshInkInputTransform();
        }

        /// <summary>
        /// O desenho ao vivo do InkCanvas (DynamicRenderer) guarda a conversão tela → desenho e só a atualiza
        /// numa passagem de layout. Mudar o zoom mexe só na RenderTransform, que não dispara layout; aí o
        /// primeiro ponto de um traço saía com a posição antiga e aparecia uma linha reta até o início do traço.
        /// Invalidar o arranjo força o layout (prioridade Render, antes da próxima entrada do mouse).
        /// </summary>
        private void RefreshInkInputTransform() => Ink.InvalidateArrange();

        /// <summary>Captura o monitor sem o próprio overlay (para não duplicar os desenhos no zoom).</summary>
        private async Task<BitmapSource> CaptureBehindOverlayAsync()
        {
            bool excluded = NativeMethods.SetWindowDisplayAffinity(_hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            if (!excluded) RootGrid.Opacity = 0;
            await Task.Delay(100);
            try
            {
                using var bmp = ScreenCaptureService.CaptureBitmap(_bounds);
                return ScreenCaptureService.ToBitmapSource(bmp);
            }
            finally
            {
                if (excluded) NativeMethods.SetWindowDisplayAffinity(_hwnd, NativeMethods.WDA_NONE);
                else RootGrid.Opacity = 1;
            }
        }

        // ---------------------------------------------------------------- formas

        private void ShapeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(ShapeCanvas);
            if (Tool == DrawTool.Text)
            {
                FinishText();
                BeginText(p);
                e.Handled = true;
                return;
            }
            if (!ShapeTools.IsShapeTool(Tool)) return;

            _shapeStart = p;
            _currentShape = ShapeTools.Create(Tool, CurrentColor, EffectivePenSize);
            ShapeCanvas.Children.Add(_currentShape);
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void ShapeCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_currentShape == null) return;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            ShapeTools.Update(_currentShape, Tool, _shapeStart, e.GetPosition(ShapeCanvas), shift);
        }

        private void ShapeCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentShape == null) return;
            var shape = _currentShape;
            _currentShape = null;
            ShapeCanvas.ReleaseMouseCapture();

            if ((e.GetPosition(ShapeCanvas) - _shapeStart).Length < 3 || shape.Data == null)
                ShapeCanvas.Children.Remove(shape);
            else
                _undo.Push(new ElementAddedAction(ShapeCanvas, shape));
        }

        private void CancelShape()
        {
            if (_currentShape == null) return;
            ShapeCanvas.Children.Remove(_currentShape);
            _currentShape = null;
            ShapeCanvas.ReleaseMouseCapture();
        }

        // ---------------------------------------------------------------- texto

        private void BeginText(Point p)
        {
            var color = CurrentColor;
            var brush = new SolidColorBrush(color);
            var tb = new TextBox
            {
                Foreground = brush,
                CaretBrush = brush,
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                FontSize = TextFontSize,
                AcceptsReturn = true,
                MinWidth = 60,
            };
            Canvas.SetLeft(tb, p.X);
            Canvas.SetTop(tb, p.Y - TextFontSize * 0.7);
            tb.LostKeyboardFocus += (_, _) => FinishText();

            ShapeCanvas.Children.Add(tb);
            _editingText = tb;
            Dispatcher.BeginInvoke(() => Keyboard.Focus(tb), DispatcherPriority.Input);
        }

        private void FinishText()
        {
            var tb = _editingText;
            if (tb == null) return;
            _editingText = null;
            ShapeCanvas.Children.Remove(tb);

            string text = tb.Text.TrimEnd();
            if (text.Length > 0)
            {
                // compensa borda + margem interna do TextBox para o texto não "pular"
                var block = CreateTextBlock(text, tb.Foreground, tb.FontSize,
                    Canvas.GetLeft(tb) + 3, Canvas.GetTop(tb) + 1);
                ShapeCanvas.Children.Add(block);
                _undo.Push(new ElementAddedAction(ShapeCanvas, block));
            }
            Dispatcher.BeginInvoke(() => Focus(), DispatcherPriority.Input);
        }

        private static TextBlock CreateTextBlock(string text, Brush foreground, double fontSize, double left, double top)
        {
            var block = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                FontSize = fontSize,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            return block;
        }

        // ---------------------------------------------------------------- borracha em formas/textos

        private void EraseElementAt(Point p)
        {
            for (int i = ShapeCanvas.Children.Count - 1; i >= 0; i--)
            {
                var element = ShapeCanvas.Children[i];
                if (!HitsElement(element, p)) continue;

                int index = i;
                _undo.Apply(() => ShapeCanvas.Children.RemoveAt(index));
                _undo.Push(new ElementRemovedAction(ShapeCanvas, element, index));
                return;
            }
        }

        private static bool HitsElement(UIElement element, Point p)
        {
            switch (element)
            {
                case Path { Data: not null } path:
                    var pen = new Pen(Brushes.Black, path.StrokeThickness + 10);
                    return path.Data.StrokeContains(pen, p) || (path.Fill != null && path.Data.FillContains(p));
                case TextBlock block:
                    return new Rect(Canvas.GetLeft(block), Canvas.GetTop(block), block.ActualWidth, block.ActualHeight)
                        .Contains(p);
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- rascunhos

        /// <summary>
        /// Salva os desenhos num rascunho. Grava no rascunho atual (aberto ou já salvo) ou cria um novo na pasta.
        /// Junto vai a imagem da tela por trás dos desenhos, para poder continuar sobre ela depois.
        /// </summary>
        public async Task<(string Path, BitmapSource? Thumbnail)> SaveDraftAsync(string folder)
        {
            FinishText();
            CancelShape();

            BitmapSource? background = await GetBackgroundForSaveAsync();
            var doc = BuildDraft(background);
            string path = _draftPath ?? DraftStore.NewPath(folder);
            DraftStore.Save(path, doc);

            _draftPath = path;
            _lastDraftBackground = background;
            _dirty = false;
            RaiseState();
            return (path, doc.Thumbnail);
        }

        /// <summary>Substitui o conteúdo atual pelo rascunho.</summary>
        /// <param name="withBackground">Mostrar a imagem de tela salva por trás dos desenhos.</param>
        public void LoadDraft(DraftDocument doc, string path, bool withBackground)
        {
            FinishText();
            CancelShape();
            if (IsZoomed) ExitZoom();

            _undo.Apply(() =>
            {
                Ink.Strokes.Clear();
                ShapeCanvas.Children.Clear();
                if (doc.Ink.Length > 0)
                {
                    using var ms = new System.IO.MemoryStream(doc.Ink);
                    Ink.Strokes.Add(new StrokeCollection(ms));
                }
                foreach (var element in doc.Elements)
                {
                    var ui = CreateElement(element);
                    if (ui != null) ShapeCanvas.Children.Add(ui);
                }
            });
            _undo.Clear();

            // guarda o fundo mesmo se não for mostrado agora: dá para ligá-lo depois com B
            _lastDraftBackground = doc.Board == BoardMode.None ? doc.Background : null;
            DraftBackground.Source = withBackground ? _lastDraftBackground : null;
            SetBoard(doc.Board);

            _draftPath = path;
            _dirty = false;
            Focus();
            RaiseState();
        }

        /// <summary>Existe uma imagem de tela salva com o rascunho atual.</summary>
        public bool HasSavedBackground => _lastDraftBackground != null && Board == BoardMode.None;

        /// <summary>A imagem de tela salva está sendo mostrada por trás dos desenhos.</summary>
        public bool IsSavedBackgroundVisible => DraftBackground.Source != null && Board == BoardMode.None;

        /// <summary>Mostra/oculta a imagem de tela salva com o rascunho (tecla B).</summary>
        public void ToggleSavedBackground()
        {
            if (_lastDraftBackground == null || Board != BoardMode.None) return;
            if (IsZoomed) ExitZoom(); // o zoom foi aberto sobre outro fundo
            DraftBackground.Source = DraftBackground.Source == null ? _lastDraftBackground : null;
            UpdateSnapshotVisibility();
            RaiseState();
        }

        /// <summary>O arquivo do rascunho aberto foi renomeado na lista: continua salvando no novo nome.</summary>
        public void OnDraftRenamed(string oldPath, string newPath)
        {
            if (_draftPath != null && string.Equals(_draftPath, oldPath, StringComparison.OrdinalIgnoreCase))
                _draftPath = newPath;
        }

        /// <summary>
        /// Salva os desenhos ao sair (no rascunho atual ou num novo), sem perguntar. Roda depois de esconder
        /// a janela: assim a captura da tela de fundo sai sem os desenhos e o atalho responde na hora.
        /// </summary>
        private async void SaveOnCloseAsync(BitmapSource? background)
        {
            try
            {
                if (background == null && Board == BoardMode.None)
                {
                    await Task.Delay(150); // tempo para a janela sumir da tela
                    if (!IsVisible)
                    {
                        using var bmp = ScreenCaptureService.CaptureBitmap(_bounds);
                        background = ScreenCaptureService.ToBitmapSource(bmp);
                    }
                    else
                    {
                        background = _lastDraftBackground; // reabriu antes: a captura pegaria os desenhos
                    }
                }

                var doc = BuildDraft(background);
                string path = _draftPath ?? DraftStore.NewPath(_draftsFolder());
                _draftPath = path;
                _lastDraftBackground = background;
                _dirty = false; // mudanças feitas a partir daqui (se reabrir) entram no próximo salvamento

                await _saveLock.WaitAsync(); // salvamentos seguidos não podem escrever o mesmo arquivo juntos
                try { await Task.Run(() => DraftStore.Save(path, doc)); }
                finally { _saveLock.Release(); }
                DraftAutoSaved?.Invoke(path, doc.Thumbnail);
            }
            catch (Exception ex)
            {
                _dirty = true;
                App.LogError(ex);
                DraftAutoSaveFailed?.Invoke(ex.Message);
            }
            finally
            {
                if (!_settings().ReopenDrawings && !IsVisible) ResetContent();
            }
        }

        private async Task<BitmapSource?> GetBackgroundForSaveAsync()
        {
            if (Board != BoardMode.None) return null;
            if (DraftBackground.Source is BitmapSource loaded) return loaded;
            if (IsZoomed && SnapshotImage.Source is BitmapSource snapshot) return snapshot;
            return await CaptureBehindOverlayAsync();
        }

        private DraftDocument BuildDraft(BitmapSource? background)
        {
            var doc = new DraftDocument
            {
                Board = Board,
                Width = RootGrid.ActualWidth,
                Height = RootGrid.ActualHeight,
                Background = background,
                Thumbnail = RenderThumbnail(background),
            };

            using (var ms = new System.IO.MemoryStream())
            {
                Ink.Strokes.Save(ms);
                doc.Ink = ms.ToArray();
            }

            foreach (UIElement element in ShapeCanvas.Children)
            {
                switch (element)
                {
                    case Path { Tag: ShapeInfo info } path:
                        doc.Elements.Add(new DraftElement
                        {
                            Kind = "shape",
                            Tool = info.Tool,
                            X1 = info.Start.X, Y1 = info.Start.Y,
                            X2 = info.End.X, Y2 = info.End.Y,
                            Color = ColorOf(path.Stroke),
                            Size = path.StrokeThickness,
                        });
                        break;
                    case TextBlock block:
                        doc.Elements.Add(new DraftElement
                        {
                            Kind = "text",
                            X1 = Canvas.GetLeft(block), Y1 = Canvas.GetTop(block),
                            Color = ColorOf(block.Foreground),
                            Size = block.FontSize,
                            Text = block.Text,
                        });
                        break;
                }
            }
            return doc;
        }

        private static UIElement? CreateElement(DraftElement e)
        {
            Color color;
            try { color = (Color)ColorConverter.ConvertFromString(e.Color); }
            catch (FormatException) { color = Colors.Red; }

            if (e.Kind == "text" && !string.IsNullOrEmpty(e.Text))
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return CreateTextBlock(e.Text, brush, e.Size, e.X1, e.Y1);
            }
            if (e.Kind == "shape" && ShapeTools.IsShapeTool(e.Tool))
            {
                var path = ShapeTools.Create(e.Tool, color, e.Size);
                ShapeTools.Update(path, e.Tool, new Point(e.X1, e.Y1), new Point(e.X2, e.Y2), constrain: false);
                return path;
            }
            return null;
        }

        private static string ColorOf(Brush brush) =>
            brush is SolidColorBrush solid ? solid.Color.ToString() : Colors.Red.ToString();

        /// <summary>Miniatura (fundo + desenhos) para a lista de rascunhos.</summary>
        private BitmapSource RenderThumbnail(BitmapSource? background)
        {
            double w = Math.Max(1, RootGrid.ActualWidth), h = Math.Max(1, RootGrid.ActualHeight);
            int tw = 400, th = Math.Max(1, (int)(h * tw / w));
            var target = new Rect(0, 0, tw, th);
            var area = new Rect(0, 0, w, h);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (Board != BoardMode.None) dc.DrawRectangle(BoardRect.Fill, null, target);
                else if (background != null) dc.DrawImage(background, target);
                else dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)), null, target);

                foreach (Visual layer in new Visual[] { ShapeCanvas, Ink })
                {
                    var brush = new VisualBrush(layer)
                    {
                        Stretch = Stretch.Fill,
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = area,
                    };
                    dc.DrawRectangle(brush, null, target);
                }
            }

            var bitmap = new RenderTargetBitmap(tw, th, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        // ---------------------------------------------------------------- entrada

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // no zoom, começar a desenhar trava a imagem: ela não anda mais enquanto você mira o próximo traço
            if (IsZoomed) SetPanLocked(true);
            if (Tool == DrawTool.Eraser) EraseElementAt(e.GetPosition(ShapeCanvas));
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsZoomed) return;
            SetPanLocked(!IsPanLocked); // botão direito: solta/trava a imagem para navegar
            e.Handled = true;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Tool == DrawTool.Eraser) EraseElementAt(e.GetPosition(ShapeCanvas));
                return; // enquanto desenha, o zoom não se move
            }
            if (IsZoomed && !IsPanLocked && _currentShape == null)
                UpdatePan(e.GetPosition(RootGrid));
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (IsZoomed && !ctrl)
            {
                ZoomTo(_zoom * (e.Delta > 0 ? 1.25 : 0.8), e.GetPosition(RootGrid));
                return;
            }
            SetPenSize(PenSize + (e.Delta > 0 ? 1 : -1));
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_editingText != null)
            {
                bool ctrlEnter = e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control;
                if (e.Key == Key.Escape || ctrlEnter)
                {
                    FinishText();
                    e.Handled = true;
                }
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = Keyboard.Modifiers;

            if (mods == ModifierKeys.Control)
            {
                switch (key)
                {
                    case Key.Z: Undo(); break;
                    case Key.Y: Redo(); break;
                    case Key.S: DraftSaveRequested?.Invoke(); break;
                    case Key.O: DraftOpenRequested?.Invoke(); break;
                    default: return;
                }
                e.Handled = true;
                return;
            }
            if ((mods & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return;

            switch (key)
            {
                case Key.Escape: CloseOverlay(); break;
                case Key.P: SetTool(DrawTool.Pen); break;
                case Key.H: SetTool(DrawTool.Highlighter); break;
                case Key.X: SetTool(DrawTool.Eraser); break;
                case Key.L: SetTool(DrawTool.Line); break;
                case Key.A: SetTool(DrawTool.Arrow); break;
                case Key.R: SetTool(DrawTool.Rectangle); break;
                case Key.E: SetTool(DrawTool.Ellipse); break;
                case Key.T: SetTool(DrawTool.Text); break;
                case >= Key.D1 and <= Key.D6: SetColor(key - Key.D1); break;
                case >= Key.NumPad1 and <= Key.NumPad6: SetColor(key - Key.NumPad1); break;
                case Key.Add or Key.OemPlus: SetPenSize(PenSize + 1); break;
                case Key.Subtract or Key.OemMinus: SetPenSize(PenSize - 1); break;
                case Key.Delete or Key.Back: ClearAll(); break;
                case Key.W: ToggleBoard(BoardMode.White); break;
                case Key.K: ToggleBoard(BoardMode.Black); break;
                case Key.Z: ToggleZoom(); break;
                case Key.B: ToggleSavedBackground(); break;
                case Key.Space when IsZoomed: SetPanLocked(!IsPanLocked); break;
                case Key.F1: HelpRequested?.Invoke(); break;
                default: return;
            }
            e.Handled = true;
        }

        public void RequestScreenshot() => ScreenshotRequested?.Invoke();
        public void RequestDraftSave() => DraftSaveRequested?.Invoke();
        public void RequestDraftOpen() => DraftOpenRequested?.Invoke();
        public void RequestHelp() => HelpRequested?.Invoke();
    }
}
