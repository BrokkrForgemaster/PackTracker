using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var settingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "HouseWolf",
    "PackTracker",
    "user_settings.json");

if (!File.Exists(settingsPath))
{
    Console.Error.WriteLine($"Settings file not found: {settingsPath}");
    return 1;
}

using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
var appSettings = document.RootElement.GetProperty("AppSettings");
var apiBaseUrl = appSettings.GetProperty("ApiBaseUrl").GetString()?.TrimEnd('/');
var protectedJwt = appSettings.GetProperty("JwtToken").GetString();

if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(protectedJwt))
{
    Console.Error.WriteLine("ApiBaseUrl or JwtToken missing from user settings.");
    return 2;
}

string jwt;
try
{
    var encryptedBytes = Convert.FromBase64String(protectedJwt);
    var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
    jwt = Encoding.UTF8.GetString(plainBytes);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to unprotect JWT: {ex.Message}");
    return 3;
}

using var client = new HttpClient
{
    BaseAddress = new Uri($"{apiBaseUrl}/")
};

client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

var response = await client.GetAsync("api/v1/requests");
if (!response.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"API request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
    return 4;
}

var payload = await response.Content.ReadAsStringAsync();
using var requestsDoc = JsonDocument.Parse(payload);

foreach (var request in requestsDoc.RootElement.EnumerateArray())
{
    var title = request.GetProperty("title").GetString();
    if (title is null)
        continue;

    var status = request.GetProperty("status").GetString();
    var isPinned = request.GetProperty("isPinned").GetBoolean();
    var createdAt = request.GetProperty("createdAt").GetDateTimeOffset();
    var createdBy = request.TryGetProperty("createdByDisplayName", out var createdByProperty)
        ? createdByProperty.GetString()
        : null;
    var assignedTo = request.TryGetProperty("assignedToUsername", out var assignedProperty)
        ? assignedProperty.GetString()
        : null;
    var claimCount = request.TryGetProperty("claimCount", out var claimCountProperty)
        ? claimCountProperty.GetInt32()
        : 0;
    var maxClaims = request.TryGetProperty("maxClaims", out var maxClaimsProperty)
        ? maxClaimsProperty.GetInt32()
        : 0;

    Console.WriteLine($"{title} | Status={status} | Pinned={isPinned} | CreatedAt={createdAt:O} | CreatedBy={createdBy} | AssignedTo={assignedTo} | Claims={claimCount}/{maxClaims}");
}

return 0;
