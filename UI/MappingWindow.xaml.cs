using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NavisworksIfcExporter.Core;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.UI
{
    public partial class MappingWindow : Window
    {
        private ObservableCollection<MappingRule> _rules;

        private static readonly string DefaultSavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Navisworks 2026", "Plugins", "NavisworksIfcExporter", "mapping.json");

        public MappingWindow(IEnumerable<MappingRule>? initialRules = null)
        {
            InitializeComponent();

            // Use provided rules if any, otherwise try to load from persisted file
            if (initialRules != null)
                _rules = new ObservableCollection<MappingRule>(initialRules);
            else
                _rules = new ObservableCollection<MappingRule>(LoadFromFile(DefaultSavePath));

            GridRules.ItemsSource = _rules;
        }

        // Returns current rules as a list (caller should store this after DialogResult = true)
        public List<MappingRule> GetRules() => _rules.ToList();

        // -----------------------------------------------------------------------
        // Grid actions
        // -----------------------------------------------------------------------

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var rule = new MappingRule();
            _rules.Add(rule);
            GridRules.ScrollIntoView(rule);
            GridRules.SelectedItem = rule;
            GridRules.CurrentItem = rule;
            // Begin editing the first cell
            GridRules.BeginEdit();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var selected = GridRules.SelectedItems.Cast<MappingRule>().ToList();
            foreach (var r in selected)
                _rules.Remove(r);
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            var rule = GridRules.SelectedItem as MappingRule;
            if (rule == null) return;
            int idx = _rules.IndexOf(rule);
            if (idx > 0) _rules.Move(idx, idx - 1);
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            var rule = GridRules.SelectedItem as MappingRule;
            if (rule == null) return;
            int idx = _rules.IndexOf(rule);
            if (idx < _rules.Count - 1) _rules.Move(idx, idx + 1);
        }

        // -----------------------------------------------------------------------
        // Import / Export JSON
        // -----------------------------------------------------------------------

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importar mapeamento",
                Filter = "JSON (*.json)|*.json|Todos (*.*)|*.*",
                DefaultExt = ".json",
            };
            if (dlg.ShowDialog() != true) return;

            var loaded = LoadFromFile(dlg.FileName);
            _rules.Clear();
            foreach (var r in loaded) _rules.Add(r);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Exportar mapeamento",
                Filter = "JSON (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "mapping.json",
            };
            if (dlg.ShowDialog() != true) return;
            SaveToFile(_rules, dlg.FileName);
        }

        // -----------------------------------------------------------------------
        // ComboBox with IFC Pset catalog
        // -----------------------------------------------------------------------

        private void CmbTargetPset_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox cmb && cmb.ItemsSource == null)
                cmb.ItemsSource = PsetCatalog.PsetNames();
        }

        // -----------------------------------------------------------------------
        // OK / Cancel
        // -----------------------------------------------------------------------

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            GridRules.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            SaveToFile(_rules, DefaultSavePath);
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // -----------------------------------------------------------------------
        // Serialization helpers (DataContractJsonSerializer — no NuGet needed)
        // -----------------------------------------------------------------------

        private static List<MappingRule> LoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return new List<MappingRule>();
                var ser = new DataContractJsonSerializer(typeof(List<MappingRule>));
                using var fs = File.OpenRead(path);
                return (List<MappingRule>)ser.ReadObject(fs) ?? new List<MappingRule>();
            }
            catch { return new List<MappingRule>(); }
        }

        private static void SaveToFile(IEnumerable<MappingRule> rules, string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var ser = new DataContractJsonSerializer(typeof(List<MappingRule>));
                using var fs = File.Create(path);
                ser.WriteObject(fs, rules.ToList());
            }
            catch { /* non-critical — mapping still applied in memory */ }
        }
    }
}
