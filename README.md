# Navisworks IFC Exporter

Plugin para **Autodesk Navisworks Manage** que exporta modelos para o formato **IFC 4**, preservando propriedades e geometria tessellada.

## Funcionalidades

- Exportação do modelo completo ou apenas da seleção atual
- Inclusão opcional de elementos ocultos
- Exportação de geometria tessellada (triângulos)
- Mapeamento automático de categorias Navisworks para tipos IFC 4
- Extração de propriedades e conjuntos de propriedades (Psets)
- Interface WPF com log de progresso em tempo real
- Campos configuráveis de autor e organização no cabeçalho IFC

## Requisitos

| Componente | Versão mínima |
|---|---|
| Autodesk Navisworks Manage | 2024 (testado no 2026) |
| .NET Framework | 4.7.2 |
| xBIM Essentials | 5.1.341 |

## Estrutura do projeto

```
NavisworksIfcExporter/
├── Plugin.cs                  # Ponto de entrada e registro do AddIn
├── NavisworksIfcExporter.csproj
├── Core/
│   ├── ExportService.cs       # Orquestra a exportação
│   ├── ModelTraverser.cs      # Percorre a árvore de elementos
│   ├── PropertyExtractor.cs   # Lê propriedades dos elementos
│   ├── GeometryExtractor.cs   # Extrai malha tessellada
│   ├── IfcWriter.cs           # Gera o arquivo IFC via xBIM
│   └── IfcTypeMapper.cs       # Mapeia categorias → tipos IFC
├── Models/
│   └── ElementData.cs         # DTO com dados de cada elemento
└── UI/
    ├── ExportWindow.xaml      # Interface WPF
    └── ExportWindow.xaml.cs
```

## Como compilar

1. Instale o **Navisworks Manage 2026** (ou ajuste o caminho no `.csproj`).
2. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/navisworks-ifc-exporter.git
   cd navisworks-ifc-exporter
   ```
3. Restaure os pacotes NuGet e compile:
   ```bash
   dotnet restore
   dotnet build -c Release
   ```
   O assembly `NavisworksIfcExporter.dll` será gerado em `bin\Release\net472\`.

> **Caminho do Navisworks:** o projeto usa a propriedade MSBuild `NavisworksDir` que aponta por padrão para  
> `C:\Program Files\Autodesk\Navisworks Manage 2026`.  
> Para uma versão diferente, passe `-p:NavisworksDir="<caminho>"` no build ou ajuste o `.csproj`.

## Como instalar

1. Copie `NavisworksIfcExporter.dll` para uma pasta de sua escolha, por exemplo:  
   `C:\ProgramData\Autodesk\Navisworks Manage 2026\Plugins\NavisworksIfcExporter\`

2. Crie o arquivo de manifesto `NavisworksIfcExporter.addin` na mesma pasta:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <NavisworksPlugin>
     <Plugin id="NavisworksIfcExporter.PHD">
       <Assembly>NavisworksIfcExporter.dll</Assembly>
     </Plugin>
   </NavisworksPlugin>
   ```

3. Inicie o Navisworks. O comando **Exportar IFC 4** aparecerá no menu **Exportar**.

## Dependências de terceiros

- [xBIM Essentials](https://github.com/xBimTeam/XbimEssentials) — licença CDDL 1.0

## Licença

MIT — veja [LICENSE](LICENSE).
