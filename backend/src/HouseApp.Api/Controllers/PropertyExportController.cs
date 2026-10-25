using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Export;
using HouseApp.Api.Extensions;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseApp.Api.Controllers;

/// <summary>
/// "Give me all my data": one zip per property with data.json (complete, machine-readable),
/// data.xlsx (the same, for a person) and every Blob-stored document. Drive documents are listed by
/// link rather than copied — they're already in the owner's own Drive, and copying them would need a
/// live grant, so an expired connection would break the export.
/// </summary>
[ApiController]
[Route("api/properties/{propertyId}/export")]
[Authorize]
public class PropertyExportController(
    AppDbContext db,
    PropertyExportBuilder builder,
    IBlobStorageService blobStorage,
    ILogger<PropertyExportController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Keep å/ä/ö readable in the file rather than å — it's written to disk, never into HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [HttpGet]
    [EnableRateLimiting(RateLimitingExtensions.ExportPolicy)]
    public async Task<IActionResult> Export(string propertyId, CancellationToken cancellationToken)
    {
        // Ordinary access, not strict membership: the demo is exportable, and that reveals nothing
        // everyone can't already see.
        var userId = User.CurrentUserId();
        var property = await db.Properties.FindAsync([propertyId], cancellationToken);
        if (property is null || !await db.CanAccessPropertyAsync(propertyId, userId))
        {
            return NotFound();
        }

        var bundle = await builder.BuildAsync(property, userId);

        // Built in a temp file rather than streamed into the response: ZipArchive writes synchronously
        // on .NET 9 and Kestrel forbids synchronous IO, and a disk file keeps memory flat however many
        // documents there are. DeleteOnClose removes it once the response has been sent.
        var path = Path.Combine(Path.GetTempPath(), $"houseapp-export-{Guid.NewGuid()}.zip");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            await WriteArchiveAsync(stream, bundle, cancellationToken);
            stream.Position = 0;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }

        var fileName = $"hustracker-{Slug(property.Nickname)}-{DateTime.UtcNow:yyyy-MM-dd}.zip";
        return File(stream, "application/zip", fileName);
    }

    private async Task WriteArchiveAsync(Stream destination, PropertyExportBundle bundle, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        // Files first, so data.json and LÄSMIG.txt can say which ones couldn't be read. One lost blob
        // must not cost someone the rest of their data, so a failure is recorded rather than thrown.
        var missing = new HashSet<string>();
        foreach (var file in bundle.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream source;
            try
            {
                source = await blobStorage.OpenReadAsync(file.BlobPath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Export: could not read blob for document {DocumentId}.", file.DocumentId);
                missing.Add(file.DocumentId);
                continue;
            }

            await using (source)
            {
                // Most documents are PDFs and photos, already compressed — spending CPU on them gains nothing.
                await using var entry = archive.CreateEntry(file.ZipPath, CompressionLevel.NoCompression).Open();
                await source.CopyToAsync(entry, cancellationToken);
            }
        }

        var data = bundle.Data with
        {
            Documents = bundle.Data.Documents
                .Select(d => missing.Contains(d.Id) ? d with { FileMissing = true } : d)
                .ToList(),
        };

        await using (var json = archive.CreateEntry("data.json").Open())
        {
            await JsonSerializer.SerializeAsync(json, data, JsonOptions, cancellationToken);
        }

        using (var xlsx = new MemoryStream())
        {
            ExportWorkbook.Write(data, xlsx);
            xlsx.Position = 0;
            await using var entry = archive.CreateEntry("data.xlsx").Open();
            await xlsx.CopyToAsync(entry, cancellationToken);
        }

        await using (var readme = new StreamWriter(archive.CreateEntry("LÄSMIG.txt").Open(), new UTF8Encoding(true)))
        {
            await readme.WriteAsync(Readme(data));
        }
    }

    private static string Readme(PropertyExport data)
    {
        var text = new StringBuilder();
        text.AppendLine($"HusTracker – export av {data.Property.Nickname}");
        text.AppendLine($"Exporterad {data.ExportedAt:yyyy-MM-dd HH:mm} (UTC){(data.ExportedBy is { } by ? $" av {by}" : "")}.");
        text.AppendLine();
        text.AppendLine("Det här är all information som finns om bostaden i HusTracker.");
        text.AppendLine();
        text.AppendLine("data.json   Allt, i ett format som andra program kan läsa. Det är den fullständiga");
        text.AppendLine("            versionen – värden som arbetstyp och status står på engelska där.");
        text.AppendLine("data.xlsx   Samma innehåll som ett kalkylark, en flik per område.");
        text.AppendLine($"{PropertyExportBuilder.DocumentsFolder}/    Uppladdade dokument, sorterade som i appen: Allmänt, och en mapp per projekt.");

        var onDrive = data.Documents.Count(d => d.Storage == Models.DocumentStorageKind.Drive);
        if (onDrive > 0)
        {
            text.AppendLine();
            text.AppendLine($"{onDrive} dokument ligger i Google Drive och finns inte i den här filen – de ligger redan");
            text.AppendLine("i ditt eget Drive. Länkarna finns i data.xlsx (fliken Dokument) och i data.json.");
            if (data.Property.GoogleDriveFolderUrl is { } folder)
            {
                text.AppendLine($"Mappen: {folder}");
            }
        }

        var missing = data.Documents.Where(d => d.FileMissing).ToList();
        if (missing.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Följande filer kunde inte läsas och saknas i exporten:");
            foreach (var d in missing)
            {
                text.AppendLine($"  - {d.Title ?? d.FileName} ({d.Date:yyyy-MM-dd})");
            }
        }

        return text.ToString();
    }

    /// <summary>An ASCII file name, since Content-Disposition and every OS's download folder cope with that.</summary>
    private static string Slug(string name)
    {
        var slug = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            var mapped = ch switch
            {
                'å' or 'ä' => 'a',
                'ö' => 'o',
                'é' => 'e',
                _ => ch,
            };
            if (mapped is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(mapped);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var result = slug.ToString().Trim('-');
        return result.Length == 0 ? "bostad" : result;
    }
}
