<template>
  <div class="home">
    <h1>Borsa Uygulaması</h1>
    <p>Hoş Geldiniz! Bu uygulama ile borsa verilerini takip edebilirsiniz.</p>
    
    <StockStream />
    
    <StockList />
    
    <div class="features">
      <div class="feature-card">
        <h3>Hisse Takibi</h3>
        <p>İlgilendiğiniz hisseleri takip edin ve anlık fiyatları görüntüleyin.</p>
      </div>
      
      <div class="feature-card">
        <h3>Portföy Yönetimi</h3>
        <p>Yatırımlarınızı tek bir yerden yönetin ve performansını izleyin.</p>
      </div>
      
      <div class="feature-card">
        <h3>Piyasa Analizleri</h3>
        <p>Güncel piyasa analizleri ve finansal haberlerle bilgi sahibi olun.</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import StockList from '@/components/StockList.vue';
import StockStream from '@/components/StockStream.vue';
</script>

<style scoped>
.home {
  margin-top: 2rem;
}

h1 {
  font-size: 2.5rem;
  color: #2c3e50;
  margin-bottom: 1rem;
}

.features {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 2rem;
  margin-top: 3rem;
}

.feature-card {
  background-color: #f8f9fa;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
  transition: transform 0.3s ease;
}

.feature-card:hover {
  transform: translateY(-5px);
}

.feature-card h3 {
  font-size: 1.5rem;
  color: #3498db;
  margin-bottom: 1rem;
}
</style> 