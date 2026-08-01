using HouseApp.Api.Data;
using HouseApp.Api.Data.Seed;
using HouseApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHouseAppData(builder.Configuration);
builder.Services.AddHouseAppBlobStorage(builder.Configuration);

// The Testing environment (WebApplicationFactory in HouseApp.Api.Tests) swaps AppDbContext/IBlobStorageService
// for in-memory fakes and never restarts, so persisting Data Protection keys to Blob Storage is unnecessary there.
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddDataProtection();
}
else
{
    builder.Services.AddHouseAppDataProtection();
}

builder.Services.AddHouseAppCookieAuth();
builder.Services.AddHouseAppGoogleAuth();
builder.Services.AddHouseAppGoogleDrive();
builder.Services.AddHouseAppFeedback();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.EnvironmentName != "Testing")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (app.Environment.IsDevelopment())
    {
        // Local convenience only — in Azure, containers/partition keys are provisioned by Bicep.
        await db.Database.EnsureCreatedAsync();
    }

    // There is deliberately no data migration step here. ProjectMigrator used to run on every
    // startup, copying renovationEntries into projects; it resurrected every project you had
    // deleted, because "copy the entries whose id isn't in `projects`" can't tell "never copied"
    // from "copied, then deliberately deleted". A one-shot migration must not run on a schedule —
    // if one is ever needed again, it has to record that it ran rather than infer it from the data.

    // Runs on every startup, in every environment including production — DbSeeder is idempotent
    // (skips accounts that already exist), and this is the only place the 2 admin accounts get created.
    await DbSeeder.SeedAsync(db, app.Configuration, app.Logger);
    await PropertyComponentSeeder.SeedAsync(db, app.Logger);
}

app.UseHttpsRedirection();

// The built SPA is copied into wwwroot at publish time (see ci-cd.yml), so the API serves the
// frontend itself. That's what makes the session cookie same-origin now — not a proxy in front of
// it, which is what the Static Web App used to provide.
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Only when there is actually a SPA to serve. Local `dotnet run` has no wwwroot (Vite serves the
// frontend on :5173 and proxies /api here), and the test host boots this same Program — without the
// guard both would start answering unmatched routes with a missing file.
var indexHtml = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
if (File.Exists(indexHtml))
{
    // Client-side routes have to survive a refresh: /properties/<id>/documents is not a file, so
    // without this the server 404s a URL the app considers perfectly valid.
    //
    // The two exclusions matter as much as the rewrite, and are exactly what staticwebapp.config.json
    // spelled out before this replaced it:
    //  - /api/* must 404 rather than return index.html. Handing HTML back from an API path turns a
    //    plain "no such endpoint" into a JSON parse error somewhere far away.
    //  - anything that looks like a file must 404 too. A stale /assets/index-abc123.js should fail
    //    as a missing script, not as HTML served under a .js URL — which surfaces as a baffling
    //    MIME-type error instead of an obvious 404.
    app.MapFallback(async context =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api") || Path.HasExtension(path.Value))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexHtml);
    });
}

app.Run();

// Exposes Program for WebApplicationFactory<Program> in the test project.
public partial class Program;
