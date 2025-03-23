import axios from 'axios';
import { createStockService } from '@/protos/stock';
import type { Stock as GrpcStock } from '@/protos/stock';

export interface Stock {
  id: string;
  symbol: string;
  name: string;
  price: number;
  change: number;
  percentChange: number;
  volume: number;
  lastUpdated: string;
}

// gRPC servisi oluştur
const stockGrpcService = createStockService();

const stockService = {
  async getAllStocks(): Promise<Stock[]> {
    try {
      console.log('getAllStocks çağrıldı, gRPC servisi ile veri alınıyor');
      // gRPC servisi üzerinden stokları al
      const stocks = await stockGrpcService.getStocks({});
      return stocks;
    } catch (error) {
      console.error('Hisse senetleri alınırken hata oluştu:', error);
      throw error;
    }
  },

  async getStockBySymbol(symbol: string): Promise<Stock | undefined> {
    try {
      console.log(`getStockBySymbol çağrıldı: ${symbol}`);
      // gRPC servisi üzerinden belirli bir hisseyi al
      const stocks = await stockGrpcService.getStocks({ symbols: [symbol] });
      return stocks[0];
    } catch (error) {
      console.error(`${symbol} sembolü için hisse senedi alınırken hata oluştu:`, error);
      throw error;
    }
  },
  
  // Stream aboneliği için metod
  subscribeToStockUpdates(symbols: string[], callback: (stock: Stock) => void): () => void {
    console.log(`Stream aboneliği başlatılıyor, semboller: ${symbols.join(', ')}`);
    return stockGrpcService.subscribeToStockUpdates({ symbols }, (update) => {
      callback(update.stock);
    });
  }
};

export default stockService; 