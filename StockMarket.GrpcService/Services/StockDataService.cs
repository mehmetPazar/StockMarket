using System.Collections.Concurrent;

namespace StockMarket.GrpcService.Services;

public class StockDataService
{
    private readonly ILogger<StockDataService> _logger;
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
    
    public StockDataService(ILogger<StockDataService> logger)
    {
        _logger = logger;
    }
    
    public Task<StockInfo?> GetStockInfo(string symbol)
    {
        if (_stocks.TryGetValue(symbol.ToUpper(), out var stockInfo))
        {
            return Task.FromResult<StockInfo?>(stockInfo);
        }
        
        return Task.FromResult<StockInfo?>(null);
    }
    
    public Task<StockDto> GetStockPrice(string symbol)
    {
        symbol = symbol.ToUpper();
        
        if (!_stocks.TryGetValue(symbol, out var stockInfo))
        {
            throw new KeyNotFoundException($"Hisse senedi bulunamadı: {symbol}");
        }
        
        // Geçerli fiyat ve değişim değerleri hesaplanır
        var currentTime = DateTime.Now.Ticks / 10000000.0;
        
        // Daha belirgin değişimler için faktörü artırdık
        var noise = Math.Sin(currentTime) * 0.03 + 
                   Math.Sin(currentTime * 0.5) * 0.02 + 
                   ((_random.NextDouble() * 0.04) - 0.02); // -%2 ile +%2 arası rastgele değişim
        
        // Her sembol için hafif farklı değişim uygula (sembol bazlı tutarlı rastgelelik)
        var symbolFactor = symbol[0] % 5 * 0.01; // Sembolün ilk harfine göre 0 ile 0.04 arası ek faktör
        noise += symbolFactor * Math.Sin(currentTime * 0.7);
        
        var currentPrice = stockInfo.BasePrice * (1 + noise);
        var change = currentPrice - stockInfo.PreviousClose;
        var changePercent = (change / stockInfo.PreviousClose) * 100;
        
        _logger.LogInformation($"GetStockPrice: Symbol={symbol}, Price={Math.Round(currentPrice, 2)}, Change={Math.Round(change, 2)}, Noise={noise:F4}");
        
        return Task.FromResult(new StockDto
        {
            Symbol = stockInfo.Symbol,
            CompanyName = stockInfo.CompanyName,
            Price = Math.Round(currentPrice, 2),
            Change = Math.Round(change, 2),
            ChangePercent = Math.Round(changePercent, 2),
            Volume = _random.Next(100000, 10000000),
            Open = stockInfo.PreviousClose * (1 + (_random.NextDouble() * 0.01 - 0.005)),
            High = currentPrice * (1 + (_random.NextDouble() * 0.01)),
            Low = currentPrice * (1 - (_random.NextDouble() * 0.01)),
            PreviousClose = stockInfo.PreviousClose,
            MarketCap = FormatMarketCap(currentPrice * stockInfo.OutstandingShares),
            LastUpdated = DateTime.Now
        });
    }
    
    public IEnumerable<string> GetAllSymbols()
    {
        _logger.LogInformation($"GetAllSymbols çağrıldı, {_stocks.Keys.Count} sembol bulundu: {string.Join(", ", _stocks.Keys)}");
        return _stocks.Keys;
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
    public class StockInfo
    {
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public double BasePrice { get; set; }
        public double PreviousClose { get; set; }
        public long OutstandingShares { get; set; } = 1_000_000_000; // Varsayılan
    }
    
    // Hisse senedi verisi dönüş sınıfı
    public class StockDto
    {
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public double Price { get; set; }
        public double Change { get; set; }
        public double ChangePercent { get; set; }
        public long Volume { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double PreviousClose { get; set; }
        public string MarketCap { get; set; } = "";
        public DateTime LastUpdated { get; set; }
    }
} 