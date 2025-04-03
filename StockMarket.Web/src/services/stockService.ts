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

// Create gRPC service
const stockGrpcService = createStockService();

const stockService = {
  async getAllStocks(): Promise<Stock[]> {
    try {
      console.log('getAllStocks called, getting data via gRPC service');
      // Get stocks via gRPC service
      const stocks = await stockGrpcService.getStocks({});
      return stocks;
    } catch (error) {
      console.error('Error getting stocks:', error);
      throw error;
    }
  },

  async getStockBySymbol(symbol: string): Promise<Stock | undefined> {
    try {
      console.log(`getStockBySymbol called: ${symbol}`);
      // Get specific stock via gRPC service
      const stocks = await stockGrpcService.getStocks({ symbols: [symbol] });
      return stocks[0];
    } catch (error) {
      console.error(`Error getting stock for symbol ${symbol}:`, error);
      throw error;
    }
  },
  
  // Method for stream subscription
  subscribeToStockUpdates(symbols: string[], callback: (stock: Stock) => void): () => void {
    console.log(`Starting stream subscription, symbols: ${symbols.join(', ')}`);
    return stockGrpcService.subscribeToStockUpdates({ symbols }, (update) => {
      callback(update.stock);
    });
  }
};

export default stockService; 