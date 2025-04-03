# StockMarket Project

This project is a stock market application consisting of a .NET 8-based gRPC service and a web interface using Vue.js.

## Project Components

- **StockMarket.GrpcService**: gRPC service providing stock data
- **StockMarket.Web**: Vue.js-based web interface
- **StockMarket.AppHost**: Aspire-based distributed application host project
- **StockMarket.ServiceDefaults**: Shared project for service configuration settings

## Getting Started

### Requirements

- .NET 8 SDK
- Node.js 16+ and npm
- Protobuf compiler

### Installation

1. Clone the project:
```bash
git clone https://github.com/yourusername/StockMarket.git
cd StockMarket
```

2. Install dependencies:
```bash
# Install .NET dependencies
dotnet restore

# Install Vue.js dependencies
cd StockMarket.Web
npm install
```

3. Compile proto files:
```bash
cd StockMarket.Web
npm run proto:gen
```

### Running the Application

```bash
cd /path/to/StockMarket
dotnet run --project StockMarket.AppHost
```

The application will run at:
- gRPC Service: http://localhost:5207
- Web Interface: http://localhost:5173

## Features

- Stock list viewing
- Stock detail page
- Real-time stock data stream (SSE)
- Stock price chart

## Technologies

- **Backend**:
  - .NET 8
  - gRPC
  - Server-Sent Events (SSE)
  - Aspire (distributed application framework)

- **Frontend**:
  - Vue.js 3
  - TypeScript
  - Chart.js
  - Axios
  - gRPC-Web

## License

MIT 