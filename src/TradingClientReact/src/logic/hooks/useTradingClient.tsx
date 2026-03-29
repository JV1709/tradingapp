import React, { createContext, useContext, useEffect, useState, useMemo } from 'react';
import { TradingClient, TradingClientConfig } from '../TradingClient';
import { Account, Order, Quote, Instrument, PlaceOrderRequest, CancelOrderRequest } from '../types';

interface TradingContextType {
  client: TradingClient | null;
  account: Account | null;
  orders: Order[];
  quotes: Map<string, Quote>;
  instruments: Instrument[];
  
  // Actions
  createAccount: (username: string, initialBalance: number) => Promise<Account>;
  subscribeToAccount: (username: string) => Promise<void>;
  subscribeToQuote: (symbol: string) => Promise<void>;
  placeOrder: (request: PlaceOrderRequest) => void;
  cancelOrder: (request: CancelOrderRequest) => void;
  fetchInstruments: () => Promise<void>;
}

const TradingContext = createContext<TradingContextType | undefined>(undefined);

export const TradingClientProvider: React.FC<{ config: TradingClientConfig; children: React.ReactNode }> = ({ config, children }) => {
  const [account, setAccount] = useState<Account | null>(null);
  const [orders, setOrders] = useState<Order[]>([]);
  const [quotes, setQuotes] = useState<Map<string, Quote>>(new Map());
  const [instruments, setInstruments] = useState<Instrument[]>([]);

  // We memoize the client to avoid recreating it on every render
  const client = useMemo(() => new TradingClient(config), [config]);

  useEffect(() => {
    // Cleanup on unmount
    return () => {
      client.dispose();
    };
  }, [client]);

  // We memoize all actions to prevent infinite re-render loops in components
  const createAccount = React.useCallback(async (username: string, initialBalance: number) => {
    const newAccount = await client.accountClient.createAccount(username, initialBalance);
    setAccount(newAccount);
    return newAccount;
  }, [client]);

  const subscribeToAccount = React.useCallback(async (username: string) => {
    // We await the WebSocket connection as it gives us confirmation
    await client.orderClient.subscribe(username, (event) => {
      setOrders(prev => {
        const index = prev.findIndex(o => o.OrderId === event.Order.OrderId);
        if (index >= 0) {
          const updated = [...prev];
          updated[index] = event.Order;
          return updated;
        }
        return [...prev, event.Order];
      });
    });

    // We do NOT await the account stream subscription as it is an infinite stream
    client.accountClient.subscribe(username, (updatedAccount) => {
      setAccount(updatedAccount);
    });
  }, [client]);

  const subscribeToQuote = React.useCallback(async (symbol: string) => {
    // We do NOT await the quote stream subscription as it is an infinite stream
    client.priceClient.subscribe(symbol, (quote) => {
      setQuotes(prev => {
        const next = new Map(prev);
        next.set(symbol, quote);
        return next;
      });
    });
  }, [client]);

  const placeOrder = React.useCallback((request: PlaceOrderRequest) => {
    client.orderClient.placeOrder(request);
  }, [client]);

  const cancelOrder = React.useCallback((request: CancelOrderRequest) => {
    client.orderClient.cancelOrder(request);
  }, [client]);

  const fetchInstruments = React.useCallback(async () => {
    const data = await client.instrumentClient.getInstruments();
    setInstruments(data);
  }, [client]);

  const value: TradingContextType = React.useMemo(() => ({
    client,
    account,
    orders,
    quotes,
    instruments,
    createAccount,
    subscribeToAccount,
    subscribeToQuote,
    placeOrder,
    cancelOrder,
    fetchInstruments,
  }), [client, account, orders, quotes, instruments, createAccount, subscribeToAccount, subscribeToQuote, placeOrder, cancelOrder, fetchInstruments]);

  return <TradingContext.Provider value={value}>{children}</TradingContext.Provider>;
};

/**
 * Custom hook to consume the TradingClient logic.
 */
export const useTradingClient = () => {
  const context = useContext(TradingContext);
  if (context === undefined) {
    throw new Error('useTradingClient must be used within a TradingClientProvider');
  }
  return context;
};
