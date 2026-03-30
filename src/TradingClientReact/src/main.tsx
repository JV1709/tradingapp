import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { TradingClientProvider } from './logic/hooks/useTradingClient'

// Use an absolute API URL (e.g. https://localhost:7289) to call backend directly.
// When left empty, relative paths are used and routed through Vite proxy.
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

const config = {
  accountClientConfig: { hostname: apiBaseUrl },
  instrumentClientConfig: { hostname: apiBaseUrl },
  orderClientConfig: { hostname: apiBaseUrl },
  priceClientConfig: { hostname: apiBaseUrl }
};

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <TradingClientProvider config={config}>
      <App />
    </TradingClientProvider>
  </StrictMode>,
)
