// grpcStockService.ts
// NOT: Bu dosya, protoc tarafından oluşturulan dosyalara bağımlıdır
// Gerçek uygulamada "npm run proto:gen" komutunun çalıştırılması gerekir

/*
 * Bu dosya örnek amaçlıdır ve gerçek bir uygulama için tamamlanması gerekmektedir.
 * Protobuf dosyalarının oluşturulması ve gRPC-Web'in yapılandırılması gerekmektedir.
 */

import { GrpcWebFetchTransport } from 'grpc-web';
import { StockServiceClient } from '../protos/stock_grpc_web_pb';
import { StockRequest, MultiStockRequest, StockPriceUpdate } from '../protos/stock_pb';

// Stockservice için interface
export interface StockUpdate {
  symbol: string;
  price: number;
  change: number;
  changePercent: number;
  timestamp: number;
}

class GrpcStockService {
  private client: StockServiceClient;
  private activeStreams: Map<string, any> = new Map();

  constructor() {
    // gRPC istemcisini yapılandır
    const transport = new GrpcWebFetchTransport({
      baseUrl: '/grpc', // API Gateway yoluyla yönlendirilmiş adres
      debug: true, // Geliştirme modunda debug açık
    });

    this.client = new StockServiceClient(transport);
  }

  // Tek bir hisse senedi bilgisi al
  async getStockInfo(symbol: string): Promise<any> {
    const request = new StockRequest();
    request.setSymbol(symbol);

    try {
      const response = await this.client.getStock(request);
      return {
        symbol: response.getSymbol(),
        price: response.getPrice(),
        change: response.getChange(),
        volume: response.getVolume(),
        companyName: response.getCompanyName(),
        open: response.getOpen(),
        high: response.getHigh(),
        low: response.getLow(),
        previousClose: response.getPreviousClose(),
        marketCap: response.getMarketCap()
      };
    } catch (error) {
      console.error('Error fetching stock info:', error);
      throw error;
    }
  }

  // Tek bir hisse senedi için canlı fiyat akışı başlat
  streamStockPrice(symbol: string, onUpdate: (data: StockUpdate) => void, onError: (err: any) => void): () => void {
    if (this.activeStreams.has(symbol)) {
      console.log(`Stream for ${symbol} already exists. Reusing.`);
      return () => this.stopStream(symbol);
    }

    const request = new StockRequest();
    request.setSymbol(symbol);

    try {
      const stream = this.client.streamStockPrice(request);
      
      stream.on('data', (response: StockPriceUpdate) => {
        onUpdate({
          symbol: response.getSymbol(),
          price: response.getPrice(),
          change: response.getChange(),
          changePercent: response.getChangePercent(),
          timestamp: response.getTimestamp()
        });
      });

      stream.on('error', (err: any) => {
        onError(err);
        this.activeStreams.delete(symbol);
      });

      stream.on('end', () => {
        console.log(`Stream for ${symbol} ended`);
        this.activeStreams.delete(symbol);
      });

      this.activeStreams.set(symbol, stream);

      // Akışı durdurma fonksiyonunu döndür
      return () => this.stopStream(symbol);
    } catch (error) {
      console.error('Error starting stream:', error);
      throw error;
    }
  }

  // Birden fazla hisse senedi için canlı fiyat akışı başlat
  streamMultipleStocks(symbols: string[], onUpdate: (data: StockUpdate) => void, onError: (err: any) => void): () => void {
    const streamKey = symbols.sort().join(',');

    if (this.activeStreams.has(streamKey)) {
      console.log(`Stream for ${streamKey} already exists. Reusing.`);
      return () => this.stopStream(streamKey);
    }

    const request = new MultiStockRequest();
    request.setSymbolsList(symbols);

    try {
      const stream = this.client.streamMultipleStocks(request);
      
      stream.on('data', (response: StockPriceUpdate) => {
        onUpdate({
          symbol: response.getSymbol(),
          price: response.getPrice(),
          change: response.getChange(),
          changePercent: response.getChangePercent(),
          timestamp: response.getTimestamp()
        });
      });

      stream.on('error', (err: any) => {
        onError(err);
        this.activeStreams.delete(streamKey);
      });

      stream.on('end', () => {
        console.log(`Stream for ${streamKey} ended`);
        this.activeStreams.delete(streamKey);
      });

      this.activeStreams.set(streamKey, stream);

      // Akışı durdurma fonksiyonunu döndür
      return () => this.stopStream(streamKey);
    } catch (error) {
      console.error('Error starting multi-stream:', error);
      throw error;
    }
  }

  // Bir akışı durdur
  private stopStream(key: string): void {
    const stream = this.activeStreams.get(key);
    if (stream) {
      try {
        stream.cancel();
        console.log(`Stream for ${key} cancelled`);
      } catch (error) {
        console.error(`Error cancelling stream for ${key}:`, error);
      }
      this.activeStreams.delete(key);
    }
  }

  // Tüm akışları durdur
  stopAllStreams(): void {
    for (const key of this.activeStreams.keys()) {
      this.stopStream(key);
    }
  }
}

export default new GrpcStockService(); 