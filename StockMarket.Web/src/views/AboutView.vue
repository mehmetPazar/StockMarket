<template>
  <div class="about">
    <h1>Hakkımızda</h1>
    <p>
      Bu uygulama, borsa işlemlerinizi takip etmek, portföyünüzü yönetmek ve piyasa analizlerini 
      görüntülemek için tasarlanmış modern bir çözümdür.
    </p>
    
    <h2>Teknolojik Altyapı</h2>
    <ul>
      <li><strong>Frontend:</strong> Vue.js, TypeScript</li>
      <li><strong>Backend:</strong> .NET 8, ASP.NET Core Web API</li>
      <li><strong>Mimari:</strong> .NET Aspire ile mikro servis mimarisi</li>
      <li><strong>Veri Akışı:</strong> YARP API Gateway ile yönlendirme</li>
    </ul>
    
    <h2>İletişim</h2>
    <p>
      Soru, öneri ve geri bildirimleriniz için bizimle iletişime geçmekten çekinmeyin.
      <a href="mailto:info@borsaapp.com">info@borsaapp.com</a>
    </p>
  </div>
</template>

<style scoped>
.about {
  max-width: 800px;
  margin: 0 auto;
}

h1 {
  color: #2c3e50;
  margin-bottom: 1.5rem;
}

h2 {
  color: #3498db;
  margin-top: 2rem;
  margin-bottom: 1rem;
}

p {
  line-height: 1.6;
  margin-bottom: 1rem;
}

ul {
  margin-left: 1.5rem;
  margin-bottom: 1.5rem;
}

li {
  margin-bottom: 0.5rem;
}

a {
  color: #3498db;
  text-decoration: none;
}

a:hover {
  text-decoration: underline;
}
</style> 