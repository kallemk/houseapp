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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes Program for WebApplicationFactory<Program> in the test project.
public partial class Program;
