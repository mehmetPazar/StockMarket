<template>
  <div class="chart-container">
    <h3>{{ symbol }} Fiyat Grafiği</h3>
    <div v-if="loading" class="loading">Grafik yükleniyor...</div>
    <div v-else class="canvas-container">
      <canvas ref="chartCanvas"></canvas>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, defineProps, toRefs, nextTick } from 'vue';
import { Chart, registerables } from 'chart.js';
import stockService from '../../services/stockService';
import type { Stock } from '../../services/stockService';

// Chart.js registerables'ları kaydet
Chart.register(...registerables);

// Component props tanımlama
const props = defineProps<{
  symbol: string;
}>();

const { symbol } = toRefs(props);

// Chart ve UI referansları
const chartCanvas = ref<HTMLCanvasElement | null>(null);
const loading = ref(true);
let chartInstance: Chart | null = null; // Reaktif olmayan chart referansı

// Veri noktaları için state - mutable
const priceData = ref<number[]>([]);
const timeLabels = ref<string[]>([]);
const maxDataPoints = 30; // Grafikte gösterilecek maksimum veri noktası

// Unsubscribe fonksiyonu
let unsubscribe: (() => void) | null = null;

// Grafik yeniden oluşturma fonksiyonu - daha basit ve güvenli
const renderChart = async () => {
  // Canvas hazır değilse bekleme
  if (!chartCanvas.value) {
    console.warn('Canvas henüz hazır değil');
    return;
  }
  
  // Veri yoksa bekleme
  if (priceData.value.length === 0) {
    console.log('Grafik verisi henüz yok');
    return;
  }
  
  // Önceki grafiği temizle
  if (chartInstance) {
    try {
      chartInstance.destroy();
    } catch (error) {
      console.error('Grafik temizleme hatası:', error);
    }
    chartInstance = null;
  }
  
  // DOM güncellemesi için bekle
  await nextTick();
  
  try {
    // Değişmez veri kopyaları oluştur
    const labels = [...timeLabels.value];
    const prices = [...priceData.value];
    
    const ctx = chartCanvas.value.getContext('2d');
    if (!ctx) {
      console.error('Canvas context alınamadı');
      return;
    }
    
    // Yeni chart oluştur
    chartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [{
          label: `${symbol.value} Fiyat (TL)`,
          data: prices,
          borderColor: '#3498db',
          backgroundColor: 'rgba(52, 152, 219, 0.1)',
          borderWidth: 2,
          tension: 0.3,
          pointRadius: 3,
          pointBackgroundColor: '#2980b9',
          fill: true
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          y: {
            beginAtZero: false,
            ticks: {
              callback: function(value) {
                return value + ' TL';
              }
            }
          },
          x: {
            grid: {
              display: false
            }
          }
        },
        plugins: {
          tooltip: {
            callbacks: {
              label: function(context) {
                return 'Fiyat: ' + context.raw + ' TL';
              }
            }
          },
          legend: {
            display: true,
            position: 'top'
          }
        },
        animation: {
          duration: 500
        }
      }
    });
    
    console.log('Grafik başarıyla oluşturuldu');
  } catch (error) {
    console.error('Grafik oluşturma hatası:', error);
  }
};

// Veri noktası ekleme - basitleştirilmiş
const addDataPoint = (price: number, time: string) => {
  try {
    // Yeni dizileri hesapla (değişmez)
    let newPrices = [...priceData.value];
    let newLabels = [...timeLabels.value];
    
    // Maksimum veri noktası sınırı
    if (newPrices.length >= maxDataPoints) {
      newPrices = [...newPrices.slice(1), price]; 
      newLabels = [...newLabels.slice(1), time];
    } else {
      newPrices.push(price);
      newLabels.push(time);
    }
    
    // State'i güncelle
    priceData.value = newPrices;
    timeLabels.value = newLabels;
    
    // Graf varsa ve güncelleme güvenliyse
    if (chartInstance && chartInstance.data && chartInstance.data.datasets[0]) {
      // Graf verilerini güncelle
      chartInstance.data.labels = [...newLabels];
      chartInstance.data.datasets[0].data = [...newPrices];
      chartInstance.update('none'); // Animasyonsuz güncelleme
    } else {
      // Graf yoksa yeniden oluştur
      renderChart();
    }
  } catch (error) {
    console.error('Veri ekleme hatası:', error);
    
    // Hata durumunda grafiği yeniden oluşturmayı dene
    renderChart();
  }
};

// Hisse takibini başlat
const startTracking = () => {
  // Önceki takibi temizle
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  
  // Verileri sıfırla
  priceData.value = [];
  timeLabels.value = [];
  
  // Yükleme durumunu göster
  loading.value = true;
  
  // İlk verileri al
  stockService.getStockBySymbol(symbol.value)
    .then((stock) => {
      if (stock) {
        // İlk veriyi ekle
        addDataPoint(stock.price, formatTime(new Date(stock.lastUpdated)));
        
        // Yükleme tamamlandı
        loading.value = false;
        
        // Graf oluştur
        renderChart();
      }
    })
    .catch((error) => {
      console.error(`${symbol.value} için veri alınamadı:`, error);
      loading.value = false;
    });
  
  // Canlı güncellemelere abone ol
  unsubscribe = stockService.subscribeToStockUpdates([symbol.value], (stock) => {
    try {
      // Yeni veri ekle
      addDataPoint(stock.price, formatTime(new Date(stock.lastUpdated)));
    } catch (error) {
      console.error('Hisse güncellemesi işlenemedi:', error);
    }
  });
};

// Zaman formatlama fonksiyonu
const formatTime = (date: Date): string => {
  return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`;
};

// Symbol değişince takibi güncelle
watch(symbol, () => {
  loading.value = true;
  startTracking();
});

// Component temizliği
onUnmounted(() => {
  // Takibi durdur
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  
  // Grafiği temizle
  if (chartInstance) {
    try {
      chartInstance.destroy();
    } catch (error) {
      console.error('Grafik temizlenemedi:', error);
    }
    chartInstance = null;
  }
});

// Component yüklendiğinde takibi başlat
onMounted(() => {
  startTracking();
});
</script>

<style scoped>
.chart-container {
  position: relative;
  height: 300px;
  width: 100%;
  margin-top: 1rem;
  background-color: white;
  border-radius: 8px;
  padding: 1rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

h3 {
  font-size: 1.2rem;
  margin-bottom: 1rem;
  color: #2c3e50;
  text-align: center;
}

.loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  font-style: italic;
  color: #7f8c8d;
}

.canvas-container {
  position: relative;
  height: 250px;
  width: 100%;
}

canvas {
  display: block;
}
</style> 