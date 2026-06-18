using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.Core
{
    public class ModelTraverser
    {
        private readonly PropertyExtractor _propertyExtractor = new();
        private readonly GeometryExtractor _geometryExtractor = new();

        public IEnumerable<ElementData> Traverse(ModelItemCollection items, bool includeHidden, bool exportGeometry)
        {
            var results = new List<ElementData>();
            foreach (var item in items)
                TraverseItem(item, results, includeHidden, exportGeometry);
            return results;
        }

        private void TraverseItem(ModelItem item, List<ElementData> results, bool includeHidden, bool exportGeometry)
        {
            if (!includeHidden && item.IsHidden)
                return;

            // Só exporta itens folha que possuem geometria ou são instâncias
            if (item.HasGeometry || IsLeafWithProperties(item))
            {
                var element = new ElementData
                {
                    Id = item.InstanceGuid.ToString(),
                    Name = item.DisplayName ?? "(sem nome)",
                    Category = GetCategory(item),
                    IfcType = IfcTypeMapper.Map(item),
                    PropertySets = _propertyExtractor.Extract(item),
                };

                if (exportGeometry && item.HasGeometry)
                    element.Geometry = _geometryExtractor.Extract(item);

                results.Add(element);
            }

            foreach (var child in item.Children)
                TraverseItem(child, results, includeHidden, exportGeometry);
        }

        private static bool IsLeafWithProperties(ModelItem item)
        {
            // Considera folhas sem geometria mas com propriedades (ex: grupos com PSets)
            return !item.Children.Any && item.PropertyCategories.Count > 0;
        }

        private static string GetCategory(ModelItem item)
        {
            var cat = item.PropertyCategories.FindPropertyCategory("Item");
            return cat?.Properties.FindPropertyByDisplayName("Category")?.Value?.ToDisplayString()
                   ?? "Unknown";
        }
    }
}
