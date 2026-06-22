using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Windows;
using NavisworksIfcExporter.UI;

namespace NavisworksIfcExporter
{
    // EventWatcherPlugin: loaded automatically at startup; OnLoaded() runs on the UI thread.
    // No secondary attribute is needed — Navisworks detects the plugin type from the base class.
    [Plugin("NavisworksIfcExporter.Ribbon", "PHD")]
    public class RibbonLoader : EventWatcherPlugin
    {
        private const string TabId   = "PHD_COORDINATION_TAB";
        private const string PanelId = "PHD_IFC_PANEL";

        public override void OnLoaded()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            // Guard: Navisworks sometimes reloads plugins without restarting
            if (ribbon.FindTab(TabId) != null) return;

            var tab = new RibbonTab
            {
                Title     = "PHD Coordination",
                Id        = TabId,
                IsVisible = true,
            };

            var panelSource = new RibbonPanelSource
            {
                Title = "IFC Export",
                Id    = PanelId,
            };

            panelSource.Items.Add(new RibbonButton
            {
                Text             = "Exportar IFC 4",
                Id               = "PHD_EXPORT_IFC_BTN",
                ShowText         = true,
                Size             = RibbonItemSize.Large,
                Orientation      = System.Windows.Controls.Orientation.Vertical,
                LargeImage       = CreateIcon(32),
                Image            = CreateIcon(16),
                ToolTip          = new RibbonToolTip
                {
                    Title   = "Exportar IFC 4",
                    Content = "Exporta o modelo ativo para IFC 4 com geometria tessellada e propriedades completas.",
                    IsHelpEnabled = false,
                },
                CommandHandler   = new ExportIfcCommand(),
                IsEnabled        = true,
            });

            tab.Panels.Add(new RibbonPanel { Source = panelSource });
            ribbon.Tabs.Add(tab);
        }

        public override void OnUnloading() { }

        // Cria um ícone IFC azul renderizado via WPF (sem depender de arquivo externo).
        private static ImageSource CreateIcon(int size)
        {
            try
            {
                var dv = new DrawingVisual();
                using (var ctx = dv.RenderOpen())
                {
                    // Fundo azul arredondado
                    ctx.DrawRoundedRectangle(
                        new SolidColorBrush(Color.FromRgb(0, 114, 188)),
                        null,
                        new Rect(1, 1, size - 2, size - 2),
                        3, 3);

                    // Texto "IFC"
                    double fontSize = size >= 32 ? 11.0 : 5.5;
                    var ft = new FormattedText(
                        "IFC",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Arial"), FontStyles.Normal,
                                     FontWeights.Bold, FontStretches.Normal),
                        fontSize,
                        Brushes.White,
                        1.0); // PixelsPerDip = 1.0 (DPI-aware é feito pelo Navisworks host)

                    ctx.DrawText(ft, new Point(
                        Math.Round((size - ft.Width)  / 2),
                        Math.Round((size - ft.Height) / 2)));
                }

                var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                return rtb;
            }
            catch { return null!; }
        }
    }

    // ICommand que abre o ExportWindow — chamado pelo botão do ribbon.
    internal sealed class ExportIfcCommand : ICommand
    {
        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            new ExportWindow().ShowDialog();
        }

        // Navisworks ribbon não usa CanExecuteChanged; implementação vazia é suficiente.
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
