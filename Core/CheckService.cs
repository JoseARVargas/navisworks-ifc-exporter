using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using ExcelDataReader;

namespace NavisworksIfcExporter.Core
{
    public class CheckRule
    {
        public string Disciplina  { get; set; } = "";
        public string Categoria   { get; set; } = "";
        public string Propriedade { get; set; } = "";
    }

    public class CheckResult
    {
        public string Disciplina     { get; set; } = "";
        public string SourceFile     { get; set; } = "";
        public string Guid           { get; set; } = "";
        public string Categoria      { get; set; } = "";
        public string Propriedade    { get; set; } = "";
        public string Valor          { get; set; } = "";
        public string Resultado      { get; set; } = "";
    }

    public static class CheckService
    {
        public const string OK      = "✓ Preenchida";
        public const string EMPTY   = "⚠ Vazia";
        public const string MISSING = "✗ Ausente";

        // -----------------------------------------------------------------------
        // Load rules from file
        // -----------------------------------------------------------------------

        public static List<CheckRule> LoadRules(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".xlsx" or ".xls" or ".xlsm" => LoadRulesFromExcel(path),
                _                             => LoadRulesFromCsv(path),
            };
        }

        private static List<CheckRule> LoadRulesFromCsv(string path)
        {
            var rules = new List<CheckRule>();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2) return rules;

            char sep = lines[0].Contains(';') ? ';' : ',';
            string[] headers = SplitLine(lines[0], sep);

            int iDisc  = FindHeader(headers, "disciplina");
            int iCat   = FindHeader(headers, "categoria");
            int iProp  = FindHeader(headers, "propriedade", "property");

            if (iDisc < 0 || iCat < 0 || iProp < 0)
                throw new InvalidDataException(
                    "CSV deve ter colunas: Disciplina, Categoria, Propriedade");

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = SplitLine(lines[i], sep);
                if (cols.Length <= Math.Max(iDisc, Math.Max(iCat, iProp))) continue;
                rules.Add(new CheckRule {
                    Disciplina  = cols[iDisc].Trim(),
                    Categoria   = cols[iCat].Trim(),
                    Propriedade = cols[iProp].Trim(),
                });
            }
            return rules;
        }

        private static List<CheckRule> LoadRulesFromExcel(string path)
        {
            var rules = new List<CheckRule>();
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);
            var ds = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataReader.ExcelDataTableConfiguration
                    { UseHeaderRow = true }
            });

            if (ds.Tables.Count == 0) return rules;
            var table = ds.Tables[0];

            // Find columns by name
            int iDisc  = FindColumn(table, "disciplina");
            int iCat   = FindColumn(table, "categoria");
            int iProp  = FindColumn(table, "propriedade", "property");

            if (iDisc < 0 || iCat < 0 || iProp < 0)
                throw new InvalidDataException(
                    "Excel deve ter colunas: Disciplina, Categoria, Propriedade");

            foreach (DataRow row in table.Rows)
            {
                string disc = row[iDisc]?.ToString()?.Trim() ?? "";
                string cat  = row[iCat]?.ToString()?.Trim()  ?? "";
                string prop = row[iProp]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(cat) && string.IsNullOrWhiteSpace(prop)) continue;
                rules.Add(new CheckRule { Disciplina = disc, Categoria = cat, Propriedade = prop });
            }
            return rules;
        }

        // -----------------------------------------------------------------------
        // Run property checks
        // -----------------------------------------------------------------------

        public static List<CheckResult> RunChecks(
            Document doc, IList<CheckRule> rules,
            bool onlyFailures = false,
            Action<int, int>? progress = null)
        {
            var results = new List<CheckResult>();
            var allItems = WalkGeometry(doc.Models.RootItems).ToList();
            int total = allItems.Count, done = 0;

            foreach (var item in allItems)
            {
                if (progress != null && done % 500 == 0) progress(done, total);
                done++;

                string sourceFile = GetSourceFile(item);

                foreach (var rule in rules)
                {
                    // Match Disciplina: if specified, source file must contain it
                    if (!string.IsNullOrWhiteSpace(rule.Disciplina) &&
                        sourceFile.IndexOf(rule.Disciplina, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var (resultado, valor) = CheckProperty(item, rule.Categoria, rule.Propriedade);

                    if (onlyFailures && resultado == OK) continue;

                    results.Add(new CheckResult {
                        Disciplina  = rule.Disciplina,
                        SourceFile  = Path.GetFileName(sourceFile),
                        Guid        = item.InstanceGuid.ToString(),
                        Categoria   = rule.Categoria,
                        Propriedade = rule.Propriedade,
                        Valor       = valor,
                        Resultado   = resultado,
                    });
                }
            }

            return results;
        }

        // -----------------------------------------------------------------------
        // Export results to CSV
        // -----------------------------------------------------------------------

        public static string ExportCsv(IList<CheckResult> results, string outputDir)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path  = Path.Combine(outputDir, $"check_result_{stamp}.csv");
            Directory.CreateDirectory(outputDir);

            using var w = new StreamWriter(path, false, new UTF8Encoding(true));
            w.WriteLine("Disciplina;Source File;GUID;Categoria;Propriedade;Valor;Resultado");
            foreach (var r in results)
            {
                w.WriteLine(string.Join(";", new[]
                {
                    Esc(r.Disciplina), Esc(r.SourceFile), Esc(r.Guid),
                    Esc(r.Categoria),  Esc(r.Propriedade), Esc(r.Valor),
                    Esc(r.Resultado),
                }));
            }
            return path;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static (string resultado, string valor) CheckProperty(
            ModelItem item, string category, string propName)
        {
            foreach (var cat in item.PropertyCategories)
            {
                if (!cat.DisplayName.Equals(category, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var prop in cat.Properties)
                {
                    if (!prop.DisplayName.Equals(propName, StringComparison.OrdinalIgnoreCase)) continue;
                    string val = prop.Value?.ToDisplayString() ?? "";
                    return (string.IsNullOrWhiteSpace(val) ? EMPTY : OK, val);
                }
                // Category found but property not in it → MISSING
                return (MISSING, "");
            }

            return (MISSING, "");
        }

        private static string GetSourceFile(ModelItem item)
        {
            try { return item.HasModel ? (item.Model.SourceFileName ?? item.Model.FileName ?? "") : ""; }
            catch { return ""; }
        }

        private static IEnumerable<ModelItem> WalkGeometry(IEnumerable<ModelItem> items)
        {
            foreach (var item in items)
            {
                if (item.HasGeometry) yield return item;
                foreach (var child in WalkGeometry(item.Children)) yield return child;
            }
        }

        private static string[] SplitLine(string line, char sep)
        {
            // Handles quoted fields
            var cols = new List<string>();
            bool inQuote = false;
            var cur = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"')  { inQuote = !inQuote; continue; }
                if (c == sep && !inQuote) { cols.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            cols.Add(cur.ToString());
            return cols.ToArray();
        }

        private static int FindHeader(string[] headers, params string[] names)
        {
            for (int i = 0; i < headers.Length; i++)
                foreach (var n in names)
                    if (headers[i].Trim().Equals(n, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static int FindColumn(DataTable table, params string[] names)
        {
            for (int i = 0; i < table.Columns.Count; i++)
                foreach (var n in names)
                    if (table.Columns[i].ColumnName.Trim().Equals(n, StringComparison.OrdinalIgnoreCase))
                        return i;
            return -1;
        }

        private static string Esc(string s)
        {
            if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
