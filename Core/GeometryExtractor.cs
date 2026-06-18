using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using NavisworksIfcExporter.Models;

namespace NavisworksIfcExporter.Core
{
    public class GeometryExtractor
    {
        // Set when COM tessellation fails — lets the caller log the reason.
        public string LastComError { get; private set; } = string.Empty;

        public GeometryData? Extract(ModelItem item)
        {
            if (!item.HasGeometry)
                return null;

            // Try full tessellation first; fall back to bounding box.
            var tessellated = TryComTessellation(item);
            if (tessellated != null)
                return tessellated;

            return ExtractBoundingBox(item);
        }

        // -----------------------------------------------------------------------
        // COM tessellation (exact geometry)
        // -----------------------------------------------------------------------

        private GeometryData? TryComTessellation(ModelItem item)
        {
            var callback = new TriangleCollector();
            try
            {
                var comPath = (InwOaPath3)ComApiBridge.ToInwOaPath(item);
                var frags   = (IEnumerable)comPath.Fragments();

                foreach (InwOaFragment3 frag in frags)
                    frag.GenerateSimplePrimitives(nwEVertexProperty.eNONE, callback);
            }
            catch (Exception ex)
            {
                LastComError = ex.GetType().Name + ": " + ex.Message;
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

        // -----------------------------------------------------------------------
        // Bounding-box fallback (12 triangles, one box per element)
        // -----------------------------------------------------------------------

        private static GeometryData? ExtractBoundingBox(ModelItem item)
        {
            var bb = item.Geometry?.BoundingBox;
            if (bb == null || bb.IsEmpty)
                return null;

            var mn = bb.Min;
            var mx = bb.Max;

            var verts = new List<double[]>
            {
                new[] { mn.X, mn.Y, mn.Z }, // 0
                new[] { mx.X, mn.Y, mn.Z }, // 1
                new[] { mx.X, mx.Y, mn.Z }, // 2
                new[] { mn.X, mx.Y, mn.Z }, // 3
                new[] { mn.X, mn.Y, mx.Z }, // 4
                new[] { mx.X, mn.Y, mx.Z }, // 5
                new[] { mx.X, mx.Y, mx.Z }, // 6
                new[] { mn.X, mx.Y, mx.Z }, // 7
            };

            var tris = new List<int[]>
            {
                new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, // bottom
                new[] { 4, 5, 6 }, new[] { 4, 6, 7 }, // top
                new[] { 0, 1, 5 }, new[] { 0, 5, 4 }, // front
                new[] { 3, 6, 2 }, new[] { 3, 7, 6 }, // back
                new[] { 0, 4, 7 }, new[] { 0, 7, 3 }, // left
                new[] { 1, 2, 6 }, new[] { 1, 6, 5 }, // right
            };

            return new GeometryData { Vertices = verts, Triangles = tris };
        }
    }

    // -----------------------------------------------------------------------
    // COM callback — collects triangles from GenerateSimplePrimitives
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
