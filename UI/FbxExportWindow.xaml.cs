using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Navisworks.Api;

namespace NavisworksIfcExporter.UI
{
    public partial class FbxExportWindow : Window
    {
        private readonly ObservableCollection<SetTreeNode> _rootNodes = new();
        private bool _isolated;

        public FbxExportWindow()
        {
            InitializeComponent();
            TreeSets.ItemsSource = _rootNodes;
            LoadSets();
        }

        // -----------------------------------------------------------------------
        // Build tree
        // -----------------------------------------------------------------------

        private void LoadSets()
        {
            _rootNodes.Clear();
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) return;

            try
            {
                foreach (SavedItem item in doc.SelectionSets.Value)
                {
                    var node = BuildNode(item, parent: null);
                    if (node != null) _rootNodes.Add(node);
                }
            }
            catch { }
        }

        private static SetTreeNode? BuildNode(SavedItem item, SetTreeNode? parent)
        {
            if (item is SelectionSet ss)
            {
                return new SetTreeNode {
                    Name = ss.DisplayName, TypeIcon = ss.HasSearch ? "🔍" : "📋",
                    IsFolder = false, Item = ss, Parent = parent,
                };
            }
            if (item is GroupItem folder)
            {
                var node = new SetTreeNode {
                    Name = folder.DisplayName, TypeIcon = "📁",
                    IsFolder = true, IsExpanded = true, Parent = parent,
                };
                foreach (SavedItem child in folder.Children)
                {
                    var childNode = BuildNode(child, parent: node);
                    if (childNode != null) node.Children.Add(childNode);
                }
                var count = CountLeaves(node.Children);
                node.CountLabel = count > 0 ? $"  ({count} set{(count != 1 ? "s" : "")})" : "";
                return node;
            }
            return null;
        }

        private static int CountLeaves(IEnumerable<SetTreeNode> nodes)
            => nodes.Sum(n => n.IsFolder ? CountLeaves(n.Children) : 1);

        private static IEnumerable<SetTreeNode> CollectLeaves(IEnumerable<SetTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!node.IsFolder) yield return node;
                else foreach (var leaf in CollectLeaves(node.Children)) yield return leaf;
            }
        }

        // -----------------------------------------------------------------------
        // Buttons
        // -----------------------------------------------------------------------

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool anyUnchecked = CollectLeaves(_rootNodes).Any(n => n.IsChecked != true);
            foreach (var node in _rootNodes) node.IsChecked = anyUnchecked;
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in _rootNodes) node.IsChecked = false;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadSets();

        private void BtnIsolate_Click(object sender, RoutedEventArgs e)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) { MessageBox.Show("Nenhum documento aberto.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var checkedSets = CollectLeaves(_rootNodes)
                .Where(n => n.IsChecked == true && n.Item != null)
                .Select(n => n.Item!)
                .ToList();

            if (checkedSets.Count == 0)
            {
                MessageBox.Show("Selecione ao menos um set.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Resolve set items on STA thread
            var seen = new HashSet<Guid>();
            var setItems = new List<ModelItem>();
            foreach (var set in checkedSets)
                foreach (var item in set.GetSelectedItems(doc))
                    if (seen.Add(item.InstanceGuid)) setItems.Add(item);

            if (setItems.Count == 0)
            {
                MessageBox.Show("Os sets selecionados não contêm elementos.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Collect all model items (BFS) then hide those not in set
            var setSet = new HashSet<ModelItem>(setItems);
            var allItems = WalkAll(doc.Models.RootItems).ToList();
            var toHide   = allItems.Where(i => !setSet.Contains(i)).ToList();

            try
            {
                if (_isolated) doc.Models.ResetAllHidden();
                doc.Models.SetHidden(toHide, true);
                _isolated = true;

                BtnIsolate.IsEnabled = false;
                BtnRestore.IsEnabled = true;

                MessageBox.Show(
                    $"{setItems.Count} elemento(s) isolado(s) na viewport.\n\n" +
                    "Para exportar como FBX:\n" +
                    "  Arquivo  →  Exportar  →  FBX...\n\n" +
                    "Depois clique em \"Restaurar visibilidade\".",
                    "Elementos isolados", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao isolar elementos:\n{ex.Message}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                doc?.Models.ResetAllHidden();
                _isolated = false;
                BtnIsolate.IsEnabled = true;
                BtnRestore.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao restaurar visibilidade:\n{ex.Message}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Restore visibility if we exit with items isolated
            if (_isolated)
            {
                var result = MessageBox.Show(
                    "A visibilidade dos elementos ainda está alterada.\nDeseja restaurar antes de fechar?",
                    "Restaurar visibilidade?", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    Autodesk.Navisworks.Api.Application.ActiveDocument?.Models.ResetAllHidden();
            }
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // prevent OnClosing from triggering after BtnClose already handled it
            base.OnClosing(e);
        }

        // -----------------------------------------------------------------------
        // BFS walk
        // -----------------------------------------------------------------------

        private static IEnumerable<ModelItem> WalkAll(IEnumerable<ModelItem> items)
        {
            foreach (var item in items)
            {
                yield return item;
                foreach (var child in WalkAll(item.Children))
                    yield return child;
            }
        }
    }
}
