using ClosedXML.Excel;

namespace HouseApp.Api.Export;

/// <summary>
/// data.xlsx: the same content as data.json, laid out for a person rather than a program — Swedish
/// headers and labels, real dates and numbers so sums and filters work.
///
/// Column widths are fixed rather than <c>AdjustToContents()</c>, which measures text with font
/// metrics — the kind of thing that works on a laptop and fails on App Service Linux.
/// </summary>
public static class ExportWorkbook
{
    private enum Kind { Text, Money, Number, Date, YesNo }

    private sealed record Column(string Header, double Width, Kind Kind = Kind.Text);

    private const string MoneyFormat = "#,##0 \"kr\"";
    private const string DateFormat = "yyyy-mm-dd";

    public static void Write(PropertyExport export, Stream destination)
    {
        using var workbook = new XLWorkbook();
        var p = export.Property;
        var projectNames = export.Projects.ToDictionary(x => x.Id, x => x.Name);

        AddKeyValueSheet(workbook, "Fastighet",
        [
            ("Namn", p.Nickname, Kind.Text),
            ("Adress", p.Address, Kind.Text),
            ("Adressrad 2", p.Address2, Kind.Text),
            ("Postnummer", p.PostalCode, Kind.Text),
            ("Ort", p.City, Kind.Text),
            ("Land", p.Country, Kind.Text),
            ("Fastighetsbeteckning", p.PropertyDesignation, Kind.Text),
            ("Typ", p.Type is { } type ? Labels.PropertyTypes[type] : null, Kind.Text),
            ("Byggår", p.YearBuilt, Kind.Number),
            ("Latitud", p.Latitude, Kind.Number),
            ("Longitud", p.Longitude, Kind.Number),
            ("Köpdatum", p.PurchaseDate, Kind.Date),
            ("Köpeskilling", p.PurchasePrice, Kind.Money),
            ("Medlemmar", string.Join(", ", p.Members), Kind.Text),
            ("Google Drive-mapp", p.GoogleDriveFolderUrl, Kind.Text),
            ("Exporterad", export.ExportedAt, Kind.Date),
            ("Exporterad av", export.ExportedBy, Kind.Text),
        ]);

        AddTableSheet(workbook, "Projekt",
            [
                new("Namn", 30), new("Komponent", 18), new("Typ", 14), new("Status", 12),
                new("Prioritet", 10), new("Brådskande", 11, Kind.YesNo), new("Planerad start", 13, Kind.Date),
                new("Faktisk start", 13, Kind.Date), new("Slutförd", 12, Kind.Date), new("Uppskattad kostnad", 16, Kind.Money),
                new("Faktisk kostnad", 16, Kind.Money), new("Beräknad värdeökning", 18, Kind.Money),
                new("Förväntad livslängd (år)", 12, Kind.Number), new("Energibesparing (%)", 12, Kind.Number),
                new("Räknas mot underhållsplanen", 14, Kind.YesNo), new("Hantverkare", 22), new("Telefon", 14),
                new("E-post", 22), new("Webbplats", 22), new("Offererat pris", 14, Kind.Money),
                new("Offertdatum", 12, Kind.Date), new("Beskrivning", 40), new("Anteckningar", 40),
                new("Skapad av", 16), new("Id", 38),
            ],
            export.Projects.Select(x => new object?[]
            {
                x.Name, x.ComponentName, Labels.WorkTypes[x.WorkType], Labels.Statuses[x.Status],
                Labels.Priorities[x.Priority], x.IsUrgent, x.PlannedStartDate, x.ActualStartDate, x.CompletedDate,
                x.EstimatedCost, x.ActualCost, x.EstimatedValueIncrease, x.ExpectedLifespanYears,
                x.EnergyEfficiencyGainPercent, !x.ExcludeFromMaintenanceSchedule, x.Contractor?.Name,
                x.Contractor?.Phone, x.Contractor?.Email, x.Contractor?.Website, x.Contractor?.QuotedPrice,
                x.Contractor?.QuotedDate, x.Description, x.Notes, x.CreatedBy, x.Id,
            }));

        AddTableSheet(workbook, "Kostnader",
            [
                new("Projekt", 30), new("Datum", 12, Kind.Date), new("Typ", 14), new("Beskrivning", 40),
                new("Belopp", 14, Kind.Money), new("Budgeterad", 11, Kind.YesNo), new("Projekt-id", 38),
            ],
            export.Projects.SelectMany(x => x.Costs.Select(c => new object?[]
            {
                x.Name, c.DateIncurred, Labels.CostTypes[c.Type], c.Description, c.Amount, c.IsBudgeted, x.Id,
            })));

        AddTableSheet(workbook, "Milstolpar",
            [
                new("Projekt", 30), new("Beskrivning", 40), new("Planerad", 12, Kind.Date),
                new("Klar", 12, Kind.Date), new("Projekt-id", 38),
            ],
            export.Projects.SelectMany(x => x.Milestones.Select(m => new object?[]
            {
                x.Name, m.Description, m.PlannedDate, m.CompletedDate, x.Id,
            })));

        AddTableSheet(workbook, "Värderingar",
            [
                new("Datum", 12, Kind.Date), new("Värde", 16, Kind.Money), new("Källa", 24),
                new("Anteckningar", 40), new("Skapad av", 16),
            ],
            export.Valuations.Select(v => new object?[] { v.Date, v.Value, v.Source, v.Notes, v.CreatedBy }));

        AddTableSheet(workbook, "Budget",
            [
                new("År", 8, Kind.Number), new("Underhåll", 14, Kind.Money), new("Renovering", 14, Kind.Money),
                new("Nyinvestering", 14, Kind.Money), new("Inköp", 14, Kind.Money),
            ],
            export.Budgets.Select(b => new object?[]
            {
                b.Year, b.MaintenanceBudget, b.RenovationBudget, b.InvestmentBudget, b.PurchaseBudget,
            }));

        AddTableSheet(workbook, "Dokument",
            [
                new("Titel", 30), new("Datum", 12, Kind.Date), new("Kategori", 12), new("Projekt", 30),
                new("Lagring", 13), new("Fil i exporten", 50), new("Google Drive-länk", 40),
                new("Filnamn", 30), new("Storlek (byte)", 14, Kind.Number), new("Uppladdad av", 16),
            ],
            export.Documents.Select(d => new object?[]
            {
                d.Title ?? d.FileName, d.Date, Labels.DocumentCategories[d.Category],
                d.ProjectId is not null ? projectNames.GetValueOrDefault(d.ProjectId) : null,
                Labels.StorageKinds[d.Storage], d.FileMissing ? "(saknas)" : d.File, d.DriveWebViewLink,
                d.FileName, d.SizeBytes, d.UploadedBy,
            }));

        AddTableSheet(workbook, "Komponenter",
            [new("Namn", 24), new("Intervall (månader)", 18, Kind.Number), new("Id", 38)],
            export.Components.Select(c => new object?[] { c.Name, c.RecommendedIntervalMonths, c.Id }));

        workbook.SaveAs(destination);
    }

    private static void AddKeyValueSheet(XLWorkbook workbook, string name, (string Label, object? Value, Kind Kind)[] rows)
    {
        var sheet = workbook.Worksheets.Add(name);
        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 50;
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 1, 1).Value = rows[i].Label;
            sheet.Cell(i + 1, 1).Style.Font.Bold = true;
            SetCell(sheet.Cell(i + 1, 2), rows[i].Value, rows[i].Kind);
            sheet.Cell(i + 1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }
    }

    private static void AddTableSheet(XLWorkbook workbook, string name, Column[] columns, IEnumerable<object?[]> rows)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var c = 0; c < columns.Length; c++)
        {
            var header = sheet.Cell(1, c + 1);
            header.Value = columns[c].Header;
            header.Style.Font.Bold = true;
            sheet.Column(c + 1).Width = columns[c].Width;
        }

        var rowCount = 0;
        foreach (var row in rows)
        {
            rowCount++;
            for (var c = 0; c < columns.Length; c++)
            {
                SetCell(sheet.Cell(rowCount + 1, c + 1), row[c], columns[c].Kind);
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, rowCount + 1, columns.Length).SetAutoFilter();
    }

    private static void SetCell(IXLCell cell, object? value, Kind kind)
    {
        if (value is null)
        {
            return;
        }

        switch (kind)
        {
            case Kind.Money:
                cell.Value = Convert.ToDouble(value);
                cell.Style.NumberFormat.Format = MoneyFormat;
                break;
            case Kind.Number:
                cell.Value = Convert.ToDouble(value);
                break;
            case Kind.Date:
                cell.Value = value switch
                {
                    DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                    DateTimeOffset dto => dto.UtcDateTime.Date,
                    _ => throw new ArgumentException($"Not a date: {value.GetType()}"),
                };
                cell.Style.NumberFormat.Format = DateFormat;
                break;
            case Kind.YesNo:
                cell.Value = (bool)value ? "Ja" : "Nej";
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
