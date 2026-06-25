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

    [Plugin("ExportIfcSearchSet", "PHD",
        DisplayName = "Exportar IFC por Search Set",
        ToolTip     = "Exporta um search set ou selection set específico para IFC 4")]
    [AddInPlugin(AddInLocation.None)]
    public class ExportIfcSearchSetPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var window = new SearchSetExportWindow();
            window.ShowDialog();
            return 0;
        }
    }

    [Plugin("ExportClashCsv", "PHD",
        DisplayName = "Exportar Clashes CSV",
        ToolTip     = "Exporta os resultados do Clash Detective para CSV (compatível com Power BI)")]
    [AddInPlugin(AddInLocation.None)]
    public class ExportClashCsvPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new ClashExportWindow().ShowDialog();
            return 0;
        }
    }

    [Plugin("ExportIfcSelection", "PHD",
        DisplayName = "Exportar IFC — Seleção",
        ToolTip     = "Exporta para IFC apenas os elementos selecionados na viewport")]
    [AddInPlugin(AddInLocation.None)]
    public class ExportIfcSelectionPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new ExportWindow(selectionOnly: true).ShowDialog();
            return 0;
        }
    }

    [Plugin("ExportFbxSets", "PHD",
        DisplayName = "Exportar FBX por Sets",
        ToolTip     = "Isola elementos de Search Sets na viewport para exportação FBX")]
    [AddInPlugin(AddInLocation.None)]
    public class ExportFbxSetsPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new FbxExportWindow().ShowDialog();
            return 0;
        }
    }

    [Plugin("QtoAutoAttach", "PHD",
        DisplayName = "QTO Auto Attach",
        ToolTip     = "Vincula elementos do modelo ao Quantity Takeoff por Search Set ou propriedade")]
    [AddInPlugin(AddInLocation.None)]
    public class QtoAutoAttachPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new QtoWindow().ShowDialog();
            return 0;
        }
    }

    [Plugin("HighlightSelection", "PHD",
        DisplayName = "Realçar Seleção",
        ToolTip     = "Aplica cor e transparência aos elementos não selecionados para destacar a seleção na viewport")]
    [AddInPlugin(AddInLocation.None)]
    public class HighlightSelectionPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new HighlightSelectionWindow().ShowDialog();
            return 0;
        }
    }

    [Plugin("CheckProperties", "PHD",
        DisplayName = "Verificar Propriedades",
        ToolTip     = "Verifica o preenchimento de propriedades por disciplina a partir de um arquivo de regras")]
    [AddInPlugin(AddInLocation.None)]
    public class CheckPropertiesPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new CheckWindow().ShowDialog();
            return 0;
        }
    }

    [Plugin("ResetAppearance", "PHD",
        DisplayName = "Restaurar Aparência",
        ToolTip     = "Remove todas as sobreposições de cor e transparência temporárias do modelo")]
    [AddInPlugin(AddInLocation.None)]
    public class ResetAppearancePlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            Autodesk.Navisworks.Api.Application.ActiveDocument
                ?.Models.ResetAllTemporaryMaterials();
            return 0;
        }
    }

    [Plugin("CheckIDS", "PHD",
        DisplayName = "Verificar IDS",
        ToolTip     = "Valida o modelo contra um arquivo IDS (Information Delivery Specification) buildingSMART")]
    [AddInPlugin(AddInLocation.None)]
    public class CheckIdsPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            new UI.IdsWindow().ShowDialog();
            return 0;
        }
    }
}
