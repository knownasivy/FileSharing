using FileSharing.Constants;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres(ProjectNames.Postgres)
    .WithDataVolume(isReadOnly: false);

var databaseName = ProjectNames.GetConnectionString(builder.Environment.IsDevelopment());
if (builder.Environment.IsDevelopment())
{
    postgres.WithPgAdmin();
}
else
{
    // TODO: Do i do this?
    // postgres.WithPgWeb();
}

/*
-- Create the tables
CREATE TABLE files (
    file_id UUID PRIMARY KEY,
    file_name VARCHAR(255) NOT NULL,
    file_ext VARCHAR(16) NOT NULL,
    file_size BIGINT NOT NULL,
-- Other, Audio, Hash
    file_type VARCHAR(32) NOT NULL,
-- Uploading, Uploaded, Deleted
    file_status VARCHAR(32) NOT NULL,
    created_at DATE NOT NULL,
    file_hash VARCHAR(24)
);
*/

var creationScript = $$"""
                       -- Create the database
                       CREATE DATABASE {{databaseName}};
                       """;

var db = postgres.AddDatabase(databaseName)
    .WithCreationScript(creationScript);

// TODO: Maybe make persistant in the future
var cache = builder.AddRedis(ProjectNames.Redis);
//    .WithDataVolume(isReadOnly: false);

builder.AddProject<Projects.FileSharing_ApiService>(ProjectNames.ApiService)
    .WithHttpHealthCheck("/health")
    .WithReference(db)
    .WithReference(cache)
    .WaitFor(postgres)
    .WaitFor(cache);

builder.Build().Run();