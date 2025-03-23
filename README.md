# StockMarket Projesi

Bu proje, .NET 8 tabanlı bir gRPC servisi ve Vue.js kullanan bir web arayüzünden oluşan borsa uygulamasıdır.

## Proje Bileşenleri

- **StockMarket.GrpcService**: Hisse senedi verileri sağlayan gRPC servisi
- **StockMarket.Web**: Vue.js tabanlı web arayüzü
- **StockMarket.AppHost**: Aspire tabanlı dağıtılmış uygulama ana projesi
- **StockMarket.ServiceDefaults**: Servis yapılandırma ayarları için paylaşılan proje

## Başlangıç

### Gereksinimler

- .NET 8 SDK
- Node.js 16+ ve npm
- Protobuf derleyicisi

### Kurulum

1. Projeyi klonlayın:
```bash
git clone https://github.com/yourusername/StockMarket.git
cd StockMarket
```

2. Bağımlılıkları yükleyin:
```bash
# .NET bağımlılıklarını yükleyin
dotnet restore

# Vue.js bağımlılıklarını yükleyin
cd StockMarket.Web
npm install
```

3. Proto dosyalarını derleyin:
```bash
cd StockMarket.Web
npm run proto:gen
```

### Uygulamayı Çalıştırma

```bash
cd /path/to/StockMarket
dotnet run --project StockMarket.AppHost
```

Uygulama şu adreslerde çalışacaktır:
- gRPC Servisi: http://localhost:5207
- Web Arayüzü: http://localhost:5173

## Özellikler

- Hisse senedi listesi görüntüleme
- Hisse senedi detay sayfası
- Gerçek zamanlı hisse senedi veri akışı (SSE)
- Hisse senedi fiyat grafiği

## Teknolojiler

- **Backend**:
  - .NET 8
  - gRPC
  - Server-Sent Events (SSE)
  - Aspire (dağıtılmış uygulama çerçevesi)

- **Frontend**:
  - Vue.js 3
  - TypeScript
  - Chart.js
  - Axios
  - gRPC-Web

## Lisans

MIT 