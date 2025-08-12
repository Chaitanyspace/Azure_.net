using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "InvoiceGateway", Version = "v1" });
});

// OPTIONAL: allow big uploads (100 MB)
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = 104_857_600);

// Read configuration (appsettings.json + appsettings.Development.json + env vars)
var cfg = builder.Configuration;

// --- DI: BLOB (force connection string for local dev) ---
var storageConn = cfg["Azure:Storage:ConnectionString"];
if (string.IsNullOrWhiteSpace(storageConn))
    throw new InvalidOperationException("Missing Azure:Storage:ConnectionString (put it in appsettings.Development.json).");

builder.Services.AddSingleton(new BlobServiceClient(storageConn));
builder.Services.AddSingleton(sp =>
{
    var containerName = cfg["Azure:Storage:Container"] ?? "invoices";
    return sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(containerName);
});

// --- DI: SERVICE BUS (force connection string for local dev) ---
var sbConn = cfg["Azure:ServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(sbConn))
    throw new InvalidOperationException("Missing Azure:ServiceBus:ConnectionString (put it in appsettings.Development.json).");

builder.Services.AddSingleton(new ServiceBusClient(sbConn));
builder.Services.AddSingleton(sp =>
{
    var queueName = cfg["Azure:ServiceBus:QueueName"] ?? "invoice-queue";
    return sp.GetRequiredService<ServiceBusClient>().CreateSender(queueName);
});

var app = builder.Build();

// Swagger in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// For local dev, you can keep HTTP only. Comment next line if HTTPS not configured.
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

// Simple health
app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.Run();
