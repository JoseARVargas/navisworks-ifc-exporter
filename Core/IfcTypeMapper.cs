using System.Linq;
using Autodesk.Navisworks.Api;

namespace NavisworksIfcExporter.Core
{
    /// <summary>
    /// Mapeia categorias do Navisworks para tipos IFC 4.
    /// Adicione mais mapeamentos conforme necessário para o seu modelo.
    /// </summary>
    public static class IfcTypeMapper
    {
        public static string Map(ModelItem item)
        {
            var category = item.PropertyCategories
                              .FindCategoryByName("Item")?
                              .Properties
                              .FirstOrDefault(p => p.DisplayName == "Category")?
                              .Value?.ToDisplayString() ?? string.Empty;

            return category.ToLowerInvariant() switch
            {
                var c when c.Contains("wall")        => "IfcWall",
                var c when c.Contains("floor")       => "IfcSlab",
                var c when c.Contains("slab")        => "IfcSlab",
                var c when c.Contains("roof")        => "IfcRoof",
                var c when c.Contains("door")        => "IfcDoor",
                var c when c.Contains("window")      => "IfcWindow",
                var c when c.Contains("stair")       => "IfcStair",
                var c when c.Contains("column")      => "IfcColumn",
                var c when c.Contains("beam")        => "IfcBeam",
                var c when c.Contains("pipe")        => "IfcPipeSegment",
                var c when c.Contains("duct")        => "IfcDuctSegment",
                var c when c.Contains("equipment")   => "IfcFlowTerminal",
                var c when c.Contains("furniture")   => "IfcFurnishingElement",
                _                                    => "IfcBuildingElementProxy",
            };
        }
    }
}
