using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Autodesk.Navisworks.Api;
using NavisworksIfcExporter.Core;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.UI
{
    public partial class SearchSetExportWindow : Window
    {
        private readonly ObservableCollection<SetTreeNode> _rootNodes = new ObservableCollection<SetTreeNode>();
        private List<MappingRule> _mappingRules = new List<MappingRule>();

        public SearchSetExportWindow()
        {
            InitializeComponent();
            TreeSets.ItemsSource = _rootNodes;
            LoadSets();
        }

        // -----------------------------------------------------------------------
        // Build tree from Navisworks SelectionSets hierarchy
        // -----------------------------------------------------------------------

        private void LoadSets()
        {
            _rootNodes.Clear();
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) { AppendLog("Nenhum documento aberto."); return; }

            try
            {
                foreach (SavedItem item in doc.SelectionSets.Value)
                {
                    var node = BuildNode(item, parent: null);
                    if (node != null) _rootNodes.Add(node);
                }
            }
            catch (Exception ex) { AppendLog($"Erro ao carregar sets: {ex.Message}"); }

            var setCount = CountLeaves(_rootNodes);
            AppendLog($"{setCount} set(s) encontrado(s).");
        }

        private static SetTreeNode? BuildNode(SavedItem item, SetTreeNode? parent)
        {
            if (item is SelectionSet ss)
            {
                return new SetTreeNode
                {
                    Name     = ss.DisplayName,
                    TypeIcon = ss.HasSearch ? "🔍" : "📋",
                    IsFolder = false,
                    Item     = ss,
                    Parent   = parent,
                };
            }

            if (item is GroupItem folder)
            {
                var node = new SetTreeNode
                {
                    Name       = folder.DisplayName,
                    TypeIcon   = "📁",
                    IsFolder   = true,
                    IsExpanded = true,
                    Parent     = parent,
                };
                foreach (SavedItem child in folder.Children)
                {
                    var childNode = BuildNode(child, parent: node);
                    if (childNode != null) node.Children.Add(childNode);
                }
                // Count label: "  (3 sets)"
                var count = CountLeaves(node.Children);
                node.CountLabel = count > 0 ? $"  ({count} set{(count != 1 ? "s" : "")})" : "";
                return node;
            }

            return null;
        }

        private static int CountLeaves(IEnumerable<SetTreeNode> nodes)
            => nodes.Sum(n => n.IsFolder ? CountLeaves(n.Children) : 1);

        // -----------------------------------------------------------------------
        // UI events
        // -----------------------------------------------------------------------

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtLog.Clear();
            LoadSets();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var anyUnchecked = HasUnchecked(_rootNodes);
            foreach (var node in _rootNodes)
                node.IsChecked = anyUnchecked; // all → true if any unchecked, else false
        }

        private static bool HasUnchecked(IEnumerable<SetTreeNode> nodes)
            => nodes.Any(n => n.IsChecked != true || (n.IsFolder && HasUnchecked(n.Children)));

        private void TxtFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var filter = TxtFilter.Text.Trim();
            if (string.IsNullOrEmpty(filter))
            {
                TreeSets.ItemsSource = _rootNodes;
                return;
            }

            // Flat list of matching leaf nodes (preserving original objects & checked state)
            var matches = CollectLeaves(_rootNodes)
                .Where(n => n.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            TreeSets.ItemsSource = matches;
        }

        private static IEnumerable<SetTreeNode> CollectLeaves(IEnumerable<SetTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!node.IsFolder) yield return node;
                else foreach (var leaf in CollectLeaves(node.Children)) yield return leaf;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title      = "Salvar arquivo IFC",
                Filter     = "IFC Files (*.ifc)|*.ifc",
                DefaultExt = ".ifc",
            };
            if (dialog.ShowDialog() == true) TxtOutputPath.Text = dialog.FileName;
        }

        private void BtnMapping_Click(object sender, RoutedEventArgs e)
        {
            var win = new MappingWindow(_mappingRules) { Owner = this };
            if (win.ShowDialog() == true) _mappingRules = win.GetRules();
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // Collect all checked leaf nodes
            var checkedSets = CollectLeaves(_rootNodes)
                .Where(n => n.IsChecked == true && n.Item != null)
                .Select(n => n.Item!)
                .ToList();

            if (checkedSets.Count == 0)
            {
                MessageBox.Show("Nenhum set selecionado.\nMarque ao menos um set na lista.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtOutputPath.Text))
            {
                MessageBox.Show("Selecione o caminho do arquivo de saída.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnExport.IsEnabled = false;
            TxtLog.Clear();

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument!;
            AppendLog($"Resolvendo {checkedSets.Count} set(s)...");

            // Resolve all sets on the UI/STA thread before going async
            List<ModelItem> items;
            try
            {
                var seen = new HashSet<Guid>();
                var merged = new List<ModelItem>();
                foreach (var set in checkedSets)
                {
                    foreach (var item in set.GetSelectedItems(doc))
                    {
                        if (seen.Add(item.InstanceGuid))
                            merged.Add(item);
                    }
                }
                items = merged;
                AppendLog($"{items.Count} elemento(s) único(s) encontrado(s).");
            }
            catch (Exception ex)
            {
                AppendLog($"ERRO ao resolver sets: {ex.Message}");
                BtnExport.IsEnabled = true;
                return;
            }

            if (items.Count == 0)
            {
                MessageBox.Show("Os sets selecionados não retornaram nenhum elemento.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnExport.IsEnabled = true;
                return;
            }

            var options = new ExportOptions
            {
                OutputPath       = TxtOutputPath.Text,
                ExportGeometry   = ChkGeometry.IsChecked == true,
                IncludeHidden    = ChkHidden.IsChecked == true,
                AuthorName       = TxtAuthor.Text,
                OrganizationName = TxtOrganization.Text,
                MappingRules     = _mappingRules,
                ExplicitItems    = items,
            };

            try
            {
                await Task.Run(() =>
                {
                    var service = new ExportService();
                    service.ProgressChanged += (_, msg) => Dispatcher.Invoke(() => AppendLog(msg));
                    service.Export(options);
                });
                MessageBox.Show("Exportação concluída com sucesso!", "Sucesso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"ERRO: {ex.Message}");
                MessageBox.Show($"Erro durante a exportação:\n{ex.Message}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnExport.IsEnabled = true; }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void AppendLog(string message)
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            TxtLog.ScrollToEnd();
        }
    }

    // -----------------------------------------------------------------------
    // Tree node view-model with three-state checkbox propagation
    // -----------------------------------------------------------------------

    public class SetTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string       Name       { get; set; } = "";
        public string       TypeIcon   { get; set; } = "";
        public string       CountLabel { get; set; } = "";
        public bool         IsFolder   { get; set; }
        public SelectionSet? Item      { get; set; }
        public SetTreeNode? Parent     { get; set; }
        public bool         IsExpanded { get; set; } = true;

        public ObservableCollection<SetTreeNode> Children { get; } = new ObservableCollection<SetTreeNode>();

        private bool? _isChecked = false;

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, toChildren: true, toParent: true);
        }

        internal void SetIsChecked(bool? value, bool toChildren, bool toParent)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));

            if (toChildren && value.HasValue)
                foreach (var child in Children)
                    child.SetIsChecked(value, toChildren: true, toParent: false);

            if (toParent)
                Parent?.RecalcFromChildren();
        }

        private void RecalcFromChildren()
        {
            if (Children.Count == 0) return;

            bool allChecked   = Children.All(c => c._isChecked == true);
            bool allUnchecked = Children.All(c => c._isChecked == false);

            bool? newState = allChecked ? true : allUnchecked ? false : (bool?)null;
            SetIsChecked(newState, toChildren: false, toParent: true);
        }
    }
}
