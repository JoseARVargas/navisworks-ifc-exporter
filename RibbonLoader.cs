using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Windows;
using NavisworksIfcExporter.UI;

namespace NavisworksIfcExporter
{
    // EventWatcherPlugin: discovered automatically by Navisworks from the DLL.
    // OnLoaded() runs as soon as the assembly is loaded; the ribbon may not be
    // ready yet, so we defer tab creation to Application.GuiCreated.
    [Plugin("NavisworksIfcExporter.Ribbon", "PHD",
        DisplayName = "PHD Coordination Ribbon")]
    public class RibbonLoader : EventWatcherPlugin
    {
        private const string TabId   = "PHD_COORDINATION_TAB";
        private const string PanelId = "PHD_IFC_PANEL";

        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "NavisworksIfcExporter_ribbon.log");

        public override void OnLoaded()
        {
            Log("OnLoaded() called");

            // Try immediately — succeeds if ribbon is already available.
            if (TryBuildRibbon())
            {
                Log("Ribbon built immediately in OnLoaded()");
                return;
            }

            // Ribbon not ready yet: defer to Application.GuiCreated.
            Log("Ribbon null in OnLoaded() — deferring to GuiCreated");
            Autodesk.Navisworks.Api.Application.GuiCreated += OnGuiCreated;
        }

        private void OnGuiCreated(object sender, EventArgs e)
        {
            Autodesk.Navisworks.Api.Application.GuiCreated -= OnGuiCreated;
            Log("GuiCreated fired");
            if (TryBuildRibbon())
                Log("Ribbon built from GuiCreated");
            else
                Log("ERROR: Ribbon still null after GuiCreated");
        }

        private bool TryBuildRibbon()
        {
            try
            {
                var ribbon = ComponentManager.Ribbon;
                if (ribbon == null)
                {
                    Log("ComponentManager.Ribbon is null");
                    return false;
                }

                // Guard: avoid duplicate tabs if the plugin assembly is reloaded.
                if (ribbon.FindTab(TabId) != null)
                {
                    Log("Tab already exists — skipping");
                    return true;
                }

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
                    Text           = "Exportar IFC 4",
                    Id             = "PHD_EXPORT_IFC_BTN",
                    ShowText       = true,
                    Size           = RibbonItemSize.Large,
                    Orientation    = System.Windows.Controls.Orientation.Vertical,
                    LargeImage     = CreateIcon(32),
                    Image          = CreateIcon(16),
                    ToolTip        = new RibbonToolTip
                    {
                        Title         = "Exportar IFC 4",
                        Content       = "Exporta o modelo ativo para IFC 4 com geometria tessellada e propriedades completas.",
                        IsHelpEnabled = false,
                    },
                    CommandHandler = new ExportIfcCommand(),
                    IsEnabled      = true,
                });

                tab.Panels.Add(new RibbonPanel { Source = panelSource });
                ribbon.Tabs.Add(tab);
                Log($"Tab added: {tab.Title} (id={TabId})");
                return true;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION in TryBuildRibbon: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public override void OnUnloading() { }

        // Simple rotating log for ribbon diagnostics — check %TEMP%\NavisworksIfcExporter_ribbon.log
        private static void Log(string message)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); }
            catch { }
        }

        private static ImageSource CreateIcon(int size)
        {
            try
            {
                var dv = new DrawingVisual();
                using (var ctx = dv.RenderOpen())
                {
                    ctx.DrawRoundedRectangle(
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 114, 188)),
                        null,
                        new Rect(1, 1, size - 2, size - 2),
                        3, 3);

                    double fontSize = size >= 32 ? 11.0 : 5.5;
                    var ft = new FormattedText(
                        "IFC",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Arial"),
                                     FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        fontSize,
                        Brushes.White,
                        1.0);

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

    internal sealed class ExportIfcCommand : ICommand
    {
        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => new ExportWindow().ShowDialog();

        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
