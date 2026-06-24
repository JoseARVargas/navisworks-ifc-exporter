using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace NavisworksIfcExporter.Core
{
    public class ClashExportService
    {
        public event EventHandler<string>? ProgressChanged;

        private static readonly string[] Header = {
            "TesteName", "GrupoNome", "ClashNome", "Status", "Tipo",
            "Distancia_m", "Prioridade", "Descricao", "AtribuidoPara",
            "DataCriacao", "DataAprovacao", "DataResolucao",
            "ElementoA_GUID", "ElementoA_Nome", "ElementoA_Classe", "ElementoA_Arquivo",
            "ElementoB_GUID", "ElementoB_Nome", "ElementoB_Classe", "ElementoB_Arquivo",
            "Centro_X", "Centro_Y", "Centro_Z",
        };

        public void Export(IEnumerable<ClashTest> tests, string outputPath, char separator = ';')
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(true));

            writer.WriteLine(string.Join(separator.ToString(), Header.Select(h => QuoteField(h, separator))));

            int total = 0;
            foreach (var test in tests)
            {
                Report($"Processando: {test.DisplayName}");
                foreach (var (result, groupName) in GetResults(test))
                {
                    writer.WriteLine(BuildLine(test.DisplayName, groupName, result, separator));
                    total++;
                }
            }

            Report($"Concluído: {total} clash(es) exportado(s) → {Path.GetFileName(outputPath)}");
        }

        // -----------------------------------------------------------------------
        // Traversal
        // -----------------------------------------------------------------------

        public static IEnumerable<ClashTest> GetAllTests(SavedItemCollection items)
        {
            foreach (SavedItem item in items)
            {
                if (item is ClashTest test)         yield return test;
                else if (item is ClashTestFolder f) foreach (var t in GetAllTests(f.Children)) yield return t;
            }
        }

        private static IEnumerable<(ClashResult result, string groupName)> GetResults(ClashTest test)
        {
            foreach (SavedItem item in test.Children)
            {
                if (item is ClashResult r)
                    yield return (r, "");
                else if (item is ClashResultGroup g)
                    foreach (SavedItem child in g.Children)
                        if (child is ClashResult cr) yield return (cr, g.DisplayName);
            }
        }

        public static int CountResults(ClashTest test)
        {
            int n = 0;
            foreach (SavedItem item in test.Children)
            {
                if (item is ClashResult) n++;
                else if (item is ClashResultGroup g)
                    foreach (SavedItem child in g.Children)
                        if (child is ClashResult) n++;
            }
            return n;
        }

        // -----------------------------------------------------------------------
        // Row building
        // -----------------------------------------------------------------------

        private static string BuildLine(string testName, string groupName, ClashResult r, char sep)
        {
            var cols = new[] {
                testName,
                groupName,
                r.DisplayName,
                TranslateStatus(r.Status),
                TranslateType(r.TestType),
                r.Distance.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                r.Priority.ToString(),
                r.Description ?? "",
                r.AssignedTo?.DisplayName ?? "",
                FormatDate(r.CreatedTime),
                FormatDate(r.ApprovedTime),
                FormatDate(r.ResolvedTime),
                SafeGuid(r.Item1),
                SafeName(r.Item1),
                SafeClass(r.Item1),
                SafeFile(r.Item1),
                SafeGuid(r.Item2),
                SafeName(r.Item2),
                SafeClass(r.Item2),
                SafeFile(r.Item2),
                r.Center.X.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                r.Center.Y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                r.Center.Z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            };
            return string.Join(sep.ToString(), cols.Select(c => QuoteField(c, sep)));
        }

        private static string SafeGuid(ModelItem? item) => item?.InstanceGuid.ToString() ?? "";
        private static string SafeName(ModelItem? item) => item?.DisplayName ?? "";
        private static string SafeClass(ModelItem? item) => item?.ClassDisplayName ?? "";

        private static string SafeFile(ModelItem? item)
        {
            if (item == null) return "";
            // AncestorsAndSelf is ordered from self upward; Last() is the root model node
            return item.AncestorsAndSelf.LastOrDefault()?.DisplayName ?? "";
        }

        private static string FormatDate(DateTime? dt)
            => dt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

        private static string TranslateStatus(ClashResultStatus s) => s switch {
            ClashResultStatus.New      => "Novo",
            ClashResultStatus.Active   => "Ativo",
            ClashResultStatus.Reviewed => "Revisado",
            ClashResultStatus.Approved => "Aprovado",
            ClashResultStatus.Resolved => "Resolvido",
            _                          => s.ToString(),
        };

        private static string TranslateType(ClashTestType t) => t switch {
            ClashTestType.Hard             => "Duro",
            ClashTestType.HardConservative => "Duro conservador",
            ClashTestType.Clearance        => "Folga",
            ClashTestType.Duplicate        => "Duplicado",
            ClashTestType.Custom           => "Personalizado",
            _                              => t.ToString(),
        };

        private static string QuoteField(string value, char separator)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(separator) || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private void Report(string msg) => ProgressChanged?.Invoke(this, msg);
    }
}
