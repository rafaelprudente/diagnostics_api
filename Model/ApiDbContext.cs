using doctors_api.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace doctors_api.Model
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options)
            : base(options) { }

        public DbSet<Doctor> Doctors => Set<Doctor>();

        public DatabaseUpdateStatistics BulkInsert(string fullFileName)
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

                var parameter1 = command.CreateParameter();
                parameter1.ParameterName = "$crm";
                var parameter2 = command.CreateParameter();
                parameter2.ParameterName = "$name";
                var parameter3 = command.CreateParameter();
                parameter3.ParameterName = "$status";
                var parameter4 = command.CreateParameter();
                parameter4.ParameterName = "$subscription";
                var parameter5 = command.CreateParameter();
                parameter5.ParameterName = "$inactivation";
                var parameter6 = command.CreateParameter();
                parameter6.ParameterName = "$city";
                var parameter7 = command.CreateParameter();
                parameter7.ParameterName = "$uf";
                var parameter8 = command.CreateParameter();
                parameter8.ParameterName = "$specialties";

                command.Parameters.Add(parameter1);
                command.Parameters.Add(parameter2);
                command.Parameters.Add(parameter3);
                command.Parameters.Add(parameter4);
                command.Parameters.Add(parameter5);
                command.Parameters.Add(parameter6);
                command.Parameters.Add(parameter7);
                command.Parameters.Add(parameter8);


                foreach (string line in System.IO.File.ReadLines(fullFileName))
                {
                    if (line.StartsWith("Codigo")) continue;

                    parameter1.Value = line.Split('|')[0].Trim();
                    parameter2.Value = line.Split('|')[1].Trim();
                    parameter3.Value = line.Split('|')[2].Trim();
                    if ("-  -".Equals(line.Split('|')[3].Trim()))
                    {
                        parameter4.Value = DBNull.Value;
                    }
                    else
                    {
                        parameter4.Value = line.Split('|')[3].Trim();
                    }
                    if ("-  -".Equals(line.Split('|')[4].Trim()))
                    {
                        parameter5.Value = DBNull.Value;
                    }
                    else
                    {
                        parameter5.Value = line.Split('|')[4].Trim();
                    }
                    parameter6.Value = line.Split('|')[5].Trim();
                    parameter7.Value = line.Split('|')[6].Trim();
                    parameter8.Value = line.Split('|')[7].Trim();

                    command.ExecuteNonQuery();

                    numberLines++;
                }

                transaction.Commit();
            }

            return new DatabaseUpdateStatistics(numberLines, 0);
        }
    }
}
