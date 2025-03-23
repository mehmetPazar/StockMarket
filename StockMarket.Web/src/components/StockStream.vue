<template>
  <div class="stock-stream">
    <h2>Canlı Hisse Takibi (gRPC)</h2>
    
    <div class="controls">
      <div class="symbol-select">
        <label for="stockSelect">Takip Edilecek Hisseler:</label>
        <div class="select-group">
          <select id="stockSelect" v-model="selectedSymbol">
            <option v-for="stock in availableStocks" :key="stock" :value="stock">{{ stock }}</option>
          </select>
          <button @click="addSymbol" :disabled="!selectedSymbol || watchedSymbols.includes(selectedSymbol)">
            Ekle
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
      Bağlanıyor...
    </div>
    <div v-else-if="connectionStatus === 'error'" class="status error">
      Bağlantı hatası. Lütfen tekrar deneyin.
    </div>
    <div v-else-if="connectionStatus === 'connected' && watchedSymbols.length === 0" class="status empty">
      Lütfen takip etmek istediğiniz hisseleri ekleyin.
    </div>
    
    <div class="stock-table-container" v-else>
      <table class="stock-table">
        <thead>
          <tr>
            <th>Sembol</th>
            <th>Fiyat</th>
            <th>Değişim</th>
            <th>Değişim %</th>
            <th>Son Güncelleme</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(stock, symbol) in stockUpdates" :key="symbol" :class="{ 'up': stock.change > 0, 'down': stock.change < 0 }">
            <td>{{ symbol }}</td>
            <td>{{ stock.price.toFixed(2) }} TL</td>
            <td>{{ stock.change.toFixed(2) }} TL</td>
            <td>{{ stock.changePercent.toFixed(2) }}%</td>
            <td>{{ formatTime(stock.timestamp) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Fiyat Grafik Bileşeni -->
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

// Grafik bileşeni lazy loading ile yükleniyor
const StockPriceChart = defineAsyncComponent(() => 
  import('./charts/StockPriceChart.vue')
);

defineOptions({
  name: 'StockStream'
});

// Kullanılabilir hisse senetleri
const availableStocks = ['AAPL', 'MSFT', 'GOOGL', 'AMZN', 'TSLA'];
const selectedSymbol = ref('');
const watchedSymbols = ref<string[]>([]);
const connectionStatus = ref<'connecting' | 'connected' | 'error'>('connecting');

// Stock verileri için tip tanımı
interface StockData {
  price: number;
  change: number;
  changePercent: number;
  timestamp: number;
}

const stockUpdates = ref<Record<string, StockData>>({});
let unsubscribeFn: (() => void) | null = null;

// Sembolleri izleme listesine ekle
const addSymbol = () => {
  if (selectedSymbol.value && !watchedSymbols.value.includes(selectedSymbol.value)) {
    console.log(`[Vue] Yeni sembol ekleniyor: ${selectedSymbol.value}`);
    watchedSymbols.value.push(selectedSymbol.value);
    updateSubscription();
    selectedSymbol.value = '';
  }
};

// Sembolleri izleme listesinden çıkar
const removeSymbol = (symbol: string) => {
  console.log(`[Vue] Sembol kaldırılıyor: ${symbol}`);
  watchedSymbols.value = watchedSymbols.value.filter(s => s !== symbol);
  
  // Stock güncellemelerinden kaldır
  const updates = { ...stockUpdates.value };
  delete updates[symbol];
  stockUpdates.value = updates;
  
  // Aboneliği güncelle
  updateSubscription();
};

// gRPC aboneliğini güncelle
const updateSubscription = () => {
  // Mevcut aboneliği temizle
  if (unsubscribeFn) {
    console.log('[Vue] Mevcut hisse akışı aboneliği sonlandırılıyor');
    unsubscribeFn();
    unsubscribeFn = null;
  }
  
  // İzlenen sembol yoksa aboneliği sonlandır
  if (watchedSymbols.value.length === 0) {
    console.log('[Vue] İzlenen sembol olmadığı için abonelik oluşturulmadı');
    return;
  }
  
  // Yeni abonelik oluştur
  console.log(`[Vue] Yeni hisse akışı aboneliği başlatılıyor: ${watchedSymbols.value.join(', ')}`);
  
  // Abonelik başlangıcında tüm hisseleri al
  stockService.getAllStocks().then(stocks => {
    console.log(`[Vue] İlk hisse verileri alındı: ${stocks.length} adet hisse`);
    // Sadece izlenen sembolleri filtrele
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
  
  // Canlı güncellemelere abone ol
  unsubscribeFn = stockService.subscribeToStockUpdates(watchedSymbols.value, (stock) => {
    console.log(`[Vue] Hisse güncellemesi alındı: ${stock.symbol}, fiyat: ${stock.price.toFixed(2)}, değişim: ${stock.change.toFixed(2)}`);
    
    // Reaktif güncelleme sorunlarını önlemek için kopya oluştur
    const updatedStocks = { ...stockUpdates.value };
    updatedStocks[stock.symbol] = {
      price: stock.price,
      change: stock.change,
      changePercent: stock.percentChange,
      timestamp: new Date(stock.lastUpdated).getTime() / 1000
    };
    
    // Tek seferde güncelle
    stockUpdates.value = updatedStocks;
  });
};

// Zaman formatlama
const formatTime = (timestamp: number): string => {
  const date = new Date(timestamp * 1000);
  const hours = date.getHours().toString().padStart(2, '0');
  const minutes = date.getMinutes().toString().padStart(2, '0');
  const seconds = date.getSeconds().toString().padStart(2, '0');
  return `${hours}:${minutes}:${seconds}`;
};

// Component temizliği
onUnmounted(() => {
  console.log('[Vue] StockStream bileşeni kaldırılıyor, abonelikler sonlandırılıyor');
  if (unsubscribeFn) {
    unsubscribeFn();
    unsubscribeFn = null;
  }
});

// İlk yükleme sırasında bağlantı durumunu simüle et
onMounted(() => {
  console.log('[Vue] StockStream bileşeni yüklendi, bağlantı başlatılıyor');
  setTimeout(() => {
    console.log('[Vue] Bağlantı durumu "connected" olarak güncellendi');
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
  background-color: #95a5a6;
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
  background-color: #f8f9fa;
  padding: 0.5rem 0.75rem;
  border-radius: 4px;
  border: 1px solid #ddd;
}

.remove-btn {
  background: none;
  border: none;
  color: #e74c3c;
  margin-left: 0.5rem;
  cursor: pointer;
  font-size: 1.25rem;
  font-weight: bold;
  padding: 0 0.25rem;
}

.status {
  padding: 1rem;
  border-radius: 4px;
  margin-bottom: 1rem;
  text-align: center;
}

.connecting {
  background-color: #fef9e7;
  color: #7e5c00;
}

.error {
  background-color: #fdedec;
  color: #c0392b;
}

.empty {
  background-color: #eaf2f8;
  color: #2980b9;
}

.stock-table-container {
  overflow-x: auto;
}

.stock-table {
  width: 100%;
  border-collapse: collapse;
}

.stock-table th, .stock-table td {
  padding: 0.75rem 1rem;
  text-align: left;
  border-bottom: 1px solid #ddd;
}

.stock-table th {
  background-color: #f8f9fa;
  font-weight: bold;
  color: #2c3e50;
}

.stock-table tr:last-child td {
  border-bottom: none;
}

.up {
  color: #27ae60;
}

.down {
  color: #e74c3c;
}

.charts-container {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1rem;
  margin-top: 2rem;
}

@media (min-width: 768px) {
  .charts-container {
    grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
  }
}

.stock-chart-item {
  min-height: 300px;
}
</style> 