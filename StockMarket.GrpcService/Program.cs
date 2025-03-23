using StockMarket.GrpcService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Loglama ayarlarını yapılandır
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Kendi gRPC servislerimizin log seviyesini Information olarak ayarla
builder.Logging.AddFilter("StockMarket.GrpcService.Services", LogLevel.Information);

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16 MB
    options.MaxSendMessageSize = 16 * 1024 * 1024; // 16 MB
});

// StockDataService'i ekle
builder.Services.AddSingleton<StockDataService>();

// Controller ve API desteği ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS servislerini ekle - bu hem REST hem de gRPC için gerekli
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Tüm originlere izin ver (geliştirme ortamı için)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // SSE için gerekli
              .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding", "Content-Type");
    });
});

var app = builder.Build();

// Console log ile gRPC servisinin başlatıldığını bildir
Console.WriteLine($"=== gRPC Servisi Başlatılıyor - {DateTime.Now} ===");

// CORS middleware'i ekle - önemli: CORS middleware'ini pipeline'ın başına koy
app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();

// Controller ve gRPC servislerini etkinleştir
app.MapControllers();
app.MapGrpcService<GreeterService>();
app.MapGrpcService<StockService>();

// Swagger/API dokümanları ve tanıtım için başlangıç sayfası
app.MapGet("/", () => "gRPC Stock Market Servisi\ngRPC: https://localhost:5207\nREST API: /swagger");

app.MapDefaultEndpoints();

app.Run();
