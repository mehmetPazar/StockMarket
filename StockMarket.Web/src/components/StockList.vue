<template>
  <div class="stock-list">
    <h2>Stocks</h2>
    
    <div class="loader" v-if="loading">
      <div class="spinner"></div>
      <p>Loading stock data...</p>
    </div>
    
    <div class="error" v-else-if="error">
      <h3>Connection Error</h3>
      <p>{{ error }}</p>
      <button @click="fetchStocks" class="retry-button">Retry</button>
    </div>
    
    <div class="empty-state" v-else-if="stocks.length === 0">
      <p>No stock data found yet.</p>
      <button @click="fetchStocks" class="retry-button">Refresh</button>
    </div>
    
    <table v-else>
      <thead>
        <tr>
          <th>Symbol</th>
          <th>Company</th>
          <th>Price ($)</th>
          <th>Change (%)</th>
          <th>Volume</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="stock in stocks" :key="stock.symbol" :class="{ 'up': stock.change > 0, 'down': stock.change < 0 }">
          <td>{{ stock.symbol }}</td>
          <td>{{ stock.name }}</td>
          <td>{{ stock.price.toFixed(2) }}</td>
          <td>
            <span class="change-value">{{ stock.change.toFixed(2) }} ({{ stock.percentChange.toFixed(2) }}%)</span>
          </td>
          <td>{{ formatVolume(stock.volume) }}</td>
        </tr>
      </tbody>
    </table>
    
    <div class="refresh-container" v-if="stocks.length > 0">
      <p>Last update: {{ lastUpdateTime }}</p>
      <button @click="fetchStocks" class="refresh-button">
        <span>Refresh</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import stockService from '../services/stockService';
import type { Stock } from '../services/stockService';

defineOptions({
  name: 'StockList'
});

const stocks = ref<Stock[]>([]);
const loading = ref(true);
const error = ref<string | null>(null);
const lastUpdate = ref<Date>(new Date());
let unsubscribe: (() => void) | null = null;

// Human-readable last update time
const lastUpdateTime = computed(() => {
  const now = new Date();
  const diff = Math.floor((now.getTime() - lastUpdate.value.getTime()) / 1000);
  
  if (diff < 60) return `${diff} seconds ago`;
  if (diff < 3600) return `${Math.floor(diff / 60)} minutes ago`;
  return lastUpdate.value.toLocaleTimeString();
});

const fetchStocks = async () => {
  try {
    loading.value = true;
    error.value = null;
    
    console.log('StockList: Fetching stocks...');
    stocks.value = await stockService.getAllStocks();
    lastUpdate.value = new Date();
    
    console.log(`StockList: Received ${stocks.value.length} stocks`);
    
    // Subscribe to real-time updates
    if (stocks.value.length > 0) {
      const symbols = stocks.value.map(stock => stock.symbol);
      subscribeToUpdates(symbols);
    }
  } catch (err) {
    console.error('StockList: Failed to fetch stock data', err);
    error.value = 'An error occurred while loading stock data. Please check server connection.';
  } finally {
    loading.value = false;
  }
};

const subscribeToUpdates = (symbols: string[]) => {
  // Clear previous subscription
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  
  console.log(`StockList: Subscribing to real-time updates for ${symbols.length} symbols`);
  unsubscribe = stockService.subscribeToStockUpdates(symbols, (updatedStock) => {
    console.log(`StockList: Received update for ${updatedStock.symbol}`);
    lastUpdate.value = new Date();
    
    // Update stock in the list
    const index = stocks.value.findIndex(stock => stock.symbol === updatedStock.symbol);
    if (index !== -1) {
      stocks.value[index] = updatedStock;
    } else {
      // Add if not in list
      stocks.value.push(updatedStock);
    }
  });
};

onMounted(() => {
  fetchStocks();
  
  // Auto-refresh every 60 seconds
  const refreshInterval = setInterval(() => {
    const timeSinceLastUpdate = (new Date().getTime() - lastUpdate.value.getTime()) / 1000;
    
    // If no updates received in last 30 seconds
    if (timeSinceLastUpdate > 30) {
      console.log('StockList: No updates received for a while, refreshing...');
      fetchStocks();
    }
  }, 60 * 1000);
  
  onUnmounted(() => {
    clearInterval(refreshInterval);
    if (unsubscribe) {
      console.log('StockList: Cleaning up subscriptions');
      unsubscribe();
      unsubscribe = null;
    }
  });
});

const formatVolume = (volume: number): string => {
  if (volume >= 1000000) {
    return (volume / 1000000).toFixed(2) + 'M';
  } else if (volume >= 1000) {
    return (volume / 1000).toFixed(2) + 'K';
  } else {
    return volume.toString();
  }
};
</script>

<style scoped>
.stock-list {
  margin-top: 2rem;
}

table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 1rem;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
}

th, td {
  padding: 1rem;
  text-align: left;
  border-bottom: 1px solid #ddd;
}

th {
  background-color: #f8f9fa;
  font-weight: bold;
}

tr:hover {
  background-color: #f5f5f5;
}

.up {
  color: green;
}

.down {
  color: red;
}

.change-value {
  font-weight: bold;
}

.loader {
  text-align: center;
  padding: 2rem;
  font-style: italic;
  color: #666;
}

.error {
  color: red;
  padding: 1rem;
  border: 1px solid red;
  background-color: #ffebee;
  border-radius: 4px;
}

.retry-button {
  background-color: #007bff;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  margin-top: 1rem;
}

.retry-button:hover {
  background-color: #0056b3;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  font-style: italic;
  color: #666;
}

.refresh-container {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 1rem;
  padding-top: 0.5rem;
  border-top: 1px solid #ddd;
  font-size: 0.9rem;
  color: #666;
}

.refresh-button {
  background-color: #007bff;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.refresh-button:hover {
  background-color: #0056b3;
}

.spinner {
  width: 40px;
  height: 40px;
  margin: 0 auto 1rem;
  border: 4px solid #f3f3f3;
  border-top: 4px solid #3498db;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style> 