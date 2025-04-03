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
        _logger.LogInformation("[REST] GetStocks request received. Symbols: {Symbols}", symbols ?? "all");
        
        try
        {
            var result = new List<object>();
            IEnumerable<string> symbolList;

            // Use all symbols if "all" or empty
            if (string.IsNullOrEmpty(symbols) || symbols.Trim().ToLower() == "all")
            {
                symbolList = _stockService.GetAllSymbols();
                _logger.LogInformation("[REST] Getting all symbols. Found symbols: {Symbols}", string.Join(", ", symbolList));
            }
            else
            {
                symbolList = symbols.Split(',').Select(s => s.Trim().ToUpper()).Where(s => !string.IsNullOrEmpty(s));
                _logger.LogInformation("[REST] Getting specified symbols: {Symbols}", string.Join(", ", symbolList));
            }
            
            foreach (var symbol in symbolList)
            {
                try
                {
                    _logger.LogInformation("[REST] Getting data for symbol {Symbol}", symbol);
                    var stockData = await _stockService.GetStockPrice(symbol);
                    
                    // Convert StockDto to API response
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
                    _logger.LogInformation("[REST] Data retrieved for symbol {Symbol}: Price={Price}, Change={Change}", symbol, stockData.Price, stockData.Change);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[REST] Error getting stock data for {Symbol}: {Error}", symbol, ex.Message);
                }
            }
            
            _logger.LogInformation("[REST] Sending GetStocks response. {Count} stocks.", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REST] Error processing GetStocks");
            return StatusCode(500, new { error = "An error occurred while processing the request", details = ex.Message });
        }
    }

    [HttpGet("stream")]
    public async Task StreamStockPrices([FromQuery] string? symbols)
    {
        _logger.LogInformation("[REST] StreamStockPrices request received. Symbols: {Symbols}", symbols ?? "all");
        
        // Set SSE headers
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("Access-Control-Allow-Origin", Request.Headers["Origin"].ToString());
        Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        
        // Prepare symbol list
        var symbolList = !string.IsNullOrEmpty(symbols)
            ? symbols.Split(',').Select(s => s.Trim().ToUpper()).Where(s => !string.IsNullOrEmpty(s)).ToList()
            : _stockService.GetAllSymbols().ToList();
        
        _logger.LogInformation("[REST] StreamStockPrices started. Symbols to track: {Symbols}, Update frequency: 500ms", 
            string.Join(", ", symbolList));
        
        // Use CancellationToken to track client connection
        var clientDisconnect = HttpContext.RequestAborted;
        
        // Send updates at 500ms intervals
        while (!clientDisconnect.IsCancellationRequested)
        {
            try
            {
                foreach (var symbol in symbolList)
                {
                    var stockData = await _stockService.GetStockPrice(symbol);
                    
                    // Send stock update in SSE format
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
                
                await Task.Delay(500, clientDisconnect); // Wait 0.5 seconds (half a second)
            }
            catch (OperationCanceledException)
            {
                // Client disconnected
                _logger.LogInformation("[REST] Client disconnected from SSE stream");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REST] Error during StreamStockPrices");
                break;
            }
        }
        
        _logger.LogInformation("[REST] StreamStockPrices connection terminated");
    }
} 