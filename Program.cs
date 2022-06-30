using doctors_api.Model;
using doctors_api.Records;
using LinqKit;
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

await ApiDbContext.EnsureDBExiste(app.Services, app.Logger, connectionString);

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

        return Results.Ok(await ApiDbContext.UpdateDatabase(db));
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

app.MapGet("/doctors/name_suggestions", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader, string? partName, int? page, int? pageSize) =>
{
#pragma warning disable CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
#pragma warning disable CS8602 // Possible null reference argument.

    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        List<string> returnValue = new();

        int pageNumber = page ?? 1;
        int pagerTake = pageSize ?? 50;

        if (partName == null)
            returnValue = db.Doctors.OrderBy(d => d.Name).Select(d => d.Name).GetPaged(pageNumber, pagerTake).Results.ToList();

        if (partName != null)
        {
            string[] keywords = partName.Trim().ToUpperInvariant().Split(' ');

            var predicate = PredicateBuilder.New<doctors_api.Model.Doctor>(false);
            if ((keywords.Length == 1 && keywords[0].Trim().Length > 4) || keywords.Length > 1)
                foreach (string keyword in keywords)
                    predicate = predicate.And(p => p.Name.Contains(keyword));

            returnValue = db.Doctors.Where(predicate).OrderBy(d => d.Name).Select(d => d.Name).GetPaged(pageNumber, pagerTake).Results.ToList();
        }

        return Results.Ok(returnValue);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
#pragma warning restore CS8602 // Possible null reference argument.
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
#pragma warning restore CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
})
.WithName("GetDoctorsNameSuggestions");

app.MapGet("/doctors", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader, string? partName, string? status, string? city, string? uf, string? specialties, int? page, int? pageSize) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        PageResult<doctors_api.Model.Doctor> returnValue = new PageResult<doctors_api.Model.Doctor>();

        int pageNumber = page ?? 1;
        int pagerTake = pageSize ?? 50;

        var predicate = PredicateBuilder.New<doctors_api.Model.Doctor>(false);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (partName != null)
            predicate = predicate.And(p => p.Name.Contains(partName.ToUpperInvariant()));

        if (status != null)
            predicate = predicate.And(p => p.Status.Contains(status.ToUpperInvariant()));

        if (city != null)
            predicate = predicate.And(p => p.City.Contains(city.ToUpperInvariant()));

        if (uf != null)
            predicate = predicate.And(p => p.Uf.Contains(uf.ToUpperInvariant()));

        if (specialties != null)
            predicate = predicate.And(p => p.Specialties.Contains(specialties.ToUpperInvariant()));
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        returnValue = db.Doctors.Where(predicate).OrderBy(d => d.Name)
                                                 .GetPaged(pageNumber, pagerTake);

        return Results.Ok(returnValue);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetDoctors");

app.MapGet("/doctors/{key}", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader, string key) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        long result;
        var predicate = PredicateBuilder.New<doctors_api.Model.Doctor>(false);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (Int64.TryParse(key, out result))
            predicate = predicate.Or(p => p.DoctorId.Equals(int.Parse(key)));
        if (Int64.TryParse(key, out result))
            predicate = predicate.Or(p => p.Crm.Equals(int.Parse(key)));
        predicate = predicate.Or(p => p.Name.Contains(key));
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        return Results.Ok(db.Doctors.Where(predicate).FirstOrDefault());
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetDoctorsByKey");

app.MapGet("/states", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

        return Results.Ok(db.Doctors.Where(d => !string.Empty.Equals(d.Uf)).Select(d => d.Uf).Distinct().OrderBy(d => d).ToList());
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetStates");

app.MapGet("/states/{state}/cities", (ApiDbContext db, [FromHeader(Name = "X-AUTHORIZATION-HEADER")] string authorizationHeader, string state) =>
{
    try
    {
        if (!"7A5B0351-761D-46D6-99E3-8D549A1927DB".Equals(authorizationHeader))
            return Results.Unauthorized();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        return Results.Ok(db.Doctors.Where(d => d.Uf.Equals(state) && !string.Empty.Equals(d.City)).Select(d => d.City).Distinct().OrderBy(d => d).ToList());
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.ToString());
    }
})
.WithName("GetCities");

app.Run();
