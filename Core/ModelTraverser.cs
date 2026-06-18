using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.Core
{
    public class ModelTraverser
    {
        private readonly PropertyExtractor _propertyExtractor = new();
        private readonly GeometryExtractor _geometryExtractor = new();

        public event EventHandler<string>? ProgressChanged;

        public IEnumerable<ElementData> Traverse(IEnumerable<ModelItem> items, bool includeHidden, bool exportGeometry)
        {
            var results = new List<ElementData>();
            bool comErrorLogged = false;
            int withProps = 0;
            int withoutProps = 0;

            foreach (var item in items)
                TraverseItem(item, results, includeHidden, exportGeometry, ref comErrorLogged, ref withProps, ref withoutProps);

            Report($"  {results.Count} elementos exportados ({withProps} com propriedades, {withoutProps} sem).");
            return results;
        }

        private void TraverseItem(ModelItem item, List<ElementData> results, bool includeHidden, bool exportGeometry,
            ref bool comErrorLogged, ref int withProps, ref int withoutProps)
        {
            if (!includeHidden && item.IsHidden)
                return;

            // Só exporta itens folha que possuem geometria ou são instâncias
            if (item.HasGeometry || IsLeafWithProperties(item))
            {
                var props = CollectProperties(item);

                var element = new ElementData
                {
                    Id = item.InstanceGuid.ToString(),
                    Name = item.DisplayName ?? "(sem nome)",
                    Category = GetCategory(item),
                    IfcType = IfcTypeMapper.MapFromProperties(props, item),
                    PropertySets = props,
                };

                if (props.Count > 0) withProps++; else withoutProps++;

                if (exportGeometry && item.HasGeometry)
                {
                    element.Geometry = _geometryExtractor.Extract(item);

                    if (!comErrorLogged && _geometryExtractor.LastComError.Length > 0)
                    {
                        Report($"  Aviso: tessellação COM falhou ({_geometryExtractor.LastComError}). Usando bounding box.");
                        comErrorLogged = true;
                    }
                }

                results.Add(element);
            }

            foreach (var child in item.Children)
                TraverseItem(child, results, includeHidden, exportGeometry, ref comErrorLogged, ref withProps, ref withoutProps);
        }

        // Coleta propriedades do item e de seus ancestrais (para capturar dados do tipo/família Revit)
        private Dictionary<string, Dictionary<string, string>> CollectProperties(ModelItem item)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            // Ancestors primeiro (menor prioridade: root → parent)
            foreach (var ancestor in item.Ancestors)
            {
                foreach (var kvp in _propertyExtractor.Extract(ancestor))
                    if (!result.ContainsKey(kvp.Key))
                        result[kvp.Key] = kvp.Value;
            }

            // Propriedades do próprio item sobrepõem ancestrais
            foreach (var kvp in _propertyExtractor.Extract(item))
                result[kvp.Key] = kvp.Value;

            return result;
        }

        private static bool IsLeafWithProperties(ModelItem item)
        {
            return !item.Children.Any() && item.PropertyCategories.Any();
        }

        private void Report(string message) => ProgressChanged?.Invoke(this, message);

        private static string GetCategory(ModelItem item)
        {
            var cat = item.PropertyCategories.FindCategoryByName("Item");
            var val = cat?.Properties
                         .FirstOrDefault(p => p.DisplayName == "Category")?
                         .Value;
            return val != null ? PropertyExtractor.SafeString(val) : "Unknown";
        }
    }
}
