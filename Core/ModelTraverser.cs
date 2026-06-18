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

            if (item.HasGeometry || IsLeafWithProperties(item))
            {
                var props = CollectProperties(item);

                var element = new ElementData
                {
                    Id       = item.InstanceGuid.ToString(),
                    Name     = item.DisplayName ?? "(sem nome)",
                    Category = GetCategory(item, props),
                    IfcType  = IfcTypeMapper.Map(props, item),
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

        // Coleta propriedades do item. Se o item não tem dados além do "Item" básico
        // do Navisworks, sobe pelos ancestrais para capturar propriedades de tipo/família.
        // Isso funciona para qualquer formato (Revit, AutoCAD, DGN, IFC, etc.).
        private Dictionary<string, Dictionary<string, string>> CollectProperties(ModelItem item)
        {
            var ownProps = _propertyExtractor.Extract(item);

            // Item já tem propriedades além do "Item" padrão do Navisworks → suficiente.
            if (ownProps.Keys.Any(k => k != "Item"))
                return ownProps;

            // Só tem "Item" ou nada: busca em ancestrais (máx. 8 níveis para não
            // incluir dados de projeto/site em modelos com hierarquia profunda).
            var result = new Dictionary<string, Dictionary<string, string>>(ownProps);
            int depth = 0;

            foreach (var ancestor in item.Ancestors)
            {
                if (++depth > 8) break;

                foreach (var kvp in _propertyExtractor.Extract(ancestor))
                    if (!result.ContainsKey(kvp.Key))
                        result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        private static bool IsLeafWithProperties(ModelItem item) =>
            !item.Children.Any() && item.PropertyCategories.Any();

        private void Report(string message) => ProgressChanged?.Invoke(this, message);

        // Determina a categoria legível do elemento a partir de diversas fontes.
        private static string GetCategory(ModelItem item, Dictionary<string, Dictionary<string, string>> props)
        {
            // 1. "Item" → "Category" (presente em qualquer formato no Navisworks)
            if (props.TryGetValue("Item", out var itemPset))
            {
                if (itemPset.TryGetValue("Category", out var cat) && !string.IsNullOrWhiteSpace(cat))
                    return cat;
                if (itemPset.TryGetValue("Layer", out var layer) && !string.IsNullOrWhiteSpace(layer))
                    return layer;
            }

            // 2. ClassDisplayName / ClassName do Navisworks (ex.: "Wall", "LINE", "IfcWall")
            var className = item.ClassDisplayName ?? item.ClassName;
            if (!string.IsNullOrWhiteSpace(className))
                return className;

            return "Unknown";
        }
    }
}
