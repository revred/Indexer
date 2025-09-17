using ClosedXML.Excel;
using Xunit;

public class WorkbookValidationTests
{
    [Fact]
    public void Workbook_Structure_Is_Consistent_If_Present()
    {
        // Optional integration: set CHECK_OUTPUT_XLSX=1 to enforce presence.
        string outPath = Path.Combine("..","OUTPUT","IndexContainment.xlsx");
        bool enforce = Environment.GetEnvironmentVariable("CHECK_OUTPUT_XLSX") == "1";
        if (!File.Exists(outPath))
        {
            if (enforce) Assert.True(false, $"Expected workbook at {outPath}");
            else return; // skip silently
        }

        using var wb = new XLWorkbook(outPath);
        var config = wb.Worksheet("Config");
        Assert.NotNull(config);

        foreach (var ws in wb.Worksheets.Where(w => w.Name != "Config"))
        {
            // Check summary header
            Assert.Equal("Threshold", ws.Cell(1,1).GetString());
            Assert.Equal("n", ws.Cell(1,2).GetString());
            Assert.Equal("Hits", ws.Cell(1,3).GetString());
            // Find daily table header row (first blank row after summaries + 1)
            int headerRow = 1;
            while (!string.IsNullOrWhiteSpace(ws.Cell(headerRow,1).GetString())) headerRow++;
            headerRow++; // header line
            Assert.Equal("Date", ws.Cell(headerRow,1).GetString());
            Assert.Equal("GapPct", ws.Cell(headerRow,8).GetString());
        }
    }
}