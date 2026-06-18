using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace NavisworksIfcExporter.Core
{
    /// <summary>
    /// Extrai todas as propriedades de um ModelItem e organiza por PropertySet.
    /// </summary>
    public class PropertyExtractor
    {
        // Categorias internas do Navisworks que não são úteis no IFC
        private static readonly HashSet<string> IgnoredCategories = new()
        {
            "Autodesk.Navisworks.Node",
            "Autodesk.Navisworks.Internal",
        };

        public Dictionary<string, Dictionary<string, string>> Extract(ModelItem item)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            foreach (var category in item.PropertyCategories)
            {
                if (IgnoredCategories.Contains(category.Name))
                    continue;

                var psetName = SanitizeName(category.DisplayName ?? category.Name);
                var props = new Dictionary<string, string>();

                foreach (var property in category.Properties)
                {
                    var propName = SanitizeName(property.DisplayName ?? property.Name.Name);
                    var propValue = property.Value?.ToDisplayString() ?? string.Empty;
                    props[propName] = propValue;
                }

                if (props.Count > 0)
                    result[psetName] = props;
            }

            return result;
        }

        private static string SanitizeName(string name) =>
            name.Replace(".", "_").Replace(" ", "_").Trim();
    }
}
