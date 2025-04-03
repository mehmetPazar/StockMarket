<template>
  <div class="about">
    <h1>About Us</h1>
    <p>
      This application is a modern solution designed to track your stock market transactions, 
      manage your portfolio, and view market analyses.
    </p>
    
    <h2>Technology Infrastructure</h2>
    <ul>
      <li><strong>Frontend:</strong> Vue.js, TypeScript</li>
      <li><strong>Backend:</strong> .NET 8, ASP.NET Core Web API</li>
      <li><strong>Architecture:</strong> Microservice architecture with .NET Aspire</li>
      <li><strong>Data Flow:</strong> Routing with YARP API Gateway</li>
    </ul>
    
    <h2>Contact</h2>
    <p>
      Don't hesitate to contact us for questions, suggestions, and feedback.
      <a href="mailto:info@stockapp.com">info@stockapp.com</a>
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