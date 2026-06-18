using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.Core
{
    /// <summary>
    /// Extrai geometria tessellada de um ModelItem via fragments da COM API do Navisworks 2025+.
    /// Cada fragmento representa uma malha de triângulos em coordenadas locais.
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
                var comPath = (InwOaPath3)ComApiBridge.ToInwOaPath(item);
                var frags = (IEnumerable)comPath.Fragments();

                foreach (InwOaFragment3 frag in frags)
                {
                    frag.GenerateSimplePrimitives(nwEVertexProperty.eNONE, callback);
                }
            }
            catch
            {
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

    // -----------------------------------------------------------------------
    // Callback COM que recebe os triângulos primitivos do Navisworks
    // -----------------------------------------------------------------------
    internal class TriangleCollector : InwSimplePrimitivesCB
    {
        private readonly Dictionary<string, int> _vertexIndex = new Dictionary<string, int>();

        public List<double[]> Vertices  { get; } = new List<double[]>();
        public List<int[]>    Triangles { get; } = new List<int[]>();

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            var i0 = AddVertex(v1);
            var i1 = AddVertex(v2);
            var i2 = AddVertex(v3);
            Triangles.Add(new[] { i0, i1, i2 });
        }

        public void Line(InwSimpleVertex v1, InwSimpleVertex v2) { }
        public void Point(InwSimpleVertex v1) { }
        public void SnapPoint(InwSimpleVertex v1) { }

        private int AddVertex(InwSimpleVertex v)
        {
            var pos = (Array)v.coord;
            double x = (double)pos.GetValue(1);
            double y = (double)pos.GetValue(2);
            double z = (double)pos.GetValue(3);

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
