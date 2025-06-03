using Dapper;
using DbUp;
using FileSharing.ApiService.Database.Types;
using FileSharing.ApiService.Models;

namespace FileSharing.ApiService.Database;

public class DatabaseInitializer
{
    private readonly string? _connectionString;

    public DatabaseInitializer(string? connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new Exception("Connection string not set");
        }
        
        SqlMapper.AddTypeHandler(new JsonbTypeHandler<List<ZipItem>>());
        
        EnsureDatabase.For.PostgresqlDatabase(_connectionString);

        var upgrader = DeployChanges.To.PostgresqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseInitializer).Assembly)
            .LogToConsole()
            .Build();

        if (upgrader.IsUpgradeRequired())
        {
            upgrader.PerformUpgrade();
        }
    }
}