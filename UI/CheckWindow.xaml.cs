using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using NavisworksIfcExporter.Core;

namespace NavisworksIfcExporter.UI
{
    public partial class CheckWindow : Window
    {
        private List<CheckRule>   _rules   = new();
        private List<CheckResult> _results = new();

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
                BtnRun.IsEnabled    = _rules.Count > 0;
                BtnExport.IsEnabled = false;
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
        // Run
        // -----------------------------------------------------------------------

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) { SetStatus("Nenhum documento aberto."); return; }
            if (_rules.Count == 0) { SetStatus("Carregue as regras antes de executar."); return; }

            BtnRun.IsEnabled    = false;
            BtnExport.IsEnabled = false;
            GridResults.ItemsSource = null;
            SetStatus("Executando verificação... (aguarde)");

            try
            {
                bool onlyFail = ChkOnlyFailures.IsChecked == true;
                _results = CheckService.RunChecks(doc, _rules, onlyFail,
                    (done, total) => SetStatus($"Verificando... {done}/{total} elementos"));

                GridResults.ItemsSource = _results;

                int ok      = 0, empty = 0, missing = 0;
                foreach (var r in _results)
                {
                    if      (r.Resultado == CheckService.OK)      ok++;
                    else if (r.Resultado == CheckService.EMPTY)   empty++;
                    else if (r.Resultado == CheckService.MISSING) missing++;
                }

                SetStatus(
                    $"{_results.Count} linha(s) gerada(s)  |  " +
                    $"✓ Preenchidas: {ok}  |  ⚠ Vazias: {empty}  |  ✗ Ausentes: {missing}");

                BtnExport.IsEnabled = _results.Count > 0;
            }
            catch (Exception ex)
            {
                SetStatus($"ERRO: {ex.Message}");
                MessageBox.Show(ex.Message, "Erro na verificação",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRun.IsEnabled = true;
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

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void SetStatus(string msg) => TxtStatus.Text = msg;
    }
}
