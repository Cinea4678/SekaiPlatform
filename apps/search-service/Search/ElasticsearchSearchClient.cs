using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Minimal Elasticsearch REST client for tenant-scoped language asset search.
/// </summary>
internal sealed class ElasticsearchSearchClient(HttpClient httpClient, IOptions<SearchIndexOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SearchIndexOptions options = options.Value;

    /// <summary>
    /// Searches shared source lines and the current tenant's translation lines.
    /// </summary>
    public async Task<SearchQueryResponse> SearchAsync(
        SearchQueryRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(
            $"{options.IndexName}/_search",
            JsonContent(BuildSearchBody(request)),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowElasticsearchErrorAsync(response, cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var hits = document.RootElement.GetProperty("hits");
        var total = ReadTotal(hits.GetProperty("total"));
        var items = hits
            .GetProperty("hits")
            .EnumerateArray()
            .Select(ReadHit)
            .ToArray();

        return new SearchQueryResponse(items, total, request.Page, request.PageSize);
    }

    /// <summary>
    /// Builds the Elasticsearch query used by the public search API.
    /// </summary>
    private static JsonObject BuildSearchBody(SearchQueryRequest request)
    {
        return new JsonObject
        {
            ["from"] = ((long)request.Page - 1) * request.PageSize,
            ["size"] = request.PageSize,
            ["track_total_hits"] = true,
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["must"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["multi_match"] = new JsonObject
                            {
                                ["query"] = request.Keyword,
                                ["type"] = "best_fields",
                                ["fields"] = new JsonArray(
                                    "text^3",
                                    "text.zh^2",
                                    "text.folded")
                            }
                        }
                    },
                    ["filter"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["bool"] = new JsonObject
                            {
                                ["should"] = new JsonArray
                                {
                                    new JsonObject
                                    {
                                        ["term"] = new JsonObject
                                        {
                                            ["asset_type"] = SearchIndexConstants.AssetTypeSource
                                        }
                                    },
                                    new JsonObject
                                    {
                                        ["bool"] = new JsonObject
                                        {
                                            ["filter"] = new JsonArray
                                            {
                                                new JsonObject
                                                {
                                                    ["term"] = new JsonObject
                                                    {
                                                        ["asset_type"] = SearchIndexConstants.AssetTypeTranslation
                                                    }
                                                },
                                                new JsonObject
                                                {
                                                    ["term"] = new JsonObject
                                                    {
                                                        ["tenant_id"] = request.TenantId
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                ["minimum_should_match"] = 1
                            }
                        }
                    }
                }
            },
            ["highlight"] = new JsonObject
            {
                ["pre_tags"] = new JsonArray("<mark>"),
                ["post_tags"] = new JsonArray("</mark>"),
                ["encoder"] = "html",
                ["require_field_match"] = false,
                ["fields"] = new JsonObject
                {
                    ["text"] = new JsonObject(),
                    ["text.zh"] = new JsonObject(),
                    ["text.folded"] = new JsonObject()
                }
            },
            ["sort"] = new JsonArray
            {
                new JsonObject
                {
                    ["_script"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["order"] = "desc",
                        ["script"] = new JsonObject
                        {
                            ["lang"] = "painless",
                            ["source"] = "if (doc['asset_type'].value == 'translation') { return 1; } for (def tenantId : doc['translated_tenant_ids']) { if (tenantId == params.tenant_id) { return 1; } } return 0;",
                            ["params"] = new JsonObject
                            {
                                ["tenant_id"] = request.TenantId
                            }
                        }
                    }
                },
                new JsonObject { ["_score"] = new JsonObject { ["order"] = "desc" } },
                new JsonObject { ["story_id"] = new JsonObject { ["order"] = "asc" } },
                new JsonObject { ["line_no"] = new JsonObject { ["order"] = "asc" } },
                new JsonObject { ["asset_type"] = new JsonObject { ["order"] = "asc" } },
                new JsonObject { ["source_line_id"] = new JsonObject { ["order"] = "asc" } },
                new JsonObject
                {
                    ["translation_version_id"] = new JsonObject
                    {
                        ["order"] = "asc",
                        ["missing"] = "_first"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Converts one Elasticsearch hit into the public search hit shape.
    /// </summary>
    private static SearchQueryHit ReadHit(JsonElement hit)
    {
        var source = hit.GetProperty("_source");
        var text = source.GetRequiredString("text");
        return new SearchQueryHit
        {
            AssetType = source.GetRequiredString("asset_type"),
            Text = text,
            HighlightText = ReadHighlight(hit) ?? text,
            Speaker = source.GetNullableString("speaker"),
            LineNo = source.GetProperty("line_no").GetInt32(),
            StoryId = source.GetProperty("story_id").GetInt64(),
            StoryTitle = source.GetRequiredString("story_title"),
            StoryType = source.GetRequiredString("story_type"),
            StoryGroupId = source.GetNullableInt64("story_group_id"),
            StoryGroupTitle = source.GetNullableString("story_group_title"),
            SourceLineId = source.GetProperty("source_line_id").GetInt64(),
            TranslationLineId = ReadTranslationLineId(hit),
            TranslationVersionId = source.GetNullableInt64("translation_version_id")
        };
    }

    /// <summary>
    /// Reads the translation line identifier from the stable Elasticsearch document id.
    /// </summary>
    private static long? ReadTranslationLineId(JsonElement hit)
    {
        if (!hit.TryGetProperty("_id", out var id)
            || id.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = id.GetString();
        return value is not null
            && value.StartsWith("translation:", StringComparison.Ordinal)
            && long.TryParse(value["translation:".Length..], out var translationLineId)
                ? translationLineId
                : null;
    }

    /// <summary>
    /// Reads the first text highlight returned by Elasticsearch.
    /// </summary>
    private static string? ReadHighlight(JsonElement hit)
    {
        if (!hit.TryGetProperty("highlight", out var highlight))
        {
            return null;
        }

        foreach (var field in new[] { "text", "text.zh", "text.folded" })
        {
            if (highlight.TryGetProperty(field, out var values)
                && values.ValueKind == JsonValueKind.Array
                && values.GetArrayLength() > 0)
            {
                return values[0].GetString();
            }
        }

        return null;
    }

    private static long ReadTotal(JsonElement total)
    {
        return total.ValueKind == JsonValueKind.Object
            ? total.GetProperty("value").GetInt64()
            : total.GetInt64();
    }

    private static StringContent JsonContent(JsonNode node)
    {
        return new StringContent(node.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task ThrowElasticsearchErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Elasticsearch search request failed with {(int)response.StatusCode}: {body}");
    }
}

/// <summary>
/// Provides typed JSON helpers for Elasticsearch source documents.
/// </summary>
file static class SearchJsonElementExtensions
{
    /// <summary>
    /// Reads a required string property.
    /// </summary>
    public static string GetRequiredString(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            throw new JsonException($"Missing required Elasticsearch field '{propertyName}'.");
        }

        return value.GetString()
            ?? throw new JsonException($"Elasticsearch field '{propertyName}' must be a string.");
    }

    /// <summary>
    /// Reads an optional string property.
    /// </summary>
    public static string? GetNullableString(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// Reads an optional 64-bit integer property.
    /// </summary>
    public static long? GetNullableInt64(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt64()
            : null;
    }
}
