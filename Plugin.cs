using Autodesk.Navisworks.Api.Plugins;
using NavisworksIfcExporter.UI;

// Registra o plugin no Navisworks.
// "NavisworksIfcExporter" = nome único do plugin
// "PHD" = ID do desenvolvedor (máx. 4 chars, único por empresa)
[assembly: Plugin(
    "NavisworksIfcExporter",
    "PHD",
    ToolTip     = "Exporta o modelo atual para o formato IFC 4 com propriedades e geometria tessellada",
    DisplayName = "Exportar IFC 4")]

namespace NavisworksIfcExporter
{
    [Plugin("ExportIfc", "PHD",
        DisplayName = "Exportar IFC 4",
        ToolTip     = "Abre o diálogo de exportação IFC 4")]
    [AddInPlugin(AddInLocation.Export)]
    public class ExportIfcPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var window = new ExportWindow();
            window.ShowDialog();
            return 0;
        }
    }
}
