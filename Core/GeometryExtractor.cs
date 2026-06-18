using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Interop;
using Autodesk.Navisworks.Api.Interop.ComApi;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.Core
{
    /// <summary>
    /// Extrai geometria tessellada de um ModelItem usando a COM API do Navisworks.
    /// Os vértices retornados estão em coordenadas de mundo (world space).
    /// </summary>
    public class GeometryExtractor
    {
        public GeometryData? Extract(ModelItem item)
        {
            if (!item.HasGeometry)
                return null;

            var callback = new TriangleCollector();

            try
            {
                // Converte o ModelItem gerenciado para o path COM
                var comPath = ComBridge.ToInwOaPath(item.CreatePath());
                comPath.GenerateSimplePrimitives(nwEVertexFormat.eVFCoords, callback);
            }
            catch (Exception)
            {
                // Elemento sem geometria acessível via COM (ex: apenas bounding box)
                return null;
            }

            if (callback.Vertices.Count == 0)
                return null;

            return new GeometryData
            {
                Vertices  = callback.Vertices,
                Triangles = callback.Triangles,
            };
        }
    }

    // -------------------------------------------------------------------------
    // Callback COM que recebe os triângulos primitivos do Navisworks
    // -------------------------------------------------------------------------
    internal class TriangleCollector : InwSimplePrimitivesCB
    {
        // Mapa vértice→índice para deduplicação
        private readonly Dictionary<string, int> _vertexIndex = new();

        public List<double[]> Vertices  { get; } = new();
        public List<int[]>    Triangles { get; } = new();

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            var i0 = AddVertex(v1);
            var i1 = AddVertex(v2);
            var i2 = AddVertex(v3);
            Triangles.Add(new[] { i0, i1, i2 });
        }

        // Primitivas não relevantes para IFC sólido — ignoradas
        public void Line(InwSimpleVertex v1, InwSimpleVertex v2) { }
        public void Point(InwSimpleVertex v1) { }
        public void SnapPoint(InwSimpleVertex v1) { }

        private int AddVertex(InwSimpleVertex v)
        {
            // coord é InwLPos3f: data1=X, data2=Y, data3=Z
            var pos = (Array)v.coord;
            double x = (double)pos.GetValue(1);
            double y = (double)pos.GetValue(2);
            double z = (double)pos.GetValue(3);

            // Chave para deduplicação com precisão de 6 casas
            var key = $"{x:F6},{y:F6},{z:F6}";
            if (_vertexIndex.TryGetValue(key, out var idx))
                return idx;

            idx = Vertices.Count;
            Vertices.Add(new[] { x, y, z });
            _vertexIndex[key] = idx;
            return idx;
        }
    }
}
