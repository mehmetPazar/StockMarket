<template>
  <div class="stock-stream">
    <h2>Live Stock Tracking (gRPC)</h2>
    
    <div class="controls">
      <div class="symbol-select">
        <label for="stockSelect">Stocks to Track:</label>
        <div class="select-group">
          <select id="stockSelect" v-model="selectedSymbol">
            <option v-for="stock in availableStocks" :key="stock" :value="stock">{{ stock }}</option>
          </select>
          <button @click="addSymbol" :disabled="!selectedSymbol || watchedSymbols.includes(selectedSymbol)">
            Add
          </button>
        </div>
      </div>
      
      <div class="watched-symbols">
        <div v-for="symbol in watchedSymbols" :key="symbol" class="watched-symbol">
          {{ symbol }}
          <button @click="removeSymbol(symbol)" class="remove-btn">&times;</button>
        </div>
      </div>
    </div>
    
    <div v-if="connectionStatus === 'connecting'" class="status connecting">
      Connecting...
    </div>
    <div v-else-if="connectionStatus === 'error'" class="status error">
      Connection error. Please try again.
    </div>
    <div v-else-if="connectionStatus === 'connected' && watchedSymbols.length === 0" class="status empty">
      Please add stocks you want to track.
    </div>
    
    <div class="stock-table-container" v-else>
      <table class="stock-table">
        <thead>
          <tr>
            <th>Symbol</th>
            <th>Price</th>
            <th>Change</th>
            <th>Change %</th>
            <th>Last Update</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(stock, symbol) in stockUpdates" :key="symbol" :class="{ 'up': stock.change > 0, 'down': stock.change < 0 }">
            <td>{{ symbol }}</td>
            <td>${{ stock.price.toFixed(2) }}</td>
            <td>${{ stock.change.toFixed(2) }}</td>
            <td>{{ stock.changePercent.toFixed(2) }}%</td>
            <td>{{ formatTime(stock.timestamp) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Price Chart Component -->
    <div v-if="watchedSymbols.length > 0" class="charts-container">
      <StockPriceChart 
        v-for="symbol in watchedSymbols" 
        :key="`chart-${symbol}`" 
        :symbol="symbol" 
        class="stock-chart-item"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, defineAsyncComponent } from 'vue';
import stockService from '../services/stockService';
import type { Stock } from '../services/stockService';

// Load chart component lazily
const StockPriceChart = defineAsyncComponent(() => 
  import('./charts/StockPriceChart.vue')
);

defineOptions({
  name: 'StockStream'
});

// Available stocks
const availableStocks = ['AAPL', 'MSFT', 'GOOGL', 'AMZN', 'TSLA'];
const selectedSymbol = ref('');
const watchedSymbols = ref<string[]>([]);
const connectionStatus = ref<'connecting' | 'connected' | 'error'>('connecting');

// Type definition for stock data
interface StockData {
  price: number;
  change: number;
  changePercent: number;
  timestamp: number;
}

const stockUpdates = ref<Record<string, StockData>>({});
let unsubscribeFn: (() => void) | null = null;

// Add symbols to watch list
const addSymbol = () => {
  if (selectedSymbol.value && !watchedSymbols.value.includes(selectedSymbol.value)) {
    console.log(`[Vue] Adding new symbol: ${selectedSymbol.value}`);
    watchedSymbols.value.push(selectedSymbol.value);
    updateSubscription();
    selectedSymbol.value = '';
  }
};

// Remove symbols from watch list
const removeSymbol = (symbol: string) => {
  console.log(`[Vue] Removing symbol: ${symbol}`);
  watchedSymbols.value = watchedSymbols.value.filter(s => s !== symbol);
  
  // Remove from stock updates
  const updates = { ...stockUpdates.value };
  delete updates[symbol];
  stockUpdates.value = updates;
  
  // Update subscription
  updateSubscription();
};

// Update gRPC subscription
const updateSubscription = () => {
  // Clear existing subscription
  if (unsubscribeFn) {
    console.log('[Vue] Ending current stock stream subscription');
    unsubscribeFn();
    unsubscribeFn = null;
  }
  
  // End subscription if no symbols are watched
  if (watchedSymbols.value.length === 0) {
    console.log('[Vue] No subscription created as no symbols are being watched');
    return;
  }
  
  // Create new subscription
  console.log(`[Vue] Starting new stock stream subscription: ${watchedSymbols.value.join(', ')}`);
  
  // Get all stocks at subscription start
  stockService.getAllStocks().then(stocks => {
    console.log(`[Vue] Initial stock data received: ${stocks.length} stocks`);
    // Filter only watched symbols
    const filteredStocks = stocks.filter(stock => watchedSymbols.value.includes(stock.symbol));
    
    filteredStocks.forEach(stockData => {
      stockUpdates.value[stockData.symbol] = {
        price: stockData.price,
        change: stockData.change,
        changePercent: stockData.percentChange,
        timestamp: new Date(stockData.lastUpdated).getTime() / 1000
      };
    });
  });
  
  // Subscribe to live updates
  unsubscribeFn = stockService.subscribeToStockUpdates(watchedSymbols.value, (stock) => {
    console.log(`[Vue] Stock update received: ${stock.symbol}, price: ${stock.price.toFixed(2)}, change: ${stock.change.toFixed(2)}`);
    
    // Create copy to prevent reactive update issues
    const updatedStocks = { ...stockUpdates.value };
    updatedStocks[stock.symbol] = {
      price: stock.price,
      change: stock.change,
      changePercent: stock.percentChange,
      timestamp: new Date(stock.lastUpdated).getTime() / 1000
    };
    
    // Update all at once
    stockUpdates.value = updatedStocks;
  });
};

// Time formatting
const formatTime = (timestamp: number): string => {
  const date = new Date(timestamp * 1000);
  const hours = date.getHours().toString().padStart(2, '0');
  const minutes = date.getMinutes().toString().padStart(2, '0');
  const seconds = date.getSeconds().toString().padStart(2, '0');
  return `${hours}:${minutes}:${seconds}`;
};

// Component cleanup
onUnmounted(() => {
  console.log('[Vue] StockStream component unmounting, cleaning up subscriptions');
  if (unsubscribeFn) {
    unsubscribeFn();
    unsubscribeFn = null;
  }
});

// Simulate connection status during initial load
onMounted(() => {
  console.log('[Vue] StockStream component mounted, initializing connection');
  setTimeout(() => {
    console.log('[Vue] Connection status updated to "connected"');
    connectionStatus.value = 'connected';
  }, 1000);
});
</script>

<style scoped>
.stock-stream {
  margin-top: 2rem;
  background-color: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
}

h2 {
  margin-top: 0;
  margin-bottom: 1.5rem;
  color: #2c3e50;
}

.controls {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.symbol-select {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.select-group {
  display: flex;
  gap: 0.5rem;
}

select, button {
  padding: 0.5rem 0.75rem;
  border-radius: 4px;
  border: 1px solid #ddd;
}

button {
  background-color: #3498db;
  color: white;
  border: none;
  cursor: pointer;
  transition: background-color 0.2s;
}

button:hover:not(:disabled) {
  background-color: #2980b9;
}

button:disabled {
  background-color: #bdc3c7;
  cursor: not-allowed;
}

.watched-symbols {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.watched-symbol {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.25rem 0.5rem;
  background-color: #f8f9fa;
  border-radius: 4px;
  border: 1px solid #ddd;
}

.remove-btn {
  padding: 0;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: transparent;
  color: #666;
  font-size: 1.2rem;
  border: none;
}

.remove-btn:hover {
  color: #e74c3c;
  background-color: transparent;
}

.status {
  padding: 1rem;
  border-radius: 4px;
  margin-bottom: 1rem;
  text-align: center;
}

.connecting {
  background-color: #f8f9fa;
  color: #666;
}

.error {
  background-color: #fee;
  color: #e74c3c;
}

.empty {
  background-color: #f8f9fa;
  color: #666;
}

.stock-table-container {
  margin-top: 1rem;
  overflow-x: auto;
}

.stock-table {
  width: 100%;
  border-collapse: collapse;
}

.stock-table th,
.stock-table td {
  padding: 0.75rem;
  text-align: left;
  border-bottom: 1px solid #ddd;
}

.stock-table th {
  background-color: #f8f9fa;
  font-weight: bold;
}

.up {
  color: #27ae60;
}

.down {
  color: #e74c3c;
}

.charts-container {
  margin-top: 2rem;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1rem;
}

.stock-chart-item {
  background-color: #f8f9fa;
  padding: 1rem;
  border-radius: 4px;
  border: 1px solid #ddd;
}

@media (max-width: 768px) {
  .stock-table {
    font-size: 0.9rem;
  }
  
  .stock-table th,
  .stock-table td {
    padding: 0.5rem;
  }
}
</style> 