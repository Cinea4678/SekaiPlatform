using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Minimal Elasticsearch REST client for language asset index management and bulk writes.
/// </summary>
internal sealed class ElasticsearchIndexClient(HttpClient httpClient, IOptions<SearchIndexOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SearchIndexOptions options = options.Value;

    /// <summary>
    /// Ensures the configured index exists with the language asset mapping.
    /// </summary>
    public async Task EnsureIndexAsync(bool forceRecreate, CancellationToken cancellationToken)
    {
        if (forceRecreate)
        {
            await DeleteIndexIfExistsAsync(cancellationToken);
        }

        using var head = await httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Head, options.IndexName),
            cancellationToken);
        if (head.IsSuccessStatusCode)
        {
            return;
        }

        if (head.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            await ThrowElasticsearchErrorAsync(head, cancellationToken);
        }

        using var create = await httpClient.PutAsync(
            options.IndexName,
            JsonContent(CreateIndexBody()),
            cancellationToken);
        if (!create.IsSuccessStatusCode)
        {
            await ThrowElasticsearchErrorAsync(create, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes existing documents matching a rebuild request scope and filters.
    /// </summary>
    public async Task DeleteDocumentsAsync(SearchIndexRebuildRequest request, string scope, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["query"] = BuildDeleteQuery(request, scope)
        };

        using var response = await httpClient.PostAsync(
            $"{options.IndexName}/_delete_by_query?refresh=true&conflicts=proceed",
            JsonContent(body),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowElasticsearchErrorAsync(response, cancellationToken);
        }
    }

    /// <summary>
    /// Indexes language asset documents using Elasticsearch bulk API.
    /// </summary>
    public async Task<int> BulkIndexAsync(IReadOnlyList<SearchIndexDocument> documents, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return 0;
        }

        var indexed = 0;
        var batchSize = Math.Max(1, options.BulkBatchSize);
        foreach (var batch in documents.Chunk(batchSize))
        {
            var content = BuildBulkContent(batch);
            using var response = await httpClient.PostAsync("_bulk?refresh=true", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await ThrowElasticsearchErrorAsync(response, cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var result = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (result.RootElement.TryGetProperty("errors", out var errors) && errors.GetBoolean())
            {
                throw new InvalidOperationException("Elasticsearch bulk indexing reported item errors.");
            }

            indexed += batch.Length;
        }

        return indexed;
    }

    /// <summary>
    /// Deletes the configured index when it already exists.
    /// </summary>
    private async Task DeleteIndexIfExistsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(options.IndexName, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            await ThrowElasticsearchErrorAsync(response, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the index settings and mapping required by the language asset index.
    /// </summary>
    private static JsonObject CreateIndexBody()
    {
        return new JsonObject
        {
            ["settings"] = new JsonObject
            {
                ["analysis"] = new JsonObject
                {
                    ["analyzer"] = new JsonObject
                    {
                        ["ja_analyzer"] = new JsonObject
                        {
                            ["type"] = "custom",
                            ["tokenizer"] = "kuromoji_tokenizer",
                            ["filter"] = new JsonArray("kuromoji_baseform", "cjk_width", "lowercase", "icu_folding")
                        },
                        ["folded_analyzer"] = new JsonObject
                        {
                            ["type"] = "custom",
                            ["tokenizer"] = "standard",
                            ["filter"] = new JsonArray("cjk_width", "lowercase", "icu_folding")
                        }
                    }
                }
            },
            ["mappings"] = new JsonObject
            {
                ["dynamic"] = "strict",
                ["properties"] = new JsonObject
                {
                    ["asset_type"] = Keyword(),
                    ["tenant_id"] = Long(),
                    ["story_id"] = Long(),
                    ["story_type"] = Keyword(),
                    ["scenario_id"] = Keyword(),
                    ["story_title"] = TextWithLanguageFields(),
                    ["story_group_id"] = Long(),
                    ["story_group_title"] = TextWithLanguageFields(),
                    ["translation_version_id"] = Long(),
                    ["source_line_id"] = Long(),
                    ["line_no"] = new JsonObject { ["type"] = "integer" },
                    ["speaker"] = TextWithLanguageFields(),
                    ["text"] = TextWithLanguageFields()
                }
            }
        };
    }

    /// <summary>
    /// Builds the Elasticsearch delete-by-query predicate for a rebuild request.
    /// </summary>
    private static JsonObject BuildDeleteQuery(SearchIndexRebuildRequest request, string scope)
    {
        if (scope == SearchIndexConstants.ScopeAll
            && request.StoryIds is null
            && request.TenantId is null
            && request.TranslationVersionId is null)
        {
            return new JsonObject { ["match_all"] = new JsonObject() };
        }

        var filters = new JsonArray();
        if (scope is SearchIndexConstants.ScopeSource or SearchIndexConstants.ScopeTranslation)
        {
            filters.Add(new JsonObject
            {
                ["term"] = new JsonObject { ["asset_type"] = scope }
            });
        }

        if (request.StoryIds is { Length: > 0 })
        {
            filters.Add(new JsonObject
            {
                ["terms"] = new JsonObject { ["story_id"] = ToJsonArray(request.StoryIds) }
            });
        }

        if (request.TenantId is not null)
        {
            filters.Add(new JsonObject
            {
                ["term"] = new JsonObject { ["tenant_id"] = request.TenantId.Value }
            });
        }

        if (request.TranslationVersionId is not null)
        {
            filters.Add(new JsonObject
            {
                ["term"] = new JsonObject { ["translation_version_id"] = request.TranslationVersionId.Value }
            });
        }

        return new JsonObject
        {
            ["bool"] = new JsonObject { ["filter"] = filters }
        };
    }

    /// <summary>
    /// Builds newline-delimited JSON content for Elasticsearch bulk indexing.
    /// </summary>
    private StringContent BuildBulkContent(IReadOnlyList<SearchIndexDocument> documents)
    {
        var builder = new StringBuilder();
        foreach (var document in documents)
        {
            builder.Append(JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["index"] = new Dictionary<string, object?>
                    {
                        ["_index"] = options.IndexName,
                        ["_id"] = document.DocumentId
                    }
                },
                JsonOptions));
            builder.Append('\n');
            builder.Append(JsonSerializer.Serialize(document, JsonOptions));
            builder.Append('\n');
        }

        return new StringContent(builder.ToString(), Encoding.UTF8, "application/x-ndjson");
    }

    private static JsonObject Keyword() => new() { ["type"] = "keyword" };

    private static JsonObject Long() => new() { ["type"] = "long" };

    private static JsonObject TextWithLanguageFields()
    {
        return new JsonObject
        {
            ["type"] = "text",
            ["analyzer"] = "ja_analyzer",
            ["fields"] = new JsonObject
            {
                ["zh"] = new JsonObject
                {
                    ["type"] = "text",
                    ["analyzer"] = "smartcn"
                },
                ["folded"] = new JsonObject
                {
                    ["type"] = "text",
                    ["analyzer"] = "folded_analyzer"
                }
            }
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<long> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static StringContent JsonContent(JsonNode node)
    {
        return new StringContent(node.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task ThrowElasticsearchErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Elasticsearch request failed with {(int)response.StatusCode}: {body}");
    }
}
