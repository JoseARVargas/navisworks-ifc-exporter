using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.Navisworks.Api;
using NwColor = Autodesk.Navisworks.Api.Color;

namespace NavisworksIfcExporter.UI
{
    public partial class HighlightSelectionWindow : Window
    {
        private System.Windows.Media.Color _pickedColor = System.Windows.Media.Color.FromRgb(180, 180, 180);
        private bool _overrideActive;

        public HighlightSelectionWindow()
        {
            InitializeComponent();
            UpdateSwatch();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => RefreshStatus();

        private void RefreshStatus()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) { TxtStatus.Text = "Nenhum documento aberto."; return; }

            int selected   = doc.CurrentSelection.SelectedItems.Count;
            int totalGeom  = WalkGeometry(doc.Models.RootItems).Count();

            TxtStatus.Text = selected > 0
                ? $"Seleção atual: {selected} elemento(s)  |  Não selecionados: {totalGeom - selected} elemento(s)"
                : "Nenhum elemento selecionado. Faça uma seleção no Navisworks e clique em Aplicar.";

            BtnApply.IsEnabled = selected > 0;
        }

        // -----------------------------------------------------------------------
        // Color picker
        // -----------------------------------------------------------------------

        private void ColorSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OpenColorDialog();

        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
            => OpenColorDialog();

        private void OpenColorDialog()
        {
            var dlg = new System.Windows.Forms.ColorDialog
            {
                Color          = System.Drawing.Color.FromArgb(_pickedColor.R, _pickedColor.G, _pickedColor.B),
                FullOpen       = true,
                AllowFullOpen  = true,
            };

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var owner  = System.Windows.Forms.Control.FromHandle(helper.Handle)
                         ?? new System.Windows.Forms.NativeWindow().AsControl(helper.Handle);

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = dlg.Color;
                _pickedColor = System.Windows.Media.Color.FromRgb(c.R, c.G, c.B);
                UpdateSwatch();
                if (ChkLivePreview.IsChecked == true && _overrideActive) ApplyOverride();
            }
        }

        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            var parts = (btn.Tag as string)?.Split(',');
            if (parts?.Length != 3) return;
            _pickedColor = System.Windows.Media.Color.FromRgb(
                byte.Parse(parts[0].Trim()),
                byte.Parse(parts[1].Trim()),
                byte.Parse(parts[2].Trim()));
            UpdateSwatch();
            if (ChkLivePreview.IsChecked == true && _overrideActive) ApplyOverride();
        }

        private void UpdateSwatch()
        {
            ColorSwatch.Background = new SolidColorBrush(_pickedColor);
        }

        // -----------------------------------------------------------------------
        // Transparency slider
        // -----------------------------------------------------------------------

        private void SliderTransp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtTranspPct == null) return;
            TxtTranspPct.Text = $"{(int)SliderTransp.Value}%";
            if (ChkLivePreview?.IsChecked == true && _overrideActive) ApplyOverride();
        }

        private void ChkLivePreview_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkLivePreview.IsChecked == true && _overrideActive) ApplyOverride();
        }

        // -----------------------------------------------------------------------
        // Apply / Restore
        // -----------------------------------------------------------------------

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) return;

            int selected = doc.CurrentSelection.SelectedItems.Count;
            if (selected == 0)
            {
                MessageBox.Show(
                    "Faça uma seleção no Navisworks antes de aplicar o realce.",
                    "Sem seleção", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplyOverride();
        }

        private void ApplyOverride()
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) return;

            // Always reset first to clear any previous override
            doc.Models.ResetAllTemporaryMaterials();

            var selectedGuids = new HashSet<Guid>(
                doc.CurrentSelection.SelectedItems.Select(i => i.InstanceGuid));

            var unselected = WalkGeometry(doc.Models.RootItems)
                .Where(i => !selectedGuids.Contains(i.InstanceGuid))
                .ToList();

            if (unselected.Count == 0) return;

            double r = _pickedColor.R / 255.0;
            double g = _pickedColor.G / 255.0;
            double b = _pickedColor.B / 255.0;
            var nwColor = new NwColor(r, g, b);

            double transparency = SliderTransp.Value / 100.0;

            doc.Models.OverrideTemporaryColor(unselected, nwColor);
            doc.Models.OverrideTemporaryTransparency(unselected, transparency);

            _overrideActive     = true;
            BtnRestore.IsEnabled = true;
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            doc?.Models.ResetAllTemporaryMaterials();
            _overrideActive      = false;
            BtnRestore.IsEnabled = false;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_overrideActive)
                Autodesk.Navisworks.Api.Application.ActiveDocument
                    ?.Models.ResetAllTemporaryMaterials();
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static IEnumerable<ModelItem> WalkGeometry(IEnumerable<ModelItem> items)
        {
            foreach (var item in items)
            {
                if (item.HasGeometry) yield return item;
                foreach (var child in WalkGeometry(item.Children)) yield return child;
            }
        }
    }

    // WinForms helper: wrap a native handle as a Form owner for dialogs
    internal static class NativeWindowHelper
    {
        internal static System.Windows.Forms.Control AsControl(
            this System.Windows.Forms.NativeWindow nw, IntPtr handle)
        {
            nw.AssignHandle(handle);
            return new OwnerControl(handle);
        }

        private sealed class OwnerControl : System.Windows.Forms.Control
        {
            internal OwnerControl(IntPtr handle) { }
            protected override void WndProc(ref System.Windows.Forms.Message m)
                => base.WndProc(ref m);
        }
    }
}
