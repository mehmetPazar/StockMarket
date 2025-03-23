using Grpc.Core;
using System.Collections.Concurrent;
using System.Text.Json;

namespace StockMarket.GrpcService.Services;

public class StockService : global::StockMarket.GrpcService.StockService.StockServiceBase
{
    private readonly ILogger<StockService> _logger;
    private static readonly Random _random = new Random();
    
    // Desteklenen hisse senetleri ve statik bilgileri
    private static readonly Dictionary<string, StockInfo> _stocks = new Dictionary<string, StockInfo>
    {
        ["AAPL"] = new StockInfo { Symbol = "AAPL", CompanyName = "Apple Inc.", BasePrice = 175.50, PreviousClose = 173.75 },
        ["GOOGL"] = new StockInfo { Symbol = "GOOGL", CompanyName = "Alphabet Inc.", BasePrice = 134.25, PreviousClose = 133.20 },
        ["MSFT"] = new StockInfo { Symbol = "MSFT", CompanyName = "Microsoft Corporation", BasePrice = 325.80, PreviousClose = 323.50 },
        ["AMZN"] = new StockInfo { Symbol = "AMZN", CompanyName = "Amazon.com, Inc.", BasePrice = 128.90, PreviousClose = 127.15 },
        ["TSLA"] = new StockInfo { Symbol = "TSLA", CompanyName = "Tesla, Inc.", BasePrice = 245.75, PreviousClose = 242.30 }
    };
    
    // Aktif abonelikler
    private static readonly ConcurrentDictionary<string, IList<IServerStreamWriter<StockPriceUpdate>>> _subscriptions = 
        new ConcurrentDictionary<string, IList<IServerStreamWriter<StockPriceUpdate>>>();
    
    // Fiyat simülasyonu için timer
    private static readonly Timer _priceUpdateTimer;
    
    static StockService()
    {
        // Her 0.5 saniyede bir fiyat güncellemesi (500ms)
        _priceUpdateTimer = new Timer(UpdatePrices, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }
    
    public StockService(ILogger<StockService> logger)
    {
        _logger = logger;
        _logger.LogInformation("StockService başlatıldı. Desteklenen hisseler: {Symbols}", 
            string.Join(", ", _stocks.Keys));
    }
    
    public override Task<StockResponse> GetStock(StockRequest request, ServerCallContext context)
    {
        var symbol = request.Symbol.ToUpper();
        var clientIp = context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("[gRPC] GetStock isteği: Symbol={Symbol}, Client={ClientIp}, Time={Time}", 
            symbol, clientIp, DateTime.Now.ToString("HH:mm:ss.fff"));
        
        if (!_stocks.TryGetValue(symbol, out var stockInfo))
        {
            var errorMsg = $"Hisse senedi bulunamadı: {symbol}";
            _logger.LogWarning("[gRPC] GetStock hatası: {Error}, Client={ClientIp}", errorMsg, clientIp);
            throw new RpcException(new Status(StatusCode.NotFound, errorMsg));
        }
        
        // Geçerli fiyat ve değişim değerleri hesaplanır
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
        
        _logger.LogInformation("[gRPC] GetStock yanıtı: Symbol={Symbol}, Price={Price}, Change={Change}, Client={ClientIp}", 
            response.Symbol, response.Price, response.Change, clientIp);
        
        return Task.FromResult(response);
    }
    
    public override async Task StreamStockPrice(StockRequest request, IServerStreamWriter<StockPriceUpdate> responseStream, ServerCallContext context)
    {
        var symbol = request.Symbol.ToUpper();
        var clientIp = context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("[gRPC] StreamStockPrice bağlantısı: Symbol={Symbol}, Client={ClientIp}, Time={Time}", 
            symbol, clientIp, DateTime.Now.ToString("HH:mm:ss.fff"));
        
        if (!_stocks.ContainsKey(symbol))
        {
            var errorMsg = $"Hisse senedi bulunamadı: {symbol}";
            _logger.LogWarning("[gRPC] StreamStockPrice hatası: {Error}, Client={ClientIp}", errorMsg, clientIp);
            throw new RpcException(new Status(StatusCode.NotFound, errorMsg));
        }
        
        var streamList = _subscriptions.GetOrAdd(symbol, _ => new List<IServerStreamWriter<StockPriceUpdate>>());
        
        lock (streamList)
        {
            streamList.Add(responseStream);
        }
        
        _logger.LogInformation("[gRPC] Yeni hisse senedi akışı başlatıldı: Symbol={Symbol}, Client={ClientIp}, ActiveStreams={Count}", 
            symbol, clientIp, streamList.Count);
        
        try
        {
            // İstemci bağlantıyı kesene kadar bekle
            await Task.Delay(TimeSpan.FromHours(24), context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[gRPC] İstemci bağlantıyı kesti: Symbol={Symbol}, Client={ClientIp}", symbol, clientIp);
        }
        finally
        {
            // Abonelikten çık
            lock (streamList)
            {
                streamList.Remove(responseStream);
                _logger.LogInformation("[gRPC] Hisse senedi akışı sonlandırıldı: Symbol={Symbol}, Client={ClientIp}, RemainingStreams={Count}", 
                    symbol, clientIp, streamList.Count);
            }
        }
    }
    
    public override async Task StreamMultipleStocks(MultiStockRequest request, IServerStreamWriter<StockPriceUpdate> responseStream, ServerCallContext context)
    {
        var symbols = request.Symbols.Select(s => s.ToUpper()).ToList();
        var clientIp = context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        
        _logger.LogInformation("[gRPC] StreamMultipleStocks bağlantısı: Symbols={Symbols}, Client={ClientIp}, Time={Time}", 
            string.Join(", ", symbols), clientIp, DateTime.Now.ToString("HH:mm:ss.fff"));
        
        var invalidSymbols = symbols.Where(s => !_stocks.ContainsKey(s)).ToList();
        
        if (invalidSymbols.Any())
        {
            var errorMsg = $"Geçersiz semboller: {string.Join(", ", invalidSymbols)}";
            _logger.LogWarning("[gRPC] StreamMultipleStocks hatası: {Error}, Client={ClientIp}", errorMsg, clientIp);
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMsg));
        }
        
        foreach (var symbol in symbols)
        {
            var streamList = _subscriptions.GetOrAdd(symbol, _ => new List<IServerStreamWriter<StockPriceUpdate>>());
            
            lock (streamList)
            {
                streamList.Add(responseStream);
            }
            
            _logger.LogDebug("[gRPC] Sembol abone olundu: Symbol={Symbol}, Client={ClientIp}", symbol, clientIp);
        }
        
        _logger.LogInformation("[gRPC] Çoklu hisse senedi akışı başlatıldı: SymbolCount={Count}, Client={ClientIp}", 
            symbols.Count, clientIp);
        
        try
        {
            // İstemci bağlantıyı kesene kadar bekle
            await Task.Delay(TimeSpan.FromHours(24), context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[gRPC] İstemci çoklu akış bağlantısını kesti: Client={ClientIp}", clientIp);
        }
        finally
        {
            // Abonelikten çık
            foreach (var symbol in symbols)
            {
                if (_subscriptions.TryGetValue(symbol, out var streamList))
                {
                    lock (streamList)
                    {
                        streamList.Remove(responseStream);
                        _logger.LogDebug("[gRPC] Sembol aboneliği sonlandırıldı: Symbol={Symbol}, Client={ClientIp}, RemainingStreams={Count}", 
                            symbol, clientIp, streamList.Count);
                    }
                }
            }
            
            _logger.LogInformation("[gRPC] Çoklu hisse senedi akışı sonlandırıldı: SymbolCount={Count}, Client={ClientIp}", 
                symbols.Count, clientIp);
        }
    }
    
    private static async void UpdatePrices(object? state)
    {
        foreach (var stock in _stocks)
        {
            var symbol = stock.Key;
            var stockInfo = stock.Value;
            
            // Sinüs fonksiyonu ile gerçekçi fiyat dalgalanması
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
            
            // Tüm abone olmuş istemcilere güncellemeyi gönder
            if (_subscriptions.TryGetValue(symbol, out var streamList) && streamList.Count > 0)
            {
                List<IServerStreamWriter<StockPriceUpdate>> deadStreams = new();
                
                IServerStreamWriter<StockPriceUpdate>[] streamsToProcess;
                
                lock (streamList)
                {
                    streamsToProcess = streamList.ToArray();
                }
                
                // 10'dan fazla log üretmemek için sadece aktif stream sayısını logla
                if (streamList.Count > 0 && DateTime.Now.Second % 5 == 0 && DateTime.Now.Millisecond < 100)
                {
                    // Her 5 saniyede bir log yaz
                    Console.WriteLine($"[gRPC] {DateTime.Now:HH:mm:ss.fff} - {symbol} için aktif stream sayısı: {streamList.Count}");
                }
                
                foreach (var stream in streamsToProcess)
                {
                    try
                    {
                        await stream.WriteAsync(update);
                    }
                    catch (Exception ex)
                    {
                        // Bağlantısı kopmuş istemcileri işaretle
                        deadStreams.Add(stream);
                        Console.WriteLine($"[gRPC] {DateTime.Now:HH:mm:ss.fff} - Stream yazma hatası: {ex.Message}");
                    }
                }
                
                // Ölü akışları kaldır
                if (deadStreams.Count > 0)
                {
                    lock (streamList)
                    {
                        foreach (var deadStream in deadStreams)
                        {
                            streamList.Remove(deadStream);
                        }
                        Console.WriteLine($"[gRPC] {DateTime.Now:HH:mm:ss.fff} - {symbol} için {deadStreams.Count} ölü stream kaldırıldı, kalan: {streamList.Count}");
                    }
                }
            }
        }
    }
    
    private static string FormatMarketCap(double marketCap)
    {
        if (marketCap >= 1_000_000_000_000) // Trilyon
        {
            return $"{marketCap / 1_000_000_000_000:F2}T";
        }
        else if (marketCap >= 1_000_000_000) // Milyar
        {
            return $"{marketCap / 1_000_000_000:F2}B";
        }
        else // Milyon
        {
            return $"{marketCap / 1_000_000:F2}M";
        }
    }
    
    // Hisse senedi temel bilgileri için yardımcı sınıf
    private class StockInfo
    {
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public double BasePrice { get; set; }
        public double PreviousClose { get; set; }
        public long OutstandingShares { get; set; } = 1_000_000_000; // Varsayılan
    }
} 