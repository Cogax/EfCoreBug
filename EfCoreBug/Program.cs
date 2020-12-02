using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreBug
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var local = Environment.GetEnvironmentVariable("ConnectionStringLocal")
                ?? "Data Source=localhost,1433; Database=MyDb; multipleactiveresultsets=False; User Id=sa; Password=Top-Secret; Connection Timeout=60; ConnectRetryCount=8; ConnectRetryInterval=2";

            var azure = Environment.GetEnvironmentVariable("ConnectionStringAzure")
                ?? "data source=***.database.windows.net;initial catalog=***;persist security info=True;user id=***;password=***;multipleactiveresultsets=False;application name=EntityFramework;Max Pool Size=2000;";

            var connectionString = azure;

            await EnsureDatabaseExists(connectionString);
            await RecreateTable(connectionString);

            var context = GetDbContext(connectionString);

            var foos = Enumerable.Range(1, 4000)
                .OrderBy(x => x)
                .Select(x => new Foo {Value = x})
                .ToList();

            await context.Foo.AddRangeAsync(foos);
            await context.SaveChangesAsync();

            if (context.Foo.Count() != foos.Count)
                throw new Exception("not fully saved");

            var fooIds = foos.Select(x => x.Id).ToList();
            var loadedFoos = await context.Foo
                .Where(x => fooIds.Contains(x.Id))
                .ToListAsync();


            if(loadedFoos.Count != foos.Count)
                throw new Exception("not fully loaded");

            Console.WriteLine("Fully Loaded! OK!");
        }

        private static MyContext GetDbContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<MyContext>()
                .UseSqlServer(
                    connectionString,
                    sqlServerOptionsAction => sqlServerOptionsAction
                        .EnableRetryOnFailure()
                        .CommandTimeout(3600))
                .EnableSensitiveDataLogging()
                .Options;

            var context = new MyContext(options);
            return context;
        }

        public static async Task EnsureDatabaseExists(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dbName = builder.InitialCatalog;

            Console.WriteLine($"Ensuring Database [{dbName}] exists...");

            builder.InitialCatalog = "master";

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            var query = connection.CreateCommand();

            query.CommandText = $"SELECT COUNT(*) FROM sys.databases WHERE NAME = '{dbName}'";

            var result = await query.ExecuteScalarAsync();

            if ((int)result != 0)
            {
                Console.WriteLine($"Database [{dbName}] already exists!");
                return;
            }

            Console.WriteLine($"Creating Database [{dbName}].");

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task RecreateTable(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = 
                $@"DROP TABLE IF EXISTS Foo; 
                    CREATE TABLE Foo (Id [BIGINT] NOT NULL identity(1,1) primary key, Value [BIGINT] NOT NULL);";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public class MyContext : DbContext
    {
        public DbSet<Foo> Foo { get; set; }
        public MyContext(DbContextOptions<MyContext> options) : base(options)
        { }
    }

    public class Foo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public long Value { get; set; }
    }
}
