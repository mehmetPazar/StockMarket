<template>
  <div class="chart-container">
    <h3>{{ symbol }} Price Chart</h3>
    <div v-if="loading" class="loading">Loading chart...</div>
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

// Register Chart.js registerables
Chart.register(...registerables);

// Component props definition
const props = defineProps<{
  symbol: string;
}>();

const { symbol } = toRefs(props);

// Chart and UI references
const chartCanvas = ref<HTMLCanvasElement | null>(null);
const loading = ref(true);
let chartInstance: Chart | null = null; // Non-reactive chart reference

// State for data points - mutable
const priceData = ref<number[]>([]);
const timeLabels = ref<string[]>([]);
const maxDataPoints = 30; // Maximum data points to show in chart

// Unsubscribe function
let unsubscribe: (() => void) | null = null;

// Chart rendering function - simpler and safer
const renderChart = async () => {
  // Wait if canvas is not ready
  if (!chartCanvas.value) {
    console.warn('Canvas is not ready yet');
    return;
  }
  
  // Wait if no data
  if (priceData.value.length === 0) {
    console.log('No chart data yet');
    return;
  }
  
  // Clear previous chart
  if (chartInstance) {
    try {
      chartInstance.destroy();
    } catch (error) {
      console.error('Error clearing chart:', error);
    }
    chartInstance = null;
  }
  
  // Wait for DOM update
  await nextTick();
  
  try {
    // Create immutable data copies
    const labels = [...timeLabels.value];
    const prices = [...priceData.value];
    
    const ctx = chartCanvas.value.getContext('2d');
    if (!ctx) {
      console.error('Could not get canvas context');
      return;
    }
    
    // Create new chart
    chartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [{
          label: `${symbol.value} Price ($)`,
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
                return '$' + value;
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
                return 'Price: $' + context.raw;
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
    
    console.log('Chart created successfully');
  } catch (error) {
    console.error('Error creating chart:', error);
  }
};

// Add data point - simplified
const addDataPoint = (price: number, time: string) => {
  try {
    // Calculate new arrays (immutable)
    let newPrices = [...priceData.value];
    let newLabels = [...timeLabels.value];
    
    // Maximum data points limit
    if (newPrices.length >= maxDataPoints) {
      newPrices = [...newPrices.slice(1), price]; 
      newLabels = [...newLabels.slice(1), time];
    } else {
      newPrices.push(price);
      newLabels.push(time);
    }
    
    // Update state
    priceData.value = newPrices;
    timeLabels.value = newLabels;
    
    // If chart exists and update is safe
    if (chartInstance && chartInstance.data && chartInstance.data.datasets[0]) {
      // Update chart data
      chartInstance.data.labels = [...newLabels];
      chartInstance.data.datasets[0].data = [...newPrices];
      chartInstance.update('none'); // Update without animation
    } else {
      // Recreate chart if it doesn't exist
      renderChart();
    }
  } catch (error) {
    console.error('Error adding data:', error);
    
    // Try to recreate chart on error
    renderChart();
  }
};

// Start stock tracking
const startTracking = () => {
  // Clear previous tracking
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  
  // Reset data
  priceData.value = [];
  timeLabels.value = [];
  
  // Show loading state
  loading.value = true;
  
  // Get initial data
  stockService.getStockBySymbol(symbol.value)
    .then((stock) => {
      if (stock) {
        // Add initial data
        addDataPoint(stock.price, formatTime(new Date(stock.lastUpdated)));
        
        // Loading complete
        loading.value = false;
        
        // Create chart
        renderChart();
      }
    })
    .catch((error) => {
      console.error(`Could not get data for ${symbol.value}:`, error);
      loading.value = false;
    });
  
  // Subscribe to live updates
  unsubscribe = stockService.subscribeToStockUpdates([symbol.value], (stock) => {
    try {
      // Add new data
      addDataPoint(stock.price, formatTime(new Date(stock.lastUpdated)));
    } catch (error) {
      console.error('Could not process stock update:', error);
    }
  });
};

// Time formatting function
const formatTime = (date: Date): string => {
  return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`;
};

// Update tracking when symbol changes
watch(symbol, () => {
  loading.value = true;
  startTracking();
});

// Component cleanup
onUnmounted(() => {
  // Stop tracking
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  
  // Clear chart
  if (chartInstance) {
    try {
      chartInstance.destroy();
    } catch (error) {
      console.error('Could not clear chart:', error);
    }
    chartInstance = null;
  }
});

// Component mounted
onMounted(() => {
  startTracking();
});
</script>

<style scoped>
.chart-container {
  position: relative;
  width: 100%;
  height: 300px;
  padding: 1rem;
  background-color: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

h3 {
  margin: 0 0 1rem;
  font-size: 1.1rem;
  color: #2c3e50;
}

.loading {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  color: #666;
  font-style: italic;
}

.canvas-container {
  width: 100%;
  height: calc(100% - 2rem);
}

canvas {
  width: 100% !important;
  height: 100% !important;
}
</style> 