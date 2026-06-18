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

        // Categorias padrão do Navisworks presentes em qualquer nó de geometria.
        // Quando o item só tem estas, precisamos subir na hierarquia para encontrar
        // as propriedades da aplicação de origem (Revit, AutoCAD, Civil 3D, etc.).
        private static readonly HashSet<string> _basicNavisCategories = new HashSet<string>
        {
            "Item", "Material"
        };

        // Coleta propriedades do item e, se necessário, de seus ancestrais.
        // Funciona para qualquer formato suportado pelo Navisworks.
        private Dictionary<string, Dictionary<string, string>> CollectProperties(ModelItem item)
        {
            var ownProps = _propertyExtractor.Extract(item);

            // Item já tem categorias da aplicação de origem → usa direto.
            if (ownProps.Keys.Any(k => !_basicNavisCategories.Contains(k)))
                return ownProps;

            // Só tem "Item" e/ou "Material": sobe pelos ancestrais para capturar
            // propriedades de tipo, família, layer, etc. (máx. 8 níveis).
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
