using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors();

var storeDir = Path.Combine(app.Environment.ContentRootPath, "SharedConfigs");
if (!Directory.Exists(storeDir))
    Directory.CreateDirectory(storeDir);

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

// POST /api/configs - upload a shared config
app.MapPost("/api/configs", async ([FromBody] UploadRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req?.GameName) || string.IsNullOrWhiteSpace(req?.ConfigName) || string.IsNullOrWhiteSpace(req?.JsonContent))
    {
        return Results.BadRequest("GameName, ConfigName and JsonContent are required.");
    }
    var id = Guid.NewGuid().ToString("N")[..12];
    var entry = new SharedConfigEntry
    {
        Id = id,
        GameName = req.GameName.Trim(),
        ConfigName = req.ConfigName.Trim(),
        JsonContent = req.JsonContent,
        CreatedAt = DateTime.UtcNow
    };
    var path = Path.Combine(storeDir, id + ".json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(entry, jsonOptions));
    return Results.Created($"/api/configs/{id}", new { id, entry.GameName, entry.ConfigName, entry.CreatedAt });
});

// GET /api/configs?search= - search shared configs
app.MapGet("/api/configs", (string? search) =>
{
    var files = Directory.GetFiles(storeDir, "*.json");
    var list = new List<SharedConfigEntry>();
    foreach (var f in files)
    {
        try
        {
            var json = File.ReadAllText(f);
            var entry = JsonSerializer.Deserialize<SharedConfigEntry>(json, jsonOptions);
            if (entry == null) continue;
            var match = string.IsNullOrWhiteSpace(search) ||
                entry.GameName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.ConfigName.Contains(search, StringComparison.OrdinalIgnoreCase);
            if (match)
            {
                list.Add(entry);
            }
        }
        catch { /* skip invalid */ }
    }
    var ordered = list.OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.GameName, x.ConfigName, x.CreatedAt });
    return Results.Ok(ordered);
});

// GET /api/configs/{id} - get full config including json
app.MapGet("/api/configs/{id}", (string id) =>
{
    if (string.IsNullOrWhiteSpace(id)) return Results.NotFound();
    var path = Path.Combine(storeDir, id + ".json");
    if (!File.Exists(path)) return Results.NotFound();
    var json = File.ReadAllText(path);
    var entry = JsonSerializer.Deserialize<SharedConfigEntry>(json, jsonOptions);
    return entry != null ? Results.Ok(entry) : Results.NotFound();
});

app.Run();

file class UploadRequest
{
    public string GameName { get; set; } = "";
    public string ConfigName { get; set; } = "";
    public string JsonContent { get; set; } = "";
}

file class SharedConfigEntry
{
    public string Id { get; set; } = "";
    public string GameName { get; set; } = "";
    public string ConfigName { get; set; } = "";
    public string JsonContent { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
