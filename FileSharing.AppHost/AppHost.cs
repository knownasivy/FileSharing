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

/*
create table Files
(
    Id        uuid not null
        constraint Files_pk
            primary key,
    Name      text not null,
    Size      integer not null,
    Type      integer not null,
    Status    integer not null,
    CreatedAt timestamp not null,
    Hash      bytea not null,
    FakeFile  boolean not null,
    IPAddress text not null
);
*/

var creationScript = $$"""
                       -- Create the database
                       CREATE DATABASE {{databaseName}};
                       """;

var db = postgres.AddDatabase(databaseName)
    .WithCreationScript(creationScript);

// TODO: Maybe make persistant in the future
//var cache = builder.AddRedis(ProjectNames.Redis);
//    .WithDataVolume(isReadOnly: false);

builder.AddProject<Projects.FileSharing_ApiService>(ProjectNames.ApiService)
    .WithHttpHealthCheck("/health")
    .WithReference(db)
    .WaitFor(postgres);

builder.Build().Run();