using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace NavisworksIfcExporter.Core
{
    public static class IfcTypeMapper
    {
        // Maps using already-collected properties (no re-extraction)
        public static string MapFromProperties(Dictionary<string, Dictionary<string, string>> props, ModelItem item)
        {
            // Look for a "Category" property in any property set
            string category = string.Empty;

            // Try standard Navisworks "Item" category first
            if (props.TryGetValue("Item", out var itemPset))
                itemPset.TryGetValue("Category", out category);

            // Fallback: search all psets for a "Category" key
            if (string.IsNullOrEmpty(category))
            {
                foreach (var pset in props.Values)
                    if (pset.TryGetValue("Category", out var v) && !string.IsNullOrEmpty(v))
                    { category = v; break; }
            }

            // Also check the display name of the item itself
            if (string.IsNullOrEmpty(category))
                category = item.DisplayName ?? string.Empty;

            return MapCategory(category);
        }

        private static string MapCategory(string category)
        {
            var c = category.ToLowerInvariant();
            if (c.Contains("wall"))        return "IfcWall";
            if (c.Contains("floor"))       return "IfcSlab";
            if (c.Contains("slab"))        return "IfcSlab";
            if (c.Contains("roof"))        return "IfcRoof";
            if (c.Contains("door"))        return "IfcDoor";
            if (c.Contains("window"))      return "IfcWindow";
            if (c.Contains("stair"))       return "IfcStair";
            if (c.Contains("column"))      return "IfcColumn";
            if (c.Contains("beam"))        return "IfcBeam";
            if (c.Contains("pipe"))        return "IfcPipeSegment";
            if (c.Contains("duct"))        return "IfcDuctSegment";
            if (c.Contains("equipment"))   return "IfcFlowTerminal";
            if (c.Contains("furniture"))   return "IfcFurnishingElement";
            return "IfcBuildingElementProxy";
        }
    }
}
