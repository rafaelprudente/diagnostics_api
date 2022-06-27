using doctors_api.Model;
using doctors_api.Records;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SqliteConnectionString") ?? "Data Source=doctors_api.db";

builder.Services.AddSqlite<ApiDbContext>(connectionString);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
}).CreateLogger("Program");

var app = builder.Build();

await EnsureDBExiste(app.Services, app.Logger);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/doctors/update_database", (ApiDbContext db) =>
{
    var tempFolder = new FileInfo(Path.GetTempFileName()).Directory;

    var medicosZip = Path.GetTempFileName();
    var medicosZipFolder = "";
    if (tempFolder != null && tempFolder.Exists)
        medicosZipFolder = Path.Combine(tempFolder.FullName, "MEDICOS_ZIP");

    if (new DirectoryInfo(medicosZipFolder).Exists) Directory.Delete(medicosZipFolder, true);

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8604 // Possible null reference argument.
    var content = GetUrlContent("http://www.cremesp.org.br/servicos/Downloads/MEDICOS.ZIP");
    if (content != null)
    {
        File.WriteAllBytes($"{medicosZip}", bytes: content.Result);
        ZipFile.ExtractToDirectory(medicosZip, medicosZipFolder);
    }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8604 // Possible null reference argument.

    return db.BulkInsert(Path.Combine(medicosZipFolder, "FISICA.TXT"));
})
.WithName("GetDoctorsUpdateDatabase");

app.MapGet("/doctors", (ApiDbContext db, string? partName, string? city, int? page,
  int? pageSize) =>
{
    PageResult<doctors_api.Model.Doctor> returnValue = new PageResult<doctors_api.Model.Doctor>();

    int pageNumber = page ?? 1;
    int pagerTake = pageSize ?? 50;

    if (partName == null && city == null)
    {
        returnValue = db.Doctors.OrderBy(d => d.Name).GetPaged(pageNumber, pagerTake);
    }

    if (partName != null && city == null)
    {
        returnValue = db.Doctors.Where(d => d.Name.StartsWith(partName.ToUpperInvariant())).OrderBy(d => d.Name).GetPaged(pageNumber, pagerTake);
    }

    if (partName == null && city != null)
    {
        returnValue = db.Doctors.Where(d => d.City.Equals(city.ToUpperInvariant())).OrderBy(d => d.Name).GetPaged(pageNumber, pagerTake);
    }

    if (partName != null && city != null)
    {
        returnValue = db.Doctors.Where(d => d.Name.StartsWith(partName.ToUpperInvariant()) && d.City.Equals(city.ToUpperInvariant())).OrderBy(d => d.Name).GetPaged(pageNumber, pagerTake);
    }

    return returnValue;
})
.WithName("GetDoctors");

app.Run();

static async Task<byte[]?> GetUrlContent(string url)
{
    using var client = new HttpClient();
    using var result = await client.GetAsync(url);
    return result.IsSuccessStatusCode ? await result.Content.ReadAsByteArrayAsync() : null;
}

async Task EnsureDBExiste(IServiceProvider services, ILogger logger)
{
    logger.LogInformation("Garantindo que o banco de dados exista e esteja na string de conexão : '{connectionString}'", connectionString);
    using var db = services.CreateScope().ServiceProvider.GetRequiredService<ApiDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.MigrateAsync();
}