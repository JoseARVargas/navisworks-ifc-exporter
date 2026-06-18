using Autodesk.Navisworks.Api.Plugins;
using NavisworksIfcExporter.UI;

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
