using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Schema;

public class SchemaManager
{
    private static readonly HttpClient HttpClient = new();
    private readonly ConcurrentDictionary<string, XmlSchema> _schemaCache = new();

    /// <summary>
    /// Gets an XML schema from the cache or downloads it if not cached.
    /// </summary>
    /// <param name="schemaUrl">The URL of the schema.</param>
    /// <returns>The XmlSchema object.</returns>
    public async Task<XmlSchema> GetSchemaAsync(string schemaUrl)
    {
        // Check cache first
        if (_schemaCache.TryGetValue(schemaUrl, out var cachedSchema))
            return cachedSchema;

        // Download and parse schema
        var schema = await DownloadAndParseSchemaAsync(schemaUrl);

        // Add to cache
        _schemaCache[schemaUrl] = schema;

        return schema;
    }

    /// <summary>
    /// Downloads and parses the XML schema.
    /// </summary>
    /// <param name="schemaUrl">The URL of the schema.</param>
    /// <returns>The XmlSchema object.</returns>
    private async Task<XmlSchema> DownloadAndParseSchemaAsync(string schemaUrl)
    {
        try
        {
            var schemaContent = await HttpClient.GetStringAsync(schemaUrl);
            using var stringReader = new StringReader(schemaContent);
            return XmlSchema.Read(stringReader, (sender, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    throw new InvalidOperationException($"Schema error: {e.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load schema from {schemaUrl}: {ex.Message}", ex);
        }
    }
}
