using StockMarket.GrpcService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Configure logging settings
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Set log level for our own gRPC services to Information
builder.Logging.AddFilter("StockMarket.GrpcService.Services", LogLevel.Information);

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16 MB
    options.MaxSendMessageSize = 16 * 1024 * 1024; // 16 MB
});

// Add StockDataService
builder.Services.AddSingleton<StockDataService>();

// Add controller and API support
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS services - this is required for both REST and gRPC
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow all origins (for development environment)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Required for SSE
              .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding", "Content-Type");
    });
});

var app = builder.Build();

// Log gRPC service startup with console
Console.WriteLine($"=== gRPC Service Starting - {DateTime.Now} ===");

// Add CORS middleware - important: put CORS middleware at the beginning of the pipeline
app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();

// Enable controller and gRPC services
app.MapControllers();
app.MapGrpcService<GreeterService>();
app.MapGrpcService<StockService>();

// Start page for Swagger/API documentation and introduction
app.MapGet("/", () => "gRPC Stock Market Service\ngRPC: https://localhost:5207\nREST API: /swagger");

app.MapDefaultEndpoints();

app.Run();
