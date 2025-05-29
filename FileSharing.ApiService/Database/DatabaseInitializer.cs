using DbUp;
using FileSharing.Constants;
using Npgsql;

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