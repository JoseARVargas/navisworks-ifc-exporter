using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NavisworksIfcExporter.Core;

namespace NavisworksIfcExporter.UI
{
    public partial class CheckWindow : Window
    {
        private List<CheckRule>   _rules   = new();
        private List<CheckResult> _results = new();
        private bool _processing;

        public CheckWindow()
        {
            InitializeComponent();
            TxtOutputDir.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PHD_Check_Resultados");
        }

        // -----------------------------------------------------------------------
        // Browse + Load rules
        // -----------------------------------------------------------------------

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Selecionar arquivo de regras",
                Filter = "Planilhas e CSV|*.csv;*.xlsx;*.xls;*.xlsm|CSV|*.csv|Excel|*.xlsx;*.xls;*.xlsm",
            };
            if (dlg.ShowDialog() != true) return;
            TxtFilePath.Text  = dlg.FileName;
            BtnLoad.IsEnabled = true;
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _rules = CheckService.LoadRules(TxtFilePath.Text);
                _results.Clear();
                GridRules.ItemsSource   = _rules;
                GridResults.ItemsSource = null;
                BtnRun.IsEnabled        = _rules.Count > 0;
                BtnExport.IsEnabled     = false;
                SetStatus($"{_rules.Count} regra(s) carregada(s). Clique em \"Executar Verificação\".");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar arquivo:\n{ex.Message}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -----------------------------------------------------------------------
        // Browse output dir
        // -----------------------------------------------------------------------

        private void BtnOutputDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Selecione a pasta de saída dos resultados",
                SelectedPath        = TxtOutputDir.Text,
                ShowNewFolderButton = true,
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TxtOutputDir.Text = dlg.SelectedPath;
        }

        // -----------------------------------------------------------------------
        // Run (async — usa Task.Yield() para liberar a UI a cada 100 elementos)
        // -----------------------------------------------------------------------

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) { SetStatus("Nenhum documento aberto."); return; }
            if (_rules.Count == 0) { SetStatus("Carregue as regras antes de executar."); return; }

            _processing         = true;
            BtnRun.IsEnabled    = false;
            BtnExport.IsEnabled = false;
            GridResults.ItemsSource = null;

            SetProgress(true, 0);
            SetStatus("Coletando elementos do modelo...");
            await Dispatcher.Yield(DispatcherPriority.Background);

            try
            {
                bool onlyFail = ChkOnlyFailures.IsChecked == true;
                var results = new List<CheckResult>();
                int ok = 0, empty = 0, missing = 0;

                // Collect all geometry items with their source files (from doc.Models)
                var allItems = CheckService.GetGeometryItems(doc);
                int total = allItems.Count;
                SetStatus($"Verificando {total} elemento(s)...");
                await Dispatcher.Yield(DispatcherPriority.Background);

                for (int i = 0; i < total; i++)
                {
                    var (item, src) = allItems[i];
                    CheckService.ProcessItem(item, src, _rules, results, onlyFail);

                    // Yield to UI every 100 items → atualiza progresso e renderiza
                    if (i % 100 == 0)
                    {
                        double pct = total > 0 ? (double)i / total * 100.0 : 0;
                        SetProgress(true, pct);
                        SetStatus($"Verificando... {i}/{total} elementos");
                        await Dispatcher.Yield(DispatcherPriority.Background);
                    }
                }

                _results = results;
                GridResults.ItemsSource = results;

                foreach (var r in results)
                {
                    if      (r.Resultado == CheckService.OK)      ok++;
                    else if (r.Resultado == CheckService.EMPTY)   empty++;
                    else if (r.Resultado == CheckService.MISSING) missing++;
                }

                string summary = results.Count == 0
                    ? $"Nenhum resultado. {total} elemento(s) percorrido(s) — verifique se a coluna Disciplina corresponde ao Source File do modelo."
                    : $"{results.Count} linha(s)  |  ✓ {ok} Preenchidas  |  ⚠ {empty} Vazias  |  ✗ {missing} Ausentes";

                SetStatus(summary);
                BtnExport.IsEnabled = results.Count > 0;
            }
            catch (Exception ex)
            {
                SetStatus($"ERRO: {ex.Message}");
                MessageBox.Show(ex.Message, "Erro na verificação",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetProgress(false, 100);
                BtnRun.IsEnabled = true;
                _processing      = false;
            }
        }

        // -----------------------------------------------------------------------
        // Export CSV
        // -----------------------------------------------------------------------

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0) return;
            try
            {
                string path = CheckService.ExportCsv(_results, TxtOutputDir.Text);
                SetStatus($"CSV exportado: {path}");
                if (MessageBox.Show($"Arquivo exportado:\n{path}\n\nAbrir pasta?",
                        "Exportação concluída", MessageBoxButton.YesNo,
                        MessageBoxImage.Information) == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar:\n{ex.Message}", "Erro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_processing)
            {
                MessageBox.Show("Aguarde o término da verificação antes de fechar.",
                    "Em processamento", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Close();
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void SetStatus(string msg) => TxtStatus.Text = msg;

        private void SetProgress(bool visible, double value)
        {
            PanelProgress.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            PrgBar.Value  = value;
            TxtPct.Text   = $"{(int)value}%";
        }
    }
}
