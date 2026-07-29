using HouseApp.Api.Data;
using HouseApp.Api.Data.Migration;
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

    // Copies the old renovation log into `projects`. Idempotent and resumable (it copies only the
    // entries not already there), so running it on every startup is safe.
    //
    // It's sequenced before the seeders because that's the safe habit — migrate real data, then
    // fill gaps with defaults — but nothing here depends on it today: this migrator and
    // PropertyComponentSeeder write to different containers. Any future seeder that writes to
    // `projects` would make the order load-bearing, since a "skip if non-empty" guard satisfied by
    // seed data would strand the real data in the old container.
    await ProjectMigrator.MigrateAsync(db, app.Logger);

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
