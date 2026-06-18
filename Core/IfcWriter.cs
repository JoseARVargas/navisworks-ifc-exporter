using System;
using System.Collections.Generic;
using System.Linq;
using NavisworksIfcExporter.Models;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.SharedBldgElements;

namespace NavisworksIfcExporter.Core
{
    public class IfcWriter
    {
        private readonly string _authorName;
        private readonly string _organizationName;

        public IfcWriter(string authorName = "Exportador", string organizationName = "PHD")
        {
            _authorName = authorName;
            _organizationName = organizationName;
        }

        public void Write(IEnumerable<ElementData> elements, string outputPath)
        {
            var credentials = new XbimEditorCredentials
            {
                ApplicationDevelopersName = _organizationName,
                ApplicationFullName = "NavisworksIfcExporter",
                ApplicationVersion = "1.0",
                EditorsFamilyName = _authorName,
                EditorsOrganisationName = _organizationName,
            };

            using var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
            using var txn = model.BeginTransaction("Navisworks IFC Export");

            var project  = CreateProject(model);
            var site     = CreateSite(model, project);
            var building = CreateBuilding(model, site);
            var storey   = CreateStorey(model, building);

            foreach (var element in elements)
                CreateElement(model, storey, element);

            txn.Commit();
            model.SaveAs(outputPath, StorageType.Ifc);
        }

        // -----------------------------------------------------------------------
        // Estrutura hierárquica IFC
        // -----------------------------------------------------------------------

        private IfcProject CreateProject(IfcStore model)
        {
            var project = model.Instances.New<IfcProject>(p =>
            {
                p.Name = "Projeto Navisworks";
                p.UnitsInContext = CreateUnits(model);
                p.RepresentationContexts.Add(CreateGeomContext(model));
            });
            return project;
        }

        private IfcUnitAssignment CreateUnits(IfcStore model)
        {
            return model.Instances.New<IfcUnitAssignment>(u =>
            {
                u.Units.Add(model.Instances.New<IfcSIUnit>(si =>
                {
                    si.UnitType = IfcUnitEnum.LENGTHUNIT;
                    si.Name = IfcSIUnitName.METRE;
                }));
                u.Units.Add(model.Instances.New<IfcSIUnit>(si =>
                {
                    si.UnitType = IfcUnitEnum.AREAUNIT;
                    si.Name = IfcSIUnitName.SQUARE_METRE;
                }));
                u.Units.Add(model.Instances.New<IfcSIUnit>(si =>
                {
                    si.UnitType = IfcUnitEnum.VOLUMEUNIT;
                    si.Name = IfcSIUnitName.CUBIC_METRE;
                }));
            });
        }

        private IfcGeometricRepresentationContext CreateGeomContext(IfcStore model)
        {
            return model.Instances.New<IfcGeometricRepresentationContext>(ctx =>
            {
                ctx.ContextType = "Model";
                ctx.CoordinateSpaceDimension = 3;
                ctx.Precision = 1e-5;
                ctx.WorldCoordinateSystem = model.Instances.New<IfcAxis2Placement3D>(a =>
                {
                    a.Location = model.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(0, 0, 0));
                });
            });
        }

        private IfcSite CreateSite(IfcStore model, IfcProject project)
        {
            var site = model.Instances.New<IfcSite>(s =>
            {
                s.Name = "Site";
                s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                s.ObjectPlacement = CreatePlacement(model);
            });
            model.Instances.New<IfcRelAggregates>(rel =>
            {
                rel.RelatingObject = project;
                rel.RelatedObjects.Add(site);
            });
            return site;
        }

        private IfcBuilding CreateBuilding(IfcStore model, IfcSite site)
        {
            var building = model.Instances.New<IfcBuilding>(b =>
            {
                b.Name = "Edificio";
                b.CompositionType = IfcElementCompositionEnum.ELEMENT;
                b.ObjectPlacement = CreatePlacement(model);
            });
            model.Instances.New<IfcRelAggregates>(rel =>
            {
                rel.RelatingObject = site;
                rel.RelatedObjects.Add(building);
            });
            return building;
        }

        private IfcBuildingStorey CreateStorey(IfcStore model, IfcBuilding building)
        {
            var storey = model.Instances.New<IfcBuildingStorey>(s =>
            {
                s.Name = "Pavimento 1";
                s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                s.ObjectPlacement = CreatePlacement(model);
                s.Elevation = 0.0;
            });
            model.Instances.New<IfcRelAggregates>(rel =>
            {
                rel.RelatingObject = building;
                rel.RelatedObjects.Add(storey);
            });
            return storey;
        }

        // -----------------------------------------------------------------------
        // Criação do elemento IFC com propriedades e geometria
        // -----------------------------------------------------------------------

        private void CreateElement(IfcStore model, IfcBuildingStorey storey, ElementData data)
        {
            var element = InstantiateIfcElement(model, data.IfcType);
            element.Name = data.Name;
            element.GlobalId = IfcGloballyUniqueId.ConvertToBase64(Guid.NewGuid());
            element.ObjectPlacement = CreatePlacement(model);

            if (data.Geometry != null)
                element.Representation = CreateShapeRepresentation(model, data.Geometry);

            AddPropertySets(model, element, data.PropertySets);

            // Relaciona ao pavimento
            model.Instances.New<IfcRelContainedInSpatialStructure>(rel =>
            {
                rel.RelatingStructure = storey;
                rel.RelatedElements.Add(element);
            });
        }

        private static IfcElement InstantiateIfcElement(IfcStore model, string ifcType)
        {
            // Cria o tipo IFC correto com base no mapeamento
            return ifcType switch
            {
                "IfcWall"                   => model.Instances.New<IfcWall>(),
                "IfcSlab"                   => model.Instances.New<IfcSlab>(),
                "IfcBeam"                   => model.Instances.New<IfcBeam>(),
                "IfcColumn"                 => model.Instances.New<IfcColumn>(),
                "IfcDoor"                   => model.Instances.New<IfcDoor>(),
                "IfcWindow"                 => model.Instances.New<IfcWindow>(),
                "IfcRoof"                   => model.Instances.New<IfcRoof>(),
                "IfcStair"                  => model.Instances.New<IfcStair>(),
                "IfcFurnishingElement"       => model.Instances.New<IfcFurnishingElement>(),
                _                           => model.Instances.New<IfcBuildingElementProxy>(),
            };
        }

        // -----------------------------------------------------------------------
        // Geometria: IfcTriangulatedFaceSet (IFC4 nativo)
        // -----------------------------------------------------------------------

        private IfcProductDefinitionShape CreateShapeRepresentation(IfcStore model, GeometryData geom)
        {
            // Constrói a lista de pontos 3D
            var coordList = model.Instances.New<IfcCartesianPointList3D>(cpl =>
            {
                foreach (var v in geom.Vertices)
                {
                    var row = cpl.CoordList.GetAt(cpl.CoordList.Count);
                    row.Add(new IfcLengthMeasure(v[0]));
                    row.Add(new IfcLengthMeasure(v[1]));
                    row.Add(new IfcLengthMeasure(v[2]));
                }
            });

            // Constrói o IfcTriangulatedFaceSet
            var faceSet = model.Instances.New<IfcTriangulatedFaceSet>(fs =>
            {
                fs.Coordinates = coordList;
                fs.Closed = false;
                foreach (var tri in geom.Triangles)
                {
                    var face = fs.CoordIndex.GetAt(fs.CoordIndex.Count);
                    face.Add(new IfcPositiveInteger(tri[0] + 1)); // IFC usa índices 1-based
                    face.Add(new IfcPositiveInteger(tri[1] + 1));
                    face.Add(new IfcPositiveInteger(tri[2] + 1));
                }
            });

            var context = model.Instances.OfType<IfcGeometricRepresentationContext>().First();

            var shapeRep = model.Instances.New<IfcShapeRepresentation>(sr =>
            {
                sr.ContextOfItems = context;
                sr.RepresentationIdentifier = "Body";
                sr.RepresentationType = "Tessellation";
                sr.Items.Add(faceSet);
            });

            return model.Instances.New<IfcProductDefinitionShape>(pds =>
                pds.Representations.Add(shapeRep));
        }

        // -----------------------------------------------------------------------
        // Propriedades: IfcPropertySet → IfcRelDefinesByProperties
        // -----------------------------------------------------------------------

        private static void AddPropertySets(
            IfcStore model,
            IfcElement element,
            Dictionary<string, Dictionary<string, string>> propertySets)
        {
            foreach (var (psetName, props) in propertySets)
            {
                var pset = model.Instances.New<IfcPropertySet>(ps =>
                {
                    ps.Name = psetName;
                    foreach (var (propName, propValue) in props)
                    {
                        ps.HasProperties.Add(
                            model.Instances.New<IfcPropertySingleValue>(pv =>
                            {
                                pv.Name = propName;
                                pv.NominalValue = new IfcLabel(propValue);
                            }));
                    }
                });

                model.Instances.New<IfcRelDefinesByProperties>(rel =>
                {
                    rel.RelatingPropertyDefinition = pset;
                    rel.RelatedObjects.Add(element);
                });
            }
        }

        // -----------------------------------------------------------------------
        // Helpers de placement
        // -----------------------------------------------------------------------

        private static IfcLocalPlacement CreatePlacement(IfcStore model)
        {
            return model.Instances.New<IfcLocalPlacement>(lp =>
            {
                lp.RelativePlacement = model.Instances.New<IfcAxis2Placement3D>(a =>
                {
                    a.Location = model.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(0, 0, 0));
                });
            });
        }
    }
}
