<template>
  <div class="stock-list">
    <h2>Hisse Senetleri</h2>
    
    <div class="loader" v-if="loading">
      <div class="spinner"></div>
      <p>Hisse senedi verileri yükleniyor...</p>
    </div>
    
    <div class="error" v-else-if="error">
      <h3>Bağlantı Hatası</h3>
      <p>{{ error }}</p>
      <button @click="fetchStocks" class="retry-button">Tekrar Dene</button>
    </div>
    
    <div class="empty-state" v-else-if="stocks.length === 0">
      <p>Henüz hiç hisse senedi verisi bulunamadı.</p>
      <button @click="fetchStocks" class="retry-button">Yenile</button>
    </div>
    
    <table v-else>
      <thead>
        <tr>
          <th>Sembol</th>
          <th>Şirket</th>
          <th>Fiyat (TL)</th>
          <th>Değişim (%)</th>
          <th>Hacim</th>
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
      <p>Son güncelleme: {{ lastUpdateTime }}</p>
      <button @click="fetchStocks" class="refresh-button">
        <span>Yenile</span>
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

// İnsan tarafından okunabilir son güncelleme zamanı
const lastUpdateTime = computed(() => {
  const now = new Date();
  const diff = Math.floor((now.getTime() - lastUpdate.value.getTime()) / 1000);
  
  if (diff < 60) return `${diff} saniye önce`;
  if (diff < 3600) return `${Math.floor(diff / 60)} dakika önce`;
  return lastUpdate.value.toLocaleTimeString();
});

const fetchStocks = async () => {
  try {
    loading.value = true;
    error.value = null;
    
    console.log('StockList: Hisse senetleri alınıyor...');
    stocks.value = await stockService.getAllStocks();
    lastUpdate.value = new Date();
    
    console.log(`StockList: ${stocks.value.length} hisse senedi alındı`);
    
    // Gerçek zamanlı güncellemeler için abone ol
    if (stocks.value.length > 0) {
      const symbols = stocks.value.map(stock => stock.symbol);
      subscribeToUpdates(symbols);
    }
  } catch (err) {
    console.error('StockList: Hisse senedi verileri alınamadı', err);
    error.value = 'Hisse senedi verileri yüklenirken bir hata oluştu. Sunucu bağlantısını kontrol edin.';
  } finally {
    loading.value = false;
  }
};

const subscribeToUpdates = (symbols: string[]) => {
  // Önceki aboneliği temizle
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  
  console.log(`StockList: ${symbols.length} sembol için gerçek zamanlı güncellemelere abone olunuyor`);
  unsubscribe = stockService.subscribeToStockUpdates(symbols, (updatedStock) => {
    console.log(`StockList: ${updatedStock.symbol} için güncelleme alındı`);
    lastUpdate.value = new Date();
    
    // Listedeki hisseyi güncelle
    const index = stocks.value.findIndex(stock => stock.symbol === updatedStock.symbol);
    if (index !== -1) {
      stocks.value[index] = updatedStock;
    } else {
      // Eğer listede yoksa ekle
      stocks.value.push(updatedStock);
    }
  });
};

onMounted(() => {
  fetchStocks();
  
  // 60 saniyede bir otomatik yenileme
  const refreshInterval = setInterval(() => {
    const timeSinceLastUpdate = (new Date().getTime() - lastUpdate.value.getTime()) / 1000;
    
    // Son 30 saniyedir güncelleme alınmadıysa
    if (timeSinceLastUpdate > 30) {
      console.log('StockList: Uzun süredir güncelleme alınamadı, yenileniyor...');
      fetchStocks();
    }
  }, 60 * 1000);
  
  onUnmounted(() => {
    clearInterval(refreshInterval);
    if (unsubscribe) {
      console.log('StockList: Abonelikler sonlandırılıyor');
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
}

.refresh-button:hover {
  background-color: #0056b3;
}
</style> 