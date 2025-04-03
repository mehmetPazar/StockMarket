var builder = DistributedApplication.CreateBuilder(args);

// Configure gRPC Service to be externally accessible
var grpcService = builder.AddProject<Projects.StockMarket_GrpcService>("grpcservice")
    .WithExternalHttpEndpoints(); // Make gRPC service accessible from outside

// Web application (Vue.js)
builder.AddNpmApp("webfrontend", "../StockMarket.Web")
    .WithReference(grpcService)
    .WithHttpEndpoint(8081, 5173, isProxied: true);

// Note: StockMarket.Web and StockMarket.ApiGateway projects have been removed from the structure

builder.Build().Run();
