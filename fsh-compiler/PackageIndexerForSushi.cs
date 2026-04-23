using System.Xml;
using System.Text.Json;

namespace Hl7.FhirShorthand.Compiler;

/// <summary>
/// The Sushi Package handler only requires the following properties:
///   resourceType, id, name, URL, version
/// And can be read from XML or json
/// This indexer directly reads the file in the fastest way - json or xml direct
/// </summary>
public class SushiPackageIndexer
{
    private static XmlReaderSettings _xmlSettings = new XmlReaderSettings
    {
        IgnoreComments = true,
        IgnoreWhitespace = true,
        IgnoreProcessingInstructions = true,
        DtdProcessing = DtdProcessing.Prohibit,
        CloseInput = false
    };

    public static ResourceSummaryDetails? ExtractIndexDetailsFromXml(FileInfo fi)
    {
        if (fi.Extension != ".xml")
            return null;

        ResourceSummaryDetails details = new ResourceSummaryDetails
        {
            FileName = fi.Name
        };

        // Open the stream, then use an XmlReader to read the properties
        int propCount = 0;
        var stream = fi.OpenRead();
        using (stream)
        {
            using var reader = XmlReader.Create(stream, _xmlSettings);
            while (reader.Read() && propCount < 4)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Depth == 0 && string.IsNullOrEmpty(details.ResourceType))
                {
                    details.ResourceType = reader.LocalName;
                    continue;
                }

                if (reader.Depth != 1)
                {
                    continue;
                }

                var value = reader.GetAttribute("value");
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "id":
                        details.Id ??= value;
                        propCount++;
                        break;
                    case "name":
                        details.Name ??= value;
                        propCount++;
                        break;
                    case "url":
                        details.Url ??= value;
                        propCount++;
                        break;
                    case "version":
                        details.Version ??= value;
                        propCount++;
                        break;
                }
            }
        }

        return details;
    }

    public static ResourceSummaryDetails? ExtractIndexDetailsFromJson(FileInfo fi)
    {
        if (fi.Extension != ".json")
            return null;

        ResourceSummaryDetails details = new ResourceSummaryDetails
        {
            FileName = fi.Name
        };

        // Open the stream, then use a Utf8JsonReader to read the properties
        int propCount = 0;
        var stream = fi.OpenRead();
        using (stream)
        {
            using var memory = new MemoryStream((int)stream.Length);
            stream.CopyTo(memory);
            var json = memory.ToArray();
            var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);
            string? propertyName = null;

            while (reader.Read() && propCount < 5) // 5 in json as the typename is a property, not the element name as in xml
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    propertyName = reader.GetString();
                    continue;
                }

                if (propertyName is null || reader.CurrentDepth != 1 || reader.TokenType != JsonTokenType.String)
                {
                    continue;
                }

                var value = reader.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    propertyName = null;
                    continue;
                }

                switch (propertyName)
                {
                    case "resourceType":
                        details.ResourceType ??= value;
                        propCount++;
                        break;
                    case "id":
                        details.Id ??= value;
                        propCount++;
                        break;
                    case "name":
                        details.Name ??= value;
                        propCount++;
                        break;
                    case "url":
                        details.Url ??= value;
                        propCount++;
                        break;
                    case "version":
                        details.Version ??= value;
                        propCount++;
                        break;
                }

                propertyName = null;
            }
        }

        return details;
    }
}

public class ResourceSummaryDetails
{
    public string? FileName { get; set; }

    public string? ResourceType { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Url { get; set; }

    public string? Version { get; set; }
}
