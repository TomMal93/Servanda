using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Servanda.Infrastructure.Data.Search;

/// <summary>
/// Deterministyczna kolejność wyników wyszukiwania zgodna z kontraktem search.md.
/// Wagi BM25 pochodzą z ADR 0003; kolumna identyfikatora nie jest indeksowana.
/// </summary>
internal static class SearchQueries
{
    private const string ToolWeights = "0.0, 10.0, 6.0, 5.0, 3.0, 2.0";
    private const string PromptWeights = "0.0, 10.0, 6.0, 5.0, 4.0, 3.0, 2.0, 1.0";

    public static Task<IReadOnlyList<string>> RankToolsAsync(
        ServandaDbContext database,
        string matchQuery,
        string normalizedQuery,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        RankAsync(
            database,
            "tool_search",
            "tools",
            "name",
            ToolWeights,
            matchQuery,
            normalizedQuery,
            areaId,
            categoryIds,
            skip,
            take,
            cancellationToken);

    public static Task<int> CountToolsAsync(
        ServandaDbContext database,
        string matchQuery,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        CancellationToken cancellationToken) =>
        CountAsync(database, "tool_search", "tools", matchQuery, areaId, categoryIds, cancellationToken);

    public static Task<IReadOnlyList<string>> RankPromptsAsync(
        ServandaDbContext database,
        string matchQuery,
        string normalizedQuery,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        RankAsync(
            database,
            "prompt_search",
            "prompts",
            "title",
            PromptWeights,
            matchQuery,
            normalizedQuery,
            areaId,
            categoryIds,
            skip,
            take,
            cancellationToken);

    public static Task<int> CountPromptsAsync(
        ServandaDbContext database,
        string matchQuery,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        CancellationToken cancellationToken) =>
        CountAsync(database, "prompt_search", "prompts", matchQuery, areaId, categoryIds, cancellationToken);

    private static async Task<IReadOnlyList<string>> RankAsync(
        ServandaDbContext database,
        string indexTable,
        string entityTable,
        string titleColumn,
        string weights,
        string matchQuery,
        string normalizedQuery,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(database, cancellationToken);
        await using var command = connection.CreateCommand();
        var filter = BuildCategoryFilter(command, categoryIds);
        command.CommandText =
            $"""
            SELECT {indexTable}.entity_id
            FROM {indexTable}
            JOIN {entityTable} AS entity ON entity.id = {indexTable}.entity_id
            WHERE {indexTable} MATCH $match AND entity.area_id = $areaId{filter}
            ORDER BY
                CASE WHEN {indexTable}.{titleColumn} = $normalized THEN 0 ELSE 1 END,
                CASE WHEN {indexTable}.{titleColumn} LIKE $prefix THEN 0 ELSE 1 END,
                bm25({indexTable}, {weights}),
                entity.updated_at DESC,
                entity.id ASC
            LIMIT $take OFFSET $skip
            """;
        command.Parameters.AddWithValue("$match", matchQuery);
        command.Parameters.AddWithValue("$areaId", areaId);
        command.Parameters.AddWithValue("$normalized", normalizedQuery);
        command.Parameters.AddWithValue("$prefix", normalizedQuery + "%");
        command.Parameters.AddWithValue("$take", take);
        command.Parameters.AddWithValue("$skip", skip);

        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static async Task<int> CountAsync(
        ServandaDbContext database,
        string indexTable,
        string entityTable,
        string matchQuery,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(database, cancellationToken);
        await using var command = connection.CreateCommand();
        var filter = BuildCategoryFilter(command, categoryIds);
        command.CommandText =
            $"""
            SELECT COUNT(*)
            FROM {indexTable}
            JOIN {entityTable} AS entity ON entity.id = {indexTable}.entity_id
            WHERE {indexTable} MATCH $match AND entity.area_id = $areaId{filter}
            """;
        command.Parameters.AddWithValue("$match", matchQuery);
        command.Parameters.AddWithValue("$areaId", areaId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static string BuildCategoryFilter(SqliteCommand command, IReadOnlyList<string>? categoryIds)
    {
        if (categoryIds is null)
        {
            return string.Empty;
        }

        if (categoryIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        var names = new List<string>(categoryIds.Count);
        for (var index = 0; index < categoryIds.Count; index++)
        {
            var name = $"$category{index.ToString(CultureInfo.InvariantCulture)}";
            command.Parameters.AddWithValue(name, categoryIds[index]);
            names.Add(name);
        }

        return $" AND entity.category_id IN ({string.Join(", ", names)})";
    }

    private static async Task<SqliteConnection> OpenAsync(
        ServandaDbContext database,
        CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }
}
