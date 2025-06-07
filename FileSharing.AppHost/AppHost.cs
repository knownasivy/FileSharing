using FileSharing.AppHost.OpenTelemetryCollector;
using FileSharing.Constants;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres(ProjectNames.Postgres)
    .WithDataVolume();

var databaseName = ProjectNames.GetConnectionString(builder.Environment.IsDevelopment());
var db = postgres.AddDatabase(databaseName);

var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v3.2.1")
    .WithVolume("grafana", "/var/lib/grafana")
    .WithBindMount("../prometheus", "/etc/prometheus", isReadOnly: true)
    .WithArgs("--web.enable-otlp-receiver", "--config.file=/etc/prometheus/prometheus.yml")
    .WithHttpEndpoint(targetPort: 9090, name: "http");

var grafana = builder.AddContainer("grafana", "grafana/grafana")
    .WithVolume("grafana", "/var/lib/grafana")
    .WithBindMount("../grafana/config", "/etc/grafana", isReadOnly: true)
    .WithBindMount("../grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
    .WithEnvironment("PROMETHEUS_ENDPOINT", prometheus.GetEndpoint("http"))
    .WithHttpEndpoint(targetPort: 3000, port: 4444, name: "http");

builder.AddOpenTelemetryCollector("otelcollector", "../otelcollector/config.yaml")
    .WithEnvironment("PROMETHEUS_ENDPOINT", $"{prometheus.GetEndpoint("http")}/api/v1/otlp");

var api = builder.AddProject<Projects.FileSharing_ApiService>(ProjectNames.ApiService)
    .WithHttpHealthCheck("/health")
    //.WithEnvironment("GRAFANA_URL", grafana.GetEndpoint("http"))
    .WithReference(db)
    .WaitFor(postgres);

builder.AddYarp("apigateway")
    .WithHttpsEndpoint(port: 8443, targetPort: 8443)
    .WithExternalHttpEndpoints()
    .WithVolume("yarp", "/app/data")
    .WithConfigFile("yarp.json")
    .WithReference(api);

await using var app = builder.Build();

await app.RunAsync();