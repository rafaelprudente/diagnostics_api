using doctors_api.Model;
using doctors_api.Records;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SqliteConnectionString") ?? "DataSource=:memory:";
var keepAliveConnection = new SqliteConnection(connectionString);
keepAliveConnection.Open();

builder.Services.AddDbContext<ApiDbContext>(options =>
{
    options.UseSqlite(keepAliveConnection);
});
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

app.MapGet("/doctors/update_database", async (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        DatabaseUpdateStatistics databaseUpdateStatistics = new DatabaseUpdateStatistics(0, 0);

        databaseUpdateStatistics = await UpdateDatabase(db);

        return Results.Ok(databaseUpdateStatistics);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetDoctorsUpdateDatabase");

app.MapGet("/doctors/update_database_statistics", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        return Results.Ok(new DatabaseUpdateStatistics(db.Doctors.Count(), 0));
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetDoctorsUpdateDatabaseStatistics");

app.MapGet("/doctors", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader, string? partName, string? city, int? page,
  int? pageSize) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        PageResult<doctors_api.Model.Doctor> returnValue = new PageResult<doctors_api.Model.Doctor>();

        int pageNumber = page ?? 1;
        int pagerTake = pageSize ?? 50;

        if (partName == null && city == null)
        {
            returnValue = db.Doctors.OrderBy(d => d.Name).GetPaged(pageNumber, pagerTake);
        }

        if (partName != null && city == null)
        {
            returnValue = db.Doctors.Where(d => d.Name != null &&
                                                d.Name.StartsWith(partName.ToUpperInvariant()))
                                                      .OrderBy(d => d.Name)
                                                      .GetPaged(pageNumber, pagerTake);
        }

        if (partName == null && city != null)
        {
            returnValue = db.Doctors.Where(d => d.City != null &&
                                                d.City.Equals(city.ToUpperInvariant()))
                                                      .OrderBy(d => d.Name)
                                                      .GetPaged(pageNumber, pagerTake);
        }

        if (partName != null && city != null)
        {
            returnValue = db.Doctors.Where(d => d.Name != null &&
                                                d.Name.StartsWith(partName.ToUpperInvariant()) &&
                                                d.City != null &&
                                                d.City.Equals(city.ToUpperInvariant()))
                                                      .OrderBy(d => d.Name)
                                                      .GetPaged(pageNumber, pagerTake);
        }

        return Results.Ok(returnValue);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetDoctors");

app.Run();

async Task EnsureDBExiste(IServiceProvider services, ILogger logger)
{
    try
    {
        logger.LogInformation("Garantindo que o banco de dados exista e esteja na string de conex�o : '{connectionString}'", connectionString);
        using var db = services.CreateScope().ServiceProvider.GetRequiredService<ApiDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogError("Error creating database.", ex);
    }
}

async Task<DatabaseUpdateStatistics> UpdateDatabase(ApiDbContext db)
{
    DatabaseUpdateStatistics databaseUpdateStatistics = new DatabaseUpdateStatistics(0, 0);

    using (HttpClient client = new())
    {
        using HttpResponseMessage response = await client.GetAsync("http://www.cremesp.org.br/servicos/Downloads/MEDICOS.ZIP", HttpCompletionOption.ResponseHeadersRead);
        using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
        ZipArchive archive = new(streamToReadFrom);
        ZipArchiveEntry entry = archive.Entries[0];
        using StreamReader sr = new(entry.Open());
        databaseUpdateStatistics = db.BulkInsert(sr);
    }

    return databaseUpdateStatistics;
}