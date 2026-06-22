using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.Core
{
    public class ExportOptions
    {
        public string OutputPath       { get; set; } = string.Empty;
        public bool   IncludeHidden    { get; set; } = false;
        public bool   ExportGeometry   { get; set; } = true;
        public bool   SelectionOnly    { get; set; } = false;
        public string AuthorName       { get; set; } = "Exportador";
        public string OrganizationName { get; set; } = "PHD";
        public List<MappingRule> MappingRules { get; set; } = new List<MappingRule>();
    }

    public class ExportService
    {
        public event EventHandler<string>? ProgressChanged;

        public void Export(ExportOptions options)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument
                      ?? throw new InvalidOperationException("Nenhum documento aberto.");

            IEnumerable<ModelItem> sourceItems;
            if (options.SelectionOnly)
            {
                var selection = doc.CurrentSelection.SelectedItems;
                if (!selection.Any())
                    throw new InvalidOperationException("Nenhum elemento selecionado.");
                sourceItems = selection;
            }
            else
            {
                sourceItems = doc.Models.RootItems;
            }

            Report("Percorrendo modelo...");
            var traverser = new ModelTraverser(options.MappingRules);
            traverser.ProgressChanged += (_, msg) => Report(msg);
            var elements  = traverser.Traverse(sourceItems, options.IncludeHidden, options.ExportGeometry);

            Report("Escrevendo arquivo IFC 4...");
            var writer = new IfcWriter(options.AuthorName, options.OrganizationName);
            writer.Write(elements, options.OutputPath);

            Report($"Concluído. Arquivo salvo em: {options.OutputPath}");
        }

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}
