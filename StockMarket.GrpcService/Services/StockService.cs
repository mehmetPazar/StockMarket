using Grpc.Core;
using System.Collections.Concurrent;
using System.Text.Json;

namespace StockMarket.GrpcService.Services;

public class StockService : global::StockMarket.GrpcService.StockService.StockServiceBase
{
    private readonly ILogger<StockService> _logger;
    private static readonly Random _random = new Random();
    
    // Supported stocks and their static information
    private static readonly Dictionary<string, StockInfo> _stocks = new Dictionary<string, StockInfo>
    {
        ["AAPL"] = new StockInfo { Symbol = "AAPL", CompanyName = "Apple Inc.", BasePrice = 175.50, PreviousClose = 173.75 },
        ["GOOGL"] = new StockInfo { Symbol = "GOOGL", CompanyName = "Alphabet Inc.", BasePrice = 134.25, PreviousClose = 133.20 },
        ["MSFT"] = new StockInfo { Symbol = "MSFT", CompanyName = "Microsoft Corporation", BasePrice = 325.80, PreviousClose = 323.50 },
        ["AMZN"] = new StockInfo { Symbol = "AMZN", CompanyName = "Amazon.com, Inc.", BasePrice = 128.90, PreviousClose = 127.15 },
        ["TSLA"] = new StockInfo { Symbol = "TSLA", CompanyName = "Tesla, Inc.", BasePrice = 245.75, PreviousClose = 242.30 }
    };
    
    // Active subscriptions
    private static readonly ConcurrentDictionary<string, IList<IServerStreamWriter<StockPriceUpdate>>> _subscriptions = 
        new ConcurrentDictionary<string, IList<IServerStreamWriter<StockPriceUpdate>>>();
    
    // Timer for price simulation
    private static readonly Timer _priceUpdateTimer;
    
    static StockService()
    {
        // Price update every 0.5 seconds (500ms)
        _priceUpdateTimer = new Timer(UpdatePrices, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }
    
    public StockService(ILogger<StockService> logger)
    {
        _logger = logger;
        _logger.LogInformation("StockService started. Supported stocks: {Symbols}", 
            string.Join(", ", _stocks.Keys));
    }
    
    public override Task<StockResponse> GetStock(StockRequest request, ServerCallContext context)
    {
        var symbol = request.Symbol.ToUpper();
        var clientIp = context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("[gRPC] GetStock request: Symbol={Symbol}, Client={ClientIp}, Time={Time}", 
            symbol, clientIp, DateTime.Now.ToString("HH:mm:ss.fff"));
        
        if (!_stocks.TryGetValue(symbol, out var stockInfo))
        {
            var errorMsg = $"Stock not found: {symbol}";
            _logger.LogWarning("[gRPC] GetStock error: {Error}, Client={ClientIp}", errorMsg, clientIp);
            throw new RpcException(new Status(StatusCode.NotFound, errorMsg));
        }
        
        // Calculate current price and change values
        var currentPrice = stockInfo.BasePrice * (1 + (Math.Sin(DateTime.Now.Ticks / 10000000.0) * 0.02));
        var change = currentPrice - stockInfo.PreviousClose;
        
        var response = new StockResponse
        {
            Symbol = stockInfo.Symbol,
            CompanyName = stockInfo.CompanyName,
            Price = Math.Round(currentPrice, 2),
            Change = Math.Round(change, 2),
            Volume = _random.Next(100000, 10000000),
            Open = stockInfo.PreviousClose * (1 + (_random.NextDouble() * 0.01 - 0.005)),
            High = currentPrice * (1 + (_random.NextDouble() * 0.01)),
            Low = currentPrice * (1 - (_random.NextDouble() * 0.01)),
            PreviousClose = stockInfo.PreviousClose,
            MarketCap = FormatMarketCap(currentPrice * stockInfo.OutstandingShares)
        };
        
        _logger.LogInformation("[gRPC] GetStock response: Symbol={Symbol}, Price={Price}, Change={Change}, Client={ClientIp}", 
            response.Symbol, response.Price, response.Change, clientIp);
        
        return Task.FromResult(response);
    }
    
    public override async Task StreamStockPrice(StockRequest request, IServerStreamWriter<StockPriceUpdate> responseStream, ServerCallContext context)
    {
        var symbol = request.Symbol.ToUpper();
        var clientIp = context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("[gRPC] StreamStockPrice connection: Symbol={Symbol}, Client={ClientIp}, Time={Time}", 
            symbol, clientIp, DateTime.Now.ToString("HH:mm:ss.fff"));
        
        if (!_stocks.ContainsKey(symbol))
        {
            var errorMsg = $"Stock not found: {symbol}";
            _logger.LogWarning("[gRPC] StreamStockPrice error: {Error}, Client={ClientIp}", errorMsg, clientIp);
            throw new RpcException(new Status(StatusCode.NotFound, errorMsg));
        }
        
        var streamList = _subscriptions.GetOrAdd(symbol, _ => new List<IServerStreamWriter<StockPriceUpdate>>());
        
        lock (streamList)
        {
            streamList.Add(responseStream);
        }
        
        _logger.LogInformation("[gRPC] New stock stream started: Symbol={Symbol}, Client={ClientIp}, ActiveStreams={Count}", 
            symbol, clientIp, streamList.Count);
        
        try
        {
            // Wait until client disconnects
            await Task.Delay(TimeSpan.FromHours(24), context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[gRPC] Client disconnected: Symbol={Symbol}, Client={ClientIp}", symbol, clientIp);
        }
        finally
        {
            // Unsubscribe
            lock (streamList)
            {
                streamList.Remove(responseStream);
                _logger.LogInformation("[gRPC] Stock stream ended: Symbol={Symbol}, Client={ClientIp}, RemainingStreams={Count}", 
                    symbol, clientIp, streamList.Count);
            }
        }
    }
    
    public override async Task StreamMultipleStocks(MultiStockRequest request, IServerStreamWriter<StockPriceUpdate> responseStream, ServerCallContext context)
    {
        var symbols = request.Symbols.Select(s => s.ToUpper()).ToList();
        var clientIp = context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("[gRPC] StreamMultipleStocks connection: Symbols={Symbols}, Client={ClientIp}, Time={Time}", 
            string.Join(", ", symbols), clientIp, DateTime.Now.ToString("HH:mm:ss.fff"));
        
        var invalidSymbols = symbols.Where(s => !_stocks.ContainsKey(s)).ToList();
        
        if (invalidSymbols.Any())
        {
            var errorMsg = $"Invalid symbols: {string.Join(", ", invalidSymbols)}";
            _logger.LogWarning("[gRPC] StreamMultipleStocks error: {Error}, Client={ClientIp}", errorMsg, clientIp);
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMsg));
        }
        
        foreach (var symbol in symbols)
        {
            var streamList = _subscriptions.GetOrAdd(symbol, _ => new List<IServerStreamWriter<StockPriceUpdate>>());
            
            lock (streamList)
            {
                streamList.Add(responseStream);
            }
            
            _logger.LogDebug("[gRPC] Symbol subscribed: Symbol={Symbol}, Client={ClientIp}", symbol, clientIp);
        }
        
        _logger.LogInformation("[gRPC] Multiple stock stream started: SymbolCount={Count}, Client={ClientIp}", 
            symbols.Count, clientIp);
        
        try
        {
            // Wait until client disconnects
            await Task.Delay(TimeSpan.FromHours(24), context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[gRPC] Client disconnected from multiple stream: Client={ClientIp}", clientIp);
        }
        finally
        {
            // Unsubscribe
            foreach (var symbol in symbols)
            {
                if (_subscriptions.TryGetValue(symbol, out var streamList))
                {
                    lock (streamList)
                    {
                        streamList.Remove(responseStream);
                        _logger.LogDebug("[gRPC] Symbol unsubscribed: Symbol={Symbol}, Client={ClientIp}, RemainingStreams={Count}", 
                            symbol, clientIp, streamList.Count);
                    }
                }
            }
            
            _logger.LogInformation("[gRPC] Multiple stock stream ended: SymbolCount={Count}, Client={ClientIp}", 
                symbols.Count, clientIp);
        }
    }
    
    private static async void UpdatePrices(object? state)
    {
        foreach (var stock in _stocks)
        {
            var symbol = stock.Key;
            var stockInfo = stock.Value;
            
            // Realistic price fluctuation using sine function
            var currentTime = DateTime.Now.Ticks / 10000000.0;
            var noise = Math.Sin(currentTime) * 0.01 + Math.Sin(currentTime * 0.3) * 0.005 + ((_random.NextDouble() * 0.01) - 0.005);
            
            var currentPrice = stockInfo.BasePrice * (1 + noise);
            var change = currentPrice - stockInfo.PreviousClose;
            var changePercent = (change / stockInfo.PreviousClose) * 100;
            
            var update = new StockPriceUpdate
            {
                Symbol = symbol,
                Price = Math.Round(currentPrice, 2),
                Change = Math.Round(change, 2),
                ChangePercent = Math.Round(changePercent, 2),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            // Send update to all subscribed clients
            if (_subscriptions.TryGetValue(symbol, out var streamList) && streamList.Count > 0)
            {
                List<IServerStreamWriter<StockPriceUpdate>> deadStreams = new();
                
                IServerStreamWriter<StockPriceUpdate>[] streamsToProcess;
                
                lock (streamList)
                {
                    streamsToProcess = streamList.ToArray();
                }
                
                // Log only active stream count to avoid excessive logging
                if (streamList.Count > 0 && DateTime.Now.Second % 5 == 0 && DateTime.Now.Millisecond < 100)
                {
                    // Log every 5 seconds
                    Console.WriteLine($"[gRPC] {DateTime.Now:HH:mm:ss.fff} - Active streams for {symbol}: {streamList.Count}");
                }
                
                foreach (var stream in streamsToProcess)
                {
                    try
                    {
                        await stream.WriteAsync(update);
                    }
                    catch (Exception ex)
                    {
                        // Mark disconnected clients
                        deadStreams.Add(stream);
                        Console.WriteLine($"[gRPC] {DateTime.Now:HH:mm:ss.fff} - Stream write error: {ex.Message}");
                    }
                }
                
                // Remove dead streams
                if (deadStreams.Count > 0)
                {
                    lock (streamList)
                    {
                        foreach (var deadStream in deadStreams)
                        {
                            streamList.Remove(deadStream);
                        }
                    }
                }
            }
        }
    }
    
    private static string FormatMarketCap(double marketCap)
    {
        if (marketCap >= 1_000_000_000_000) // Trillion
            return $"{marketCap / 1_000_000_000_000:F2}T";
        if (marketCap >= 1_000_000_000) // Billion
            return $"{marketCap / 1_000_000_000:F2}B";
        if (marketCap >= 1_000_000) // Million
            return $"{marketCap / 1_000_000:F2}M";
        return $"{marketCap:F2}";
    }
    
    private class StockInfo
    {
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public double BasePrice { get; set; }
        public double PreviousClose { get; set; }
        public long OutstandingShares { get; set; } = 1_000_000_000; // Default
    }
} 