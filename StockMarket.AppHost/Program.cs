var builder = DistributedApplication.CreateBuilder(args);

// gRPC Servisi doğrudan dışarıya açık olacak şekilde yapılandır
var grpcService = builder.AddProject<Projects.StockMarket_GrpcService>("grpcservice")
    .WithExternalHttpEndpoints(); // gRPC servisi dışarıdan erişilebilir olsun

// Web uygulaması (Vue.js)
builder.AddNpmApp("webfrontend", "../StockMarket.Web")
    .WithReference(grpcService)
    .WithHttpEndpoint(8081, 5173, isProxied: true);

// Not: StockMarket.Web ve StockMarket.ApiGateway projeleri yapıdan kaldırıldı

builder.Build().Run();
