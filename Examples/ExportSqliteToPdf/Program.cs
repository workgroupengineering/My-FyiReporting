// ExportSqliteToPdf — minimal example: query a SQLite database and write a PDF.
//
// Run:  dotnet run
// Output: products.pdf in the project output directory
//
// Key patterns shown:
//   - RdlEngineConfig.RdlEngineConfigInit() called once at startup
//   - RDLParser takes the RDL XML as a plain string — any source works
//   - The database path is injected into the RDL XML before parsing so
//     the engine can validate the schema at parse time

using Majorsilence.Reporting.Rdl;

RdlEngineConfig.RdlEngineConfigInit();

var baseDir = AppContext.BaseDirectory;
var rdlPath = Path.Combine(baseDir, "Products.rdl");
var dbPath  = Path.Combine(baseDir, "sqlitetestdb2.db");
var outPath = Path.Combine(baseDir, "products.pdf");

// Inject the actual database path before parsing so the engine can
// validate the schema.  The RDL file stores a relative placeholder.
var rdlXml = (await File.ReadAllTextAsync(rdlPath))
    .Replace("sqlitetestdb2.db", dbPath);

var rdlp = new RDLParser(rdlXml) { Folder = baseDir };
using var report = await rdlp.Parse();

if (report.ErrorMaxSeverity > 4)
{
    Console.Error.WriteLine("Report parse errors:");
    foreach (var err in report.ErrorItems)
        Console.Error.WriteLine($"  {err}");
    return 1;
}

await report.RunGetData(null);

var ofs = new OneFileStreamGen(outPath, true);
await report.RunRender(ofs, OutputPresentationType.PDF);

Console.WriteLine($"Written: {outPath}");
return 0;
