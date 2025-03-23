using Microsoft.AspNetCore.Mvc;
using StockMarket.GrpcService.Services;
using System.Text.Json;
using System.Text;

namespace StockMarket.GrpcService.Controllers;

[ApiController]
[Route("[controller]")]
public class StocksController : ControllerBase
{
    private readonly ILogger<StocksController> _logger;
    private readonly StockDataService _stockService;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public StocksController(ILogger<StocksController> logger, StockDataService stockService)
    {
        _logger = logger;
        _stockService = stockService;
    }

    [HttpGet]
    public async Task<IActionResult> GetStocks([FromQuery] string? symbols)
    {
        _logger.LogInformation("[REST] GetStocks isteği alındı. Semboller: {Symbols}", symbols ?? "tümü");
        
        try
        {
            var result = new List<object>();
            IEnumerable<string> symbolList;

            // "all" veya boş ise tüm sembolleri kullan
            if (string.IsNullOrEmpty(symbols) || symbols.Trim().ToLower() == "all")
            {
                symbolList = _stockService.GetAllSymbols();
                _logger.LogInformation("[REST] Tüm semboller alınıyor. Bulunan semboller: {Symbols}", string.Join(", ", symbolList));
            }
            else
            {
                symbolList = symbols.Split(',').Select(s => s.Trim().ToUpper()).Where(s => !string.IsNullOrEmpty(s));
                _logger.LogInformation("[REST] Belirtilen semboller alınıyor: {Symbols}", string.Join(", ", symbolList));
            }
            
            foreach (var symbol in symbolList)
            {
                try
                {
                    _logger.LogInformation("[REST] {Symbol} sembolü için veri alınıyor", symbol);
                    var stockData = await _stockService.GetStockPrice(symbol);
                    
                    // StockDto'yu API yanıtına dönüştür
                    result.Add(new
                    {
                        id = stockData.Symbol,
                        symbol = stockData.Symbol,
                        name = stockData.CompanyName,
                        price = stockData.Price,
                        change = stockData.Change,
                        percentChange = stockData.ChangePercent,
                        volume = stockData.Volume,
                        lastUpdated = stockData.LastUpdated.ToString("o")
                    });
                    _logger.LogInformation("[REST] {Symbol} sembolü için veri alındı: Fiyat={Price}, Değişim={Change}", symbol, stockData.Price, stockData.Change);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[REST] {Symbol} için hisse verisi alınırken hata: {Error}", symbol, ex.Message);
                }
            }
            
            _logger.LogInformation("[REST] GetStocks yanıtı gönderiliyor. {Count} hisse senedi.", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REST] GetStocks işlenirken hata oluştu");
            return StatusCode(500, new { error = "İstek işlenirken bir hata oluştu", details = ex.Message });
        }
    }

    [HttpGet("stream")]
    public async Task StreamStockPrices([FromQuery] string? symbols)
    {
        _logger.LogInformation("[REST] StreamStockPrices isteği alındı. Semboller: {Symbols}", symbols ?? "tümü");
        
        // SSE başlıklarını ayarla
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("Access-Control-Allow-Origin", Request.Headers["Origin"].ToString());
        Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        
        // Sembol listesini hazırla
        var symbolList = !string.IsNullOrEmpty(symbols)
            ? symbols.Split(',').Select(s => s.Trim().ToUpper()).Where(s => !string.IsNullOrEmpty(s)).ToList()
            : _stockService.GetAllSymbols().ToList();
        
        _logger.LogInformation("[REST] StreamStockPrices başlatıldı. Takip edilecek semboller: {Symbols}, Güncelleme frekansı: 500ms", 
            string.Join(", ", symbolList));
        
        // İstemci bağlantısını takip etmek için CancellationToken kullan
        var clientDisconnect = HttpContext.RequestAborted;
        
        // 500ms aralıklarla güncellemeler gönder
        while (!clientDisconnect.IsCancellationRequested)
        {
            try
            {
                foreach (var symbol in symbolList)
                {
                    var stockData = await _stockService.GetStockPrice(symbol);
                    
                    // SSE formatında hisse güncellemesi gönder
                    var stockUpdate = new
                    {
                        stock = new
                        {
                            id = stockData.Symbol,
                            symbol = stockData.Symbol,
                            name = stockData.CompanyName,
                            price = stockData.Price,
                            change = stockData.Change,
                            percentChange = stockData.ChangePercent,
                            volume = stockData.Volume,
                            lastUpdated = stockData.LastUpdated.ToString("o")
                        }
                    };
                    
                    var json = JsonSerializer.Serialize(stockUpdate, _jsonOptions);
                    var data = $"data: {json}\n\n";
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(data));
                    await Response.Body.FlushAsync();
                    _logger.LogInformation("[REST] Stock update sent: {Symbol} - {Price}", stockData.Symbol, stockData.Price);
                }
                
                await Task.Delay(500, clientDisconnect); // 0.5 saniye bekle (saniyenin yarısı)
            }
            catch (OperationCanceledException)
            {
                // İstemci bağlantıyı kesti
                _logger.LogInformation("[REST] Client disconnected from SSE stream");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REST] StreamStockPrices sırasında hata");
                break;
            }
        }
        
        _logger.LogInformation("[REST] StreamStockPrices bağlantısı sonlandırıldı");
    }
} 