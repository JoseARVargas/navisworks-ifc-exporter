using Autodesk.Navisworks.Api.Plugins;
using NavisworksIfcExporter.UI;

namespace NavisworksIfcExporter
{
    [Plugin("ExportIfc", "PHD",
        DisplayName = "Exportar IFC 4",
        ToolTip     = "Exporta o modelo para IFC 4 com propriedades e geometria")]
    [AddInPlugin(AddInLocation.None)]
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
