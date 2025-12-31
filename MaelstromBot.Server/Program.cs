using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using StandardWebhooks;

var builder = WebApplication.CreateBuilder(args);

// Logging (console only for now)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Paths and DB
var dbPath = Path.Combine(app.Environment.ContentRootPath, "artifacts", "bot", "db", "maelstrombot.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var connString = $"Data Source={dbPath}";

// Initialize schema
using (var conn = new SqliteConnection(connString))
{
    conn.Execute("""
    CREATE TABLE IF NOT EXISTS jobs (
        id TEXT PRIMARY KEY,
        source_sha TEXT,
        github_delivery TEXT,
        openai_response_id TEXT,
        status TEXT,
        created_utc TEXT,
        updated_utc TEXT,
        payload TEXT
    );
    """);

    conn.Execute("""
    CREATE TABLE IF NOT EXISTS dedupe (
        source TEXT,
        key TEXT,
        created_utc TEXT,
        PRIMARY KEY (source, key)
    );
    """);

    conn.Execute("""
    CREATE TABLE IF NOT EXISTS api_keys (
        id TEXT PRIMARY KEY,
        role TEXT,
        hash TEXT
    );
    """);
}

// Helpers
bool VerifyGitHubSignature(byte[] body, string? sigHeader, string secret)
{
    if (string.IsNullOrWhiteSpace(sigHeader) || !sigHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        return false;
    var expectedHex = sigHeader.Substring("sha256=".Length);
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(body);
    var actualHex = Convert.ToHexString(hash).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.ASCII.GetBytes(actualHex),
        Encoding.ASCII.GetBytes(expectedHex.ToLowerInvariant()));
}

string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

bool HasApiKey(IDbConnection conn, string tokenHash)
    => conn.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM api_keys WHERE hash=@h", new { h = tokenHash }) > 0;

bool RequireApiKey(HttpContext ctx, IDbConnection conn)
{
    var auth = ctx.Request.Headers.Authorization.ToString();
    if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
    var token = auth.Substring("Bearer ".Length).Trim();
    if (string.IsNullOrWhiteSpace(token)) return false;
    var hash = HashToken(token);
    return HasApiKey(conn, hash);
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/webhooks/github", async (HttpContext ctx) =>
{
    var secret = Environment.GetEnvironmentVariable("GITHUB_WEBHOOK_SECRET");
    if (string.IsNullOrWhiteSpace(secret)) return Results.Problem("Missing GITHUB_WEBHOOK_SECRET", statusCode: 500);

    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var bodyBytes = ms.ToArray();
    var sig = ctx.Request.Headers["X-Hub-Signature-256"].ToString();
    if (!VerifyGitHubSignature(bodyBytes, sig, secret)) return Results.Unauthorized();

    var eventType = ctx.Request.Headers["X-GitHub-Event"].ToString();
    if (!string.Equals(eventType, "push", StringComparison.OrdinalIgnoreCase)) return Results.Ok();

    var delivery = ctx.Request.Headers["X-GitHub-Delivery"].ToString();
    using var conn = new SqliteConnection(connString);
    var deduped = conn.Execute("INSERT OR IGNORE INTO dedupe (source, key, created_utc) VALUES ('github', @k, @c)",
        new { k = delivery, c = DateTime.UtcNow.ToString("o") });
    if (deduped == 0) return Results.Ok(); // already processed

    var payload = Encoding.UTF8.GetString(bodyBytes);
    var doc = System.Text.Json.JsonDocument.Parse(payload);
    var sha = doc.RootElement.GetProperty("after").GetString();
    var jobId = Guid.NewGuid().ToString();

    conn.Execute("""
        INSERT INTO jobs (id, source_sha, github_delivery, status, created_utc, updated_utc, payload)
        VALUES (@i, @s, @g, 'submitted', @c, @c, @p);
    """, new { i = jobId, s = sha, g = delivery, c = DateTime.UtcNow.ToString("o"), p = payload });

    // NOTE: Background submission to OpenAI to be implemented (background: true). For now, just record submission.
    conn.Execute("UPDATE jobs SET status='queued' WHERE id=@i", new { i = jobId });

    return Results.Accepted($"/api/jobs/{jobId}", new { jobId });
});

app.MapPost("/webhooks/openai", async (HttpContext ctx) =>
{
    var secret = Environment.GetEnvironmentVariable("OPENAI_WEBHOOK_SECRET");
    if (string.IsNullOrWhiteSpace(secret)) return Results.Problem("Missing OPENAI_WEBHOOK_SECRET", statusCode: 500);

    using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();
    var webhook = new StandardWebhook(secret);
    try
    {
        webhook.Verify(body, ctx.Request.Headers);
    }
    catch
    {
        return Results.Unauthorized();
    }

    var doc = System.Text.Json.JsonDocument.Parse(body);
    var type = doc.RootElement.GetProperty("type").GetString();
    if (!string.Equals(type, "response.completed", StringComparison.OrdinalIgnoreCase)) return Results.Ok();

    var webhookId = ctx.Request.Headers["webhook-id"].ToString();
    using var conn = new SqliteConnection(connString);
    var deduped = conn.Execute("INSERT OR IGNORE INTO dedupe (source, key, created_utc) VALUES ('openai', @k, @c)",
        new { k = webhookId, c = DateTime.UtcNow.ToString("o") });
    if (deduped == 0) return Results.Ok();

    var data = doc.RootElement.GetProperty("data");
    var responseId = data.GetProperty("id").GetString();
    var metadata = data.GetProperty("metadata");
    var jobId = metadata.TryGetProperty("job_id", out var jv) ? jv.GetString() : null;
    if (string.IsNullOrWhiteSpace(jobId)) return Results.Ok();

    conn.Execute("UPDATE jobs SET status='completed', openai_response_id=@r, updated_utc=@u WHERE id=@i",
        new { r = responseId, u = DateTime.UtcNow.ToString("o"), i = jobId });

    return Results.Ok();
});

app.MapGet("/api/jobs", (HttpContext ctx) =>
{
    using var conn = new SqliteConnection(connString);
    if (!RequireApiKey(ctx, conn)) return Results.Unauthorized();
    var jobs = conn.Query("""
        SELECT id, source_sha, status, openai_response_id, created_utc, updated_utc
        FROM jobs
        ORDER BY updated_utc DESC
        LIMIT 50;
    """);
    return Results.Ok(jobs);
});

app.MapGet("/api/jobs/{id}", (HttpContext ctx, string id) =>
{
    using var conn = new SqliteConnection(connString);
    if (!RequireApiKey(ctx, conn)) return Results.Unauthorized();
    var job = conn.Query("""
        SELECT id, source_sha, status, openai_response_id, created_utc, updated_utc, payload
        FROM jobs
        WHERE id=@i;
    """, new { i = id }).FirstOrDefault();
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapGet("/api/status", (HttpContext ctx) =>
{
    using var conn = new SqliteConnection(connString);
    if (!RequireApiKey(ctx, conn)) return Results.Unauthorized();
    var queued = conn.QuerySingle<int>("SELECT COUNT(*) FROM jobs WHERE status IN ('submitted','queued','running')");
    return Results.Ok(new { status = "ok", queued });
});

app.Run();
