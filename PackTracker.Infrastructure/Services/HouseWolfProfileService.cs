using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Services;

public class HouseWolfProfileService : IHouseWolfProfileService
{
    private readonly ISettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HouseWolfProfileService> _logger;

    public HouseWolfProfileService(
        ISettingsService settingsService,
        IConfiguration configuration,
        ILogger<HouseWolfProfileService> logger)
    {
        _settingsService = settingsService;
        _configuration = configuration;
        _logger = logger;
    }

    private string? GetConnectionString()
    {
        // Prefer server-side IConfiguration (environment variables / appsettings)
        var host = _configuration["HouseWolf:Host"]
                   ?? _configuration["AppSettings:HousewolfApiBaseUrl"];
        var db = _configuration["HouseWolf:Database"]
                 ?? _configuration["AppSettings:databaseName"];
        var user = _configuration["HouseWolf:Username"]
                   ?? _configuration["AppSettings:databaseUsername"];
        var pass = _configuration["HouseWolf:Password"]
                   ?? _configuration["AppSettings:databasePassword"];

        // Fall back to file-based SettingsService (desktop client)
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(db)
            || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            var s = _settingsService.GetSettings();
            if (string.IsNullOrWhiteSpace(host)) host = s.HousewolfApiBaseUrl;
            if (string.IsNullOrWhiteSpace(db)) db = s.DatabaseName;
            if (string.IsNullOrWhiteSpace(user)) user = s.DatabaseUsername;
            if (string.IsNullOrWhiteSpace(pass)) pass = s.DatabasePassword;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("HouseWolf NeonDB host is not configured. Skipping profile sync.");
            return null;
        }

        return $"Host={host};Username={user};Password={pass};Database={db};SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    }

    internal static string BuildProfileLookupQuery(
        string tableName,
        IReadOnlyCollection<string> profileColumns,
        IReadOnlyCollection<string> userColumns)
    {
        var directDiscordColumn = FindFirstColumn(profileColumns, "discordId", "discord_id");
        if (directDiscordColumn is not null)
        {
            return $@"
                SELECT cp.*
                FROM {tableName} cp
                WHERE cp.""{directDiscordColumn}"" = @discordId
                LIMIT 1
                ";
        }

        var profileUserIdColumn = FindFirstColumn(profileColumns, "userId", "user_id");
        var userIdColumn = FindFirstColumn(userColumns, "id");
        var userDiscordColumn = FindFirstColumn(userColumns, "discordId", "discord_id");

        if (profileUserIdColumn is not null &&
            userIdColumn is not null &&
            userDiscordColumn is not null)
        {
            return $@"
                SELECT cp.*
                FROM {tableName} cp
                INNER JOIN ""User"" u ON u.""{userIdColumn}"" = cp.""{profileUserIdColumn}""
                WHERE u.""{userDiscordColumn}"" = @discordId
                LIMIT 1
                ";
        }

        var fallbackIdColumn = FindFirstColumn(profileColumns, "id");
        if (fallbackIdColumn is not null)
        {
            return $@"
                SELECT cp.*
                FROM {tableName} cp
                WHERE cp.""{fallbackIdColumn}"" = @discordId
                LIMIT 1
                ";
        }

        throw new InvalidOperationException(
            $"Could not determine a HouseWolf profile lookup path for {tableName}. " +
            $"Profile columns: {string.Join(", ", profileColumns)}. " +
            $"User columns: {string.Join(", ", userColumns)}");
    }

    internal static void MapProfileField(HouseWolfCharacterProfile profile, string fieldName, object? value)
    {
        var normalizedName = fieldName.ToLowerInvariant();

        switch (normalizedName)
        {
            case "id":
                profile.Id = value?.ToString();
                break;
            case "userid":
            case "user_id":
                profile.UserId = value?.ToString() ?? string.Empty;
                break;
            case "discordid":
            case "discord_id":
                profile.UserId = value?.ToString() ?? profile.UserId;
                break;
            case "charactername":
            case "character_name":
            case "displayname":
            case "display_name":
                profile.CharacterName = value?.ToString();
                break;
            case "division":
                profile.Division = value?.ToString();
                break;
            case "bio":
            case "biography":
            case "about":
            case "description":
                profile.Bio = value?.ToString();
                break;
            case "imageurl":
            case "image_url":
            case "image":
            case "avatar":
            case "profileimage":
            case "profile_image":
            case "portrait":
            case "portraiturl":
            case "portrait_url":
                profile.ImageUrl = value?.ToString();
                break;
            case "subdivision":
            case "sub_division":
                profile.SubDivision = value?.ToString();
                break;
        }
    }

    private static string? FindFirstColumn(IEnumerable<string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var match = columns.FirstOrDefault(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public async Task<HouseWolfCharacterProfile?> GetProfileByDiscordIdAsync(string discordId)
    {
        if (string.IsNullOrWhiteSpace(discordId)) return null;

        var connectionString = GetConnectionString();
        if (connectionString is null)
            return null;

        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            string[] possibleTableNames = { "\"CharacterProfile\"", "character_profile", "profiles", "users" };
            string? tableName = null;

            foreach (var name in possibleTableNames)
            {
                try
                {
                    using var testCmd = new NpgsqlCommand($"SELECT 1 FROM {name} LIMIT 1", conn);
                    await testCmd.ExecuteScalarAsync();
                    tableName = name;
                    break;
                }
                catch
                {
                    /* continue */
                }
            }

            if (tableName == null)
            {
                throw new InvalidOperationException("CharacterProfile table not found in HouseWolf database.");
            }

            // --- Column Discovery ---
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var colCmd = new NpgsqlCommand(
                       "SELECT column_name FROM information_schema.columns WHERE table_name = @table", conn))
            {
                colCmd.Parameters.AddWithValue("table", tableName.Replace("\"", ""));
                using var colReader = await colCmd.ExecuteReaderAsync();
                while (await colReader.ReadAsync())
                {
                    columns.Add(colReader.GetString(0));
                }
            }

            if (columns.Count == 0)
            {
                using var schemaCmd = new NpgsqlCommand($"SELECT * FROM {tableName} WHERE 1=0", conn);
                using var schemaReader = await schemaCmd.ExecuteReaderAsync();
                for (int i = 0; i < schemaReader.FieldCount; i++)
                    columns.Add(schemaReader.GetName(i));
            }

            _logger.LogInformation("HouseWolf {Table} columns: {Columns}",
                tableName, string.Join(", ", columns));

            var userColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var userColCmd = new NpgsqlCommand(
                    "SELECT column_name FROM information_schema.columns WHERE table_name IN ('User', 'user')",
                    conn);
                using var userColReader = await userColCmd.ExecuteReaderAsync();
                while (await userColReader.ReadAsync())
                {
                    userColumns.Add(userColReader.GetString(0));
                }
            }
            catch
            {
                // Some HouseWolf deployments may not expose a User table. Direct discord-id lookup will be used instead.
            }

            string query = BuildProfileLookupQuery(tableName, columns, userColumns);

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("discordId", discordId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var profile = new HouseWolfCharacterProfile();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                    MapProfileField(profile, reader.GetName(i), val);
                }

                var imgPreview = profile.ImageUrl == null
                    ? "(null)"
                    : profile.ImageUrl.Length > 60
                        ? profile.ImageUrl[..60] + $"... (len={profile.ImageUrl.Length})"
                        : profile.ImageUrl;
                _logger.LogInformation("Fetched HouseWolf profile: Name={Name}, ImageUrl={ImageUrl}",
                    profile.CharacterName, imgPreview);
                return profile;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching HouseWolf profile for Discord ID {DiscordId}", discordId);
            throw;
        }

        return null;
    }

    public async Task UpsertProfileAsync(HouseWolfCharacterProfile profile)
    {
        var connectionString = GetConnectionString();
        if (connectionString is null)
            return;

        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // --- Table Discovery ---
            string[] possibleTableNames = { "\"CharacterProfile\"", "character_profile", "profiles", "users" };
            string? tableName = null;
            foreach (var name in possibleTableNames)
            {
                try
                {
                    using var testCmd = new NpgsqlCommand($"SELECT 1 FROM {name} LIMIT 1", conn);
                    await testCmd.ExecuteScalarAsync();
                    tableName = name;
                    break;
                }
                catch
                {
                }
            }

            if (tableName == null) throw new InvalidOperationException("CharacterProfile table not found.");

            // --- Column Discovery ---
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var colCmd =
                   new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_name = @table",
                       conn))
            {
                colCmd.Parameters.AddWithValue("table", tableName.Replace("\"", ""));
                using var colReader = await colCmd.ExecuteReaderAsync();
                while (await colReader.ReadAsync()) columns.Add(colReader.GetString(0));
            }

            string? idCol = columns.FirstOrDefault(c => string.Equals(c, "id", StringComparison.OrdinalIgnoreCase));
            string? userIdCol = columns.FirstOrDefault(c =>
                string.Equals(c, "userId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "user_id", StringComparison.OrdinalIgnoreCase));
            string? nameCol = columns.FirstOrDefault(c =>
                string.Equals(c, "characterName", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "character_name", StringComparison.OrdinalIgnoreCase));
            string? bioCol = columns.FirstOrDefault(c => string.Equals(c, "bio", StringComparison.OrdinalIgnoreCase));
            string? imgCol = columns.FirstOrDefault(c =>
                string.Equals(c, "imageUrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "image_url", StringComparison.OrdinalIgnoreCase));
            string? divCol =
                columns.FirstOrDefault(c => string.Equals(c, "division", StringComparison.OrdinalIgnoreCase));
            string? subCol = columns.FirstOrDefault(c =>
                string.Equals(c, "subDivision", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "sub_division", StringComparison.OrdinalIgnoreCase));

            if (userIdCol == null) throw new InvalidOperationException($"Could not find userId column in {tableName}.");

            // Check if profile exists using discovered columns
            object? existingId = null;
            using (var checkCmd =
                   new NpgsqlCommand($"SELECT \"{userIdCol}\" FROM {tableName} WHERE \"{userIdCol}\" = @userId", conn))
            {
                checkCmd.Parameters.AddWithValue("userId", profile.UserId);
                existingId = await checkCmd.ExecuteScalarAsync();
            }

            if (existingId != null)
            {
                // Update
                var updateFields = new List<string>();
                if (nameCol != null) updateFields.Add($"\"{nameCol}\" = COALESCE(\"{nameCol}\", @name)");
                if (bioCol != null) updateFields.Add($"\"{bioCol}\" = COALESCE(\"{bioCol}\", @bio)");
                if (imgCol != null) updateFields.Add($"\"{imgCol}\" = COALESCE(\"{imgCol}\", @img)");

                string updatedAtCol = columns.FirstOrDefault(c =>
                    string.Equals(c, "updatedAt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c, "updated_at", StringComparison.OrdinalIgnoreCase)) ?? "";
                if (!string.IsNullOrEmpty(updatedAtCol)) updateFields.Add($"\"{updatedAtCol}\" = @now");

                if (updateFields.Count > 0)
                {
                    using var updateCmd =
                        new NpgsqlCommand(
                            $"UPDATE {tableName} SET {string.Join(", ", updateFields)} WHERE \"{userIdCol}\" = @userId",
                            conn);
                    updateCmd.Parameters.AddWithValue("userId", profile.UserId);
                    if (nameCol != null)
                        updateCmd.Parameters.AddWithValue("name", (object?)profile.CharacterName ?? DBNull.Value);
                    if (bioCol != null) updateCmd.Parameters.AddWithValue("bio", (object?)profile.Bio ?? DBNull.Value);
                    if (imgCol != null)
                        updateCmd.Parameters.AddWithValue("img", (object?)profile.ImageUrl ?? DBNull.Value);
                    if (!string.IsNullOrEmpty(updatedAtCol)) updateCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            }
            else
            {
                // Insert
                var insertCols = new List<string> { $"\"{userIdCol}\"" };
                var insertVals = new List<string> { "@userId" };

                if (idCol != null)
                {
                    // Check if 'id' has a default value
                    bool hasDefault = false;
                    string dataType = "";
                    using (var defaultCmd = new NpgsqlCommand(
                               "SELECT column_default, data_type FROM information_schema.columns WHERE table_name = @table AND column_name = @col",
                               conn))
                    {
                        defaultCmd.Parameters.AddWithValue("table", tableName.Replace("\"", ""));
                        defaultCmd.Parameters.AddWithValue("col", idCol);
                        using var r = await defaultCmd.ExecuteReaderAsync();
                        if (await r.ReadAsync())
                        {
                            hasDefault = !await r.IsDBNullAsync(0);
                            dataType = r.GetString(1).ToLowerInvariant();
                        }
                    }

                    if (!hasDefault)
                    {
                        insertCols.Add($"\"{idCol}\"");
                        if (dataType.Contains("int"))
                        {
                            insertVals.Add("@nextIdInt");
                        }
                        else
                        {
                            insertVals.Add("@nextIdStr");
                        }
                    }
                }

                if (nameCol != null)
                {
                    insertCols.Add($"\"{nameCol}\"");
                    insertVals.Add("@name");
                }

                if (bioCol != null)
                {
                    insertCols.Add($"\"{bioCol}\"");
                    insertVals.Add("@bio");
                }

                if (imgCol != null)
                {
                    insertCols.Add($"\"{imgCol}\"");
                    insertVals.Add("@img");
                }

                if (divCol != null)
                {
                    insertCols.Add($"\"{divCol}\"");
                    insertVals.Add("@div");
                }

                if (subCol != null)
                {
                    insertCols.Add($"\"{subCol}\"");
                    insertVals.Add("@sub");
                }

                string createdAtCol = columns.FirstOrDefault(c =>
                    string.Equals(c, "createdAt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c, "created_at", StringComparison.OrdinalIgnoreCase)) ?? "";
                if (!string.IsNullOrEmpty(createdAtCol))
                {
                    insertCols.Add($"\"{createdAtCol}\"");
                    insertVals.Add("@now");
                }

                string updatedAtCol = columns.FirstOrDefault(c =>
                    string.Equals(c, "updatedAt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c, "updated_at", StringComparison.OrdinalIgnoreCase)) ?? "";
                if (!string.IsNullOrEmpty(updatedAtCol))
                {
                    insertCols.Add($"\"{updatedAtCol}\"");
                    insertVals.Add("@now");
                }

                using var insertCmd =
                    new NpgsqlCommand(
                        $"INSERT INTO {tableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)})",
                        conn);
                insertCmd.Parameters.AddWithValue("userId", profile.UserId);

                if (insertVals.Contains("@nextIdStr"))
                {
                    insertCmd.Parameters.AddWithValue("nextIdStr", Guid.NewGuid().ToString("n"));
                }
                else if (insertVals.Contains("@nextIdInt"))
                {
                    using var maxCmd = new NpgsqlCommand($"SELECT MAX(\"{idCol}\") FROM {tableName}", conn);
                    var maxVal = await maxCmd.ExecuteScalarAsync();
                    int nextId = (maxVal == null || maxVal == DBNull.Value) ? 1 : Convert.ToInt32(maxVal, System.Globalization.CultureInfo.InvariantCulture) + 1;
                    insertCmd.Parameters.AddWithValue("nextIdInt", nextId);
                }

                if (nameCol != null)
                    insertCmd.Parameters.AddWithValue("name", (object?)profile.CharacterName ?? DBNull.Value);
                if (bioCol != null) insertCmd.Parameters.AddWithValue("bio", (object?)profile.Bio ?? DBNull.Value);
                if (imgCol != null) insertCmd.Parameters.AddWithValue("img", (object?)profile.ImageUrl ?? DBNull.Value);
                if (divCol != null) insertCmd.Parameters.AddWithValue("div", (object?)profile.Division ?? "Unassigned");
                if (subCol != null)
                    insertCmd.Parameters.AddWithValue("sub", (object?)profile.SubDivision ?? DBNull.Value);
                if (!string.IsNullOrEmpty(createdAtCol) || !string.IsNullOrEmpty(updatedAtCol))
                    insertCmd.Parameters.AddWithValue("now", DateTime.UtcNow);

                await insertCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting HouseWolf profile for UserId {UserId}", profile.UserId);
            throw;
        }
    }
}
