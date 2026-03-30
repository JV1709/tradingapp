import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { TradingClientProvider } from './logic/hooks/useTradingClient'

// Using relative paths to leverage the Vite dev server proxy
const config = {
  accountClientConfig: { hostname: '' },
  instrumentClientConfig: { hostname: '' },
  orderClientConfig: { hostname: '' },
  priceClientConfig: { hostname: '' }
};

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <TradingClientProvider config={config}>
      <App />
    </TradingClientProvider>
  </StrictMode>,
)
