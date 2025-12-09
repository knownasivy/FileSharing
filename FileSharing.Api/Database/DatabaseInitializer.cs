using Dapper;
using DbUp;
using DbUp.Engine;
using FileSharing.Api.Database.Types;
using FileSharing.Api.Models;
using FileSharing.Api.Shared;
using Microsoft.Extensions.Options;

namespace FileSharing.Api.Database;

public class DatabaseInitializer
{
    private readonly string? _connectionString;

    public DatabaseInitializer(string? connectionString) => 
        _connectionString = connectionString;

    public void Initialize()
    {
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
