<template>
  <div class="app">
    <header>
      <h1>Borsa Takip Uygulaması</h1>
    </header>

    <main>
      <div class="container">
        <div class="stock-list-container">
          <StockList />
        </div>
        <div class="stock-stream-container">
          <StockStream />
        </div>
      </div>
    </main>

    <footer>
      <p>&copy; 2023 Borsa Takip Uygulaması</p>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { defineAsyncComponent } from 'vue';

// Componentleri asenkron olarak yükle
const StockList = defineAsyncComponent(() => import('./components/StockList.vue'));
const StockStream = defineAsyncComponent(() => import('./components/StockStream.vue'));
</script>

<style>
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  line-height: 1.6;
  color: #333;
  background-color: #f5f7fa;
}

.app {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

header {
  background-color: #2c3e50;
  color: white;
  padding: 1.5rem;
  text-align: center;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

h1 {
  margin: 0;
  font-size: 1.8rem;
}

main {
  flex: 1;
  padding: 2rem;
}

.container {
  max-width: 1400px;
  margin: 0 auto;
  display: grid;
  grid-template-columns: 1fr;
  gap: 2rem;
}

.stock-list-container, 
.stock-stream-container {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  padding: 1rem;
}

@media (min-width: 1024px) {
  .container {
    grid-template-columns: minmax(400px, 1fr) minmax(400px, 1fr);
  }
}

footer {
  background-color: #2c3e50;
  color: white;
  text-align: center;
  padding: 1rem;
  margin-top: auto;
}

footer p {
  margin: 0;
  font-size: 0.9rem;
  opacity: 0.8;
}
</style> 