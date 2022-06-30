using doctors_api.Records;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.IO.Compression;

namespace doctors_api.Model
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options)
            : base(options) { }

        public DbSet<Doctor> Doctors => Set<Doctor>();

        public DatabaseUpdateStatistics BulkInsert(StreamReader sr)
        {
            int numberLines = 0;

            Database.OpenConnection();

            var truncate = Database.GetDbConnection().CreateCommand();
            truncate.CommandText = @"DELETE FROM doctors";
            truncate.ExecuteNonQuery();
            truncate.CommandText = @"DELETE FROM sqlite_sequence WHERE name='doctors'";
            truncate.ExecuteNonQuery();

            using (var transaction = Database.GetDbConnection().BeginTransaction())
            {
                var command = Database.GetDbConnection().CreateCommand();
                command.CommandText = @"INSERT INTO doctors (Crm, Name, Status, Subscription, Inactivation, City, Uf, Specialties) VALUES ($crm, $name, $status, $subscription, $inactivation, $city, $uf, $specialties)";

                var parameter1 = CreateParameter(command, "$crm");
                var parameter2 = CreateParameter(command, "$name");
                var parameter3 = CreateParameter(command, "$status");
                var parameter4 = CreateParameter(command, "$subscription");
                var parameter5 = CreateParameter(command, "$inactivation");
                var parameter6 = CreateParameter(command, "$city");
                var parameter7 = CreateParameter(command, "$uf");
                var parameter8 = CreateParameter(command, "$specialties");

                string? line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("Codigo")) continue;

                    parameter1.Value = ParameterValue(line, 0);
                    parameter2.Value = ParameterValue(line, 1);
                    parameter3.Value = ParameterValue(line, 2);
                    parameter4.Value = ParameterValue(line, 3);
                    parameter5.Value = ParameterValue(line, 4);
                    parameter6.Value = ParameterValue(line, 5);
                    parameter7.Value = ParameterValue(line, 6);
                    parameter8.Value = ParameterValue(line, 7);

                    command.ExecuteNonQuery();

                    numberLines++;
                }

                transaction.Commit();
            }

            return new DatabaseUpdateStatistics(numberLines, 0);
        }

        public static async Task EnsureDBExiste(IServiceProvider services, ILogger logger, string connectionString)
        {
            logger.LogInformation("Garantindo que o banco de dados exista e esteja na string de conex�o : '{connectionString}'", connectionString);
            using var db = services.CreateScope().ServiceProvider.GetRequiredService<ApiDbContext>();
            await db.Database.EnsureCreatedAsync();

            try
            {
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError("Error to exeute migrations.", ex);
            }

            if (!db.Doctors.Any()) _ = await UpdateDatabase(db);
        }
        public static async Task<DatabaseUpdateStatistics> UpdateDatabase(ApiDbContext db)
        {
            DatabaseUpdateStatistics databaseUpdateStatistics = new(0, 0);

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
        private static DbParameter CreateParameter(DbCommand command, string name)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            command.Parameters.Add(parameter);

            return parameter;
        }
        private static object? ParameterValue(string line, int index)
        {
            object returnValue = DBNull.Value;

            if (!"-  -".Equals(line.Split('|')[index].Trim()))
                returnValue = line.Split('|')[index].Trim();

            return returnValue;
        }
    }
}
