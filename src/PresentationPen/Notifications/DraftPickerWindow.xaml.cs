using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PresentationPen.Interop;
using PresentationPen.Services;

namespace PresentationPen.Notifications
{
    /// <summary>Item da lista de rascunhos (editável para renomear).</summary>
    public sealed class DraftItem : INotifyPropertyChanged
    {
        private string _path;
        private string _name;
        private bool _isEditing;
        private string _editName = "";

        public DraftItem(DraftInfo info)
        {
            _path = info.Path;
            _name = info.Name;
            Modified = info.Modified;
            Thumbnail = info.Thumbnail;
            HasBackground = info.HasBackground;
        }

        public DateTime Modified { get; }
        public BitmapSource? Thumbnail { get; }
        public bool HasBackground { get; }

        public string Path { get => _path; set => Set(ref _path, value); }
        public string Name { get => _name; set => Set(ref _name, value); }
        public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }
        public string EditName { get => _editName; set => Set(ref _editName, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>Lista os rascunhos salvos (com miniatura) para escolher qual continuar, e permite renomeá-los.</summary>
    public partial class DraftPickerWindow : Window
    {
        /// <summary>Última escolha do checkbox de fundo, lembrada enquanto o app estiver aberto.</summary>
        private static bool s_useBackground = true;

        private readonly string _folder;
        private readonly ObservableCollection<DraftItem> _items;
        private readonly List<(string OldPath, string NewPath)> _renames = new();

        public DraftPickerWindow(string folder, Window? owner, bool hideFromCapture)
        {
            InitializeComponent();
            _folder = folder;
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // a lista de rascunhos não aparece para quem assiste ao compartilhamento
            if (hideFromCapture)
                SourceInitialized += (_, _) =>
                    NativeMethods.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,
                        NativeMethods.WDA_EXCLUDEFROMCAPTURE);

            _items = new ObservableCollection<DraftItem>(DraftStore.List(folder).Select(d => new DraftItem(d)));
            DraftList.ItemsSource = _items;
            EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_items.Count > 0) DraftList.SelectedIndex = 0;
            UseBackgroundCheck.IsChecked = s_useBackground;

            DraftList.SelectionChanged += (_, _) => UpdateButtons();
            DraftList.MouseDoubleClick += (_, _) =>
            {
                if (Selected is { IsEditing: false }) Accept();
            };
            OpenButton.Click += (_, _) => Accept();
            RenameButton.Click += (_, _) => BeginRename();
            RenameMenuItem.Click += (_, _) => BeginRename();
            FolderButton.Click += (_, _) =>
            {
                Directory.CreateDirectory(_folder);
                Process.Start("explorer.exe", $"\"{_folder}\"");
            };
            Loaded += (_, _) =>
            {
                UpdateButtons();
                if (DraftList.SelectedItem != null)
                    (DraftList.ItemContainerGenerator.ContainerFromIndex(0) as IInputElement)?.Focus();
            };
        }

        /// <summary>Rascunho escolhido (após ShowDialog() == true).</summary>
        public string? SelectedPath { get; private set; }

        public bool UseBackground { get; private set; }

        /// <summary>Rascunhos renomeados nesta janela, na ordem em que aconteceu.</summary>
        public IReadOnlyList<(string OldPath, string NewPath)> Renames => _renames;

        private DraftItem? Selected => DraftList.SelectedItem as DraftItem;

        private void UpdateButtons()
        {
            var selected = Selected;
            OpenButton.IsEnabled = selected != null;
            RenameButton.IsEnabled = selected != null;
            UseBackgroundCheck.IsEnabled = selected?.HasBackground == true;
        }

        private void Accept()
        {
            if (Selected is not { } draft) return;
            if (draft.IsEditing && !CommitRename(draft)) return;
            SelectedPath = draft.Path;
            s_useBackground = UseBackgroundCheck.IsChecked == true;
            UseBackground = draft.HasBackground && s_useBackground;
            DialogResult = true; // Enter também chega aqui, pelo IsDefault do botão Abrir
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F2 && Selected is { IsEditing: false })
            {
                BeginRename();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        // ---------------------------------------------------------------- renomear

        private void BeginRename()
        {
            if (Selected is not { } item) return;
            ErrorText.Visibility = Visibility.Collapsed;
            item.EditName = item.Name;
            item.IsEditing = true; // o TextBox recebe o foco em RenameBox_IsVisibleChanged
        }

        private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox { IsVisible: true } box)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    box.Focus();
                    box.SelectAll();
                }, DispatcherPriority.Input);
            }
        }

        private void RenameBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not DraftItem item) return;
            // Enter/Esc aqui não podem chegar aos botões Abrir (IsDefault) e Cancelar (IsCancel)
            if (e.Key == Key.Enter)
            {
                if (CommitRename(item)) FocusItem(item);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                item.IsEditing = false;
                ErrorText.Visibility = Visibility.Collapsed;
                FocusItem(item);
                e.Handled = true;
            }
        }

        private void RenameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DraftItem { IsEditing: true } item)
            {
                // nome inválido ao clicar fora: descarta a edição (o erro fica visível embaixo)
                if (!CommitRename(item)) item.IsEditing = false;
            }
        }

        /// <returns>true se renomeou ou se o nome não mudou.</returns>
        private bool CommitRename(DraftItem item)
        {
            string newName = item.EditName.Trim();
            if (newName.Length == 0 || newName == item.Name)
            {
                item.IsEditing = false;
                return true;
            }
            if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || newName.EndsWith('.'))
                return ShowRenameError("O nome não pode conter \\ / : * ? \" < > | nem terminar com ponto.");

            string folder = System.IO.Path.GetDirectoryName(item.Path)!;
            string newPath = System.IO.Path.Combine(folder, newName + DraftStore.Extension);
            bool onlyCaseChanges = string.Equals(newPath, item.Path, StringComparison.OrdinalIgnoreCase);
            if (!onlyCaseChanges && File.Exists(newPath))
                return ShowRenameError($"Já existe um rascunho chamado \"{newName}\".");

            try
            {
                File.Move(item.Path, newPath);
            }
            catch (Exception ex)
            {
                return ShowRenameError($"Não foi possível renomear: {ex.Message}");
            }

            _renames.Add((item.Path, newPath));
            item.Path = newPath;
            item.Name = newName;
            item.IsEditing = false;
            ErrorText.Visibility = Visibility.Collapsed;
            return true;
        }

        private bool ShowRenameError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            return false;
        }

        private void FocusItem(DraftItem item)
        {
            int index = _items.IndexOf(item);
            Dispatcher.BeginInvoke(() =>
                (DraftList.ItemContainerGenerator.ContainerFromIndex(index) as IInputElement)?.Focus(),
                DispatcherPriority.Input);
        }
    }
}
