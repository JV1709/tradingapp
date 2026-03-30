import React, { useState, useEffect, useRef, useMemo } from 'react';
import { useTradingClient } from '../../logic/hooks/useTradingClient';
import { Order, OrderStatus } from '../../logic/types';

const Dashboard: React.FC<{ username: string; balance: string; onLogout: () => void }> = ({ username, onLogout }) => {
  const {
    account,
    orders,
    quotes,
    instruments,
    fetchInstruments,
    subscribeToQuote,
    unsubscribeFromQuote,
    placeOrder,
    cancelOrder
  } = useTradingClient();

  const [selectedSymbol, setSelectedSymbol] = useState('');
  const [orderQty, setOrderQty] = useState(1);
  const [orderPrice, setOrderPrice] = useState(0);
  const [orderSide, setOrderSide] = useState<'BUY' | 'SELL'>('BUY');
  const [ordersView, setOrdersView] = useState<'ACTIVE' | 'SETTLED'>('ACTIVE');
  const [isTabActive, setIsTabActive] = useState(() => !document.hidden);
  const subscribedSymbolsRef = useRef(new Set<string>());
  const pendingSinceRef = useRef(new Map<string, number>());
  const quoteTimestampRef = useRef(new Map<string, string>());
  const marketWatchSymbols = useMemo(() => {
    if (instruments.length === 0) {
      return [] as string[];
    }

    const preferred = ['AAPL', 'GOOG', 'MSFT', 'AMZN'];
    const available = new Set(instruments.map((inst) => inst.Symbol));
    const selected = preferred.filter((symbol) => available.has(symbol));

    if (selected.length > 0) {
      return selected;
    }

    return instruments.slice(0, 4).map((inst) => inst.Symbol);
  }, [instruments]);
  const positiveHoldings = (account?.Holdings || []).filter(holding => holding.TotalQuantity > 0);

  // Load instruments on mount
  useEffect(() => {
    fetchInstruments().catch((error) => {
      console.error('Failed to load instruments on dashboard mount:', error);
    });
  }, [fetchInstruments]);

  // Pick a default symbol once instruments are loaded
  useEffect(() => {
    if (instruments.length > 0) {
      if (!selectedSymbol) {
        setSelectedSymbol(instruments[0].Symbol);
      }
    }
  }, [instruments, selectedSymbol]);

  useEffect(() => {
    const onVisibilityChange = () => {
      setIsTabActive(!document.hidden);
    };

    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => {
      document.removeEventListener('visibilitychange', onVisibilityChange);
    };
  }, []);

  // Release quote streams when this tab goes to background.
  useEffect(() => {
    if (isTabActive) {
      return;
    }

    subscribedSymbolsRef.current.forEach((symbol) => {
      unsubscribeFromQuote(symbol);
    });
    subscribedSymbolsRef.current.clear();
    pendingSinceRef.current.clear();
  }, [isTabActive, unsubscribeFromQuote]);

  // Keep a bounded market watch stream set active.
  useEffect(() => {
    if (!isTabActive) {
      return;
    }

    const symbolsToSubscribe = selectedSymbol
      ? Array.from(new Set([...marketWatchSymbols, selectedSymbol]))
      : marketWatchSymbols;

    symbolsToSubscribe.forEach((symbol) => {
      if (!symbol || subscribedSymbolsRef.current.has(symbol)) {
        return;
      }

      subscribedSymbolsRef.current.add(symbol);
      pendingSinceRef.current.set(symbol, Date.now());
      subscribeToQuote(symbol).catch((error) => {
        console.error(`Failed to subscribe to quote for ${symbol}:`, error);
        subscribedSymbolsRef.current.delete(symbol);
        pendingSinceRef.current.delete(symbol);
      });
    });
  }, [isTabActive, marketWatchSymbols, selectedSymbol, subscribeToQuote]);

  // Mark symbols as healthy only when a fresh quote arrives.
  useEffect(() => {
    quotes.forEach((quote, symbol) => {
      const previousTimestamp = quoteTimestampRef.current.get(symbol);
      if (previousTimestamp === quote.Timestamp) {
        return;
      }

      quoteTimestampRef.current.set(symbol, quote.Timestamp);
      pendingSinceRef.current.delete(symbol);
    });
  }, [quotes]);

  // Retry symbols that remain pending for too long.
  useEffect(() => {
    if (!isTabActive) {
      return;
    }

    const retryDelayMs = 12000;
    const intervalId = window.setInterval(() => {
      const now = Date.now();
      pendingSinceRef.current.forEach((startedAt, symbol) => {
        if (now - startedAt < retryDelayMs) {
          return;
        }

        console.warn(`Quote stream for ${symbol} is stale; retrying subscription.`);
        unsubscribeFromQuote(symbol);
        subscribedSymbolsRef.current.delete(symbol);
        pendingSinceRef.current.set(symbol, Date.now());

        subscribeToQuote(symbol).catch((error) => {
          console.error(`Retry failed for quote ${symbol}:`, error);
          subscribedSymbolsRef.current.delete(symbol);
          pendingSinceRef.current.delete(symbol);
        });
      });
    }, 3000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [isTabActive, subscribeToQuote, unsubscribeFromQuote]);

  // Update order price when selected symbol changes
  useEffect(() => {
    if (selectedSymbol) {
      const q = quotes.get(selectedSymbol);
      if (q) setOrderPrice(orderSide === 'BUY' ? (q.AskPrice || 0) : (q.BidPrice || 0));
    }
  }, [selectedSymbol, orderSide, quotes]);

  const handlePlaceOrder = (e: React.FormEvent) => {
    e.preventDefault();
    if (!account) return;

    placeOrder({
      AccountKey: username,
      Symbol: selectedSymbol,
      Side: orderSide === 'BUY' ? 1 : 2,
      Quantity: orderQty,
      Price: orderPrice
    });

    alert(`Order submitted for ${selectedSymbol}`);
  };

  const getOrderProgress = (order: Order) => {
    const total = Math.max(order.TotalQuantity, 0);
    const filled = Math.max(order.FilledQuantity, 0);
    const remaining = Math.max(total - filled, 0);
    const progressPct = total > 0 ? Math.min(100, (filled / total) * 100) : 0;

    return { total, filled, remaining, progressPct };
  };

  const getOrderPnlMetrics = (order: Order): { pct: number; amount: number } | null => {
    const avgFillPrice = order.AverageFillPrice ?? order.AvgFillPrice;
    const lastPrice = quotes.get(order.Symbol)?.LastDonePrice;
    const filledQty = Math.max(order.FilledQuantity, 0);

    if (!avgFillPrice || avgFillPrice <= 0 || !lastPrice || lastPrice <= 0 || filledQty <= 0) {
      return null;
    }

    const rawPct = ((lastPrice - avgFillPrice) / avgFillPrice) * 100;
    const signedPct = order.Side === 1 ? rawPct : -rawPct;
    const rawAmount = (lastPrice - avgFillPrice) * filledQty;
    const signedAmount = order.Side === 1 ? rawAmount : -rawAmount;

    return {
      pct: signedPct,
      amount: signedAmount,
    };
  };

  const isCancellable = (status: OrderStatus): boolean => {
    return status === OrderStatus.New || status === OrderStatus.PartiallyFilled || status === OrderStatus.PendingNew;
  };

  const isCompletedOrder = (status: OrderStatus): boolean => {
    return status === OrderStatus.Filled || status === OrderStatus.Cancelled;
  };

  const isSettledOrder = (status: OrderStatus): boolean => {
    return isCompletedOrder(status) || status === OrderStatus.Rejected;
  };

  const getStatusLabel = (status: OrderStatus): string => {
    switch (status) {
      case OrderStatus.Filled:
        return 'Filled';
      case OrderStatus.New:
        return 'New';
      case OrderStatus.PartiallyFilled:
        return 'Partial';
      case OrderStatus.Cancelled:
        return 'Cancelled';
      case OrderStatus.PendingCancel:
        return 'Pending Cancel';
      case OrderStatus.Rejected:
        return 'Rejected';
      case OrderStatus.PendingNew:
        return 'Pending New';
      default:
        return 'Pending';
    }
  };

  const handleCancelOrder = (orderId: string) => {
    try {
      cancelOrder({ OrderId: orderId });
    } catch (error) {
      console.error(`Failed to cancel order ${orderId}:`, error);
      alert('Failed to send cancel request. Please try again.');
    }
  };

  const activeOrders = orders.filter(order => !isSettledOrder(order.Status as OrderStatus));
  const settledOrders = orders.filter(order => isSettledOrder(order.Status as OrderStatus));
  const displayedOrders = ordersView === 'ACTIVE' ? activeOrders : settledOrders;

  const renderOrderCard = (order: Order, allowCancel: boolean) => {
    const progress = getOrderProgress(order);
    const canCancel = allowCancel && isCancellable(order.Status as OrderStatus);
    const avgFillPrice = order.AverageFillPrice ?? order.AvgFillPrice;
    const orderPnl = getOrderPnlMetrics(order);
    const pnlColor = orderPnl === null
      ? 'var(--text-muted)'
      : orderPnl.pct >= 0
        ? '#10b981'
        : '#ef4444';

    return (
      <div key={order.OrderId} className="glass" style={{ padding: '1rem', borderRadius: '12px', marginBottom: '0.75rem', border: '1px solid rgba(255,255,255,0.05)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
          <span style={{ fontWeight: 700, fontSize: '0.9rem' }}>{order.Symbol}</span>
          <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>{order.OrderId.substring(0, 8)}</span>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
            <span style={{
              fontSize: '0.7rem',
              padding: '2px 6px',
              borderRadius: '4px',
              background: order.Side === 1 ? 'rgba(16, 185, 129, 0.2)' : 'rgba(239, 68, 68, 0.2)',
              color: order.Side === 1 ? '#10b981' : '#ef4444'
            }}>
              {order.Side === 1 ? 'BUY' : 'SELL'}
            </span>
            <span style={{ fontSize: '0.85rem' }}>{order.TotalQuantity} @ {order.Price}</span>
          </div>
          <span style={{
            fontSize: '0.75rem',
            color: order.Status === OrderStatus.Filled ? '#10b981' : '#fbbf24'
          }}>
            {getStatusLabel(order.Status as OrderStatus)}
          </span>
        </div>

        <div style={{ marginTop: '0.75rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '0.35rem' }}>
            <span>{progress.filled}/{progress.total} filled</span>
            <span>Remaining: {progress.remaining}</span>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', marginBottom: '0.35rem' }}>
            <span style={{ color: 'var(--text-muted)' }}>Avg Fill Price</span>
            <span style={{ color: avgFillPrice && avgFillPrice > 0 ? 'rgba(255,255,255,0.92)' : 'var(--text-muted)', fontWeight: 600, fontFeatureSettings: '"tnum"' }}>
              {avgFillPrice && avgFillPrice > 0 ? `$${avgFillPrice.toFixed(2)}` : '--'}
            </span>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', marginBottom: '0.35rem' }}>
            <span style={{ color: 'var(--text-muted)' }}>P/L</span>
            <span style={{ color: pnlColor, fontWeight: 600, fontFeatureSettings: '"tnum"' }}>
              {orderPnl === null
                ? '--'
                : `${orderPnl.pct >= 0 ? '+' : ''}${orderPnl.pct.toFixed(2)}% (${orderPnl.amount >= 0 ? '+' : '-'}$${Math.abs(orderPnl.amount).toFixed(2)})`}
            </span>
          </div>
          <div style={{
            height: '8px',
            borderRadius: '999px',
            background: 'rgba(255,255,255,0.08)',
            overflow: 'hidden'
          }}>
            <div style={{
              width: `${progress.progressPct}%`,
              height: '100%',
              borderRadius: '999px',
              background: order.Side === 1 ? 'linear-gradient(90deg, #10b981, #34d399)' : 'linear-gradient(90deg, #ef4444, #f87171)',
              transition: 'width 0.25s ease'
            }} />
          </div>
        </div>

        {allowCancel && (
          <div style={{ marginTop: '0.85rem', display: 'flex', justifyContent: 'flex-end' }}>
            <button
              onClick={() => handleCancelOrder(order.OrderId)}
              disabled={!canCancel}
              style={{
                background: canCancel ? 'rgba(239, 68, 68, 0.12)' : 'rgba(148, 163, 184, 0.15)',
                color: canCancel ? '#ef4444' : 'rgba(255,255,255,0.5)',
                border: canCancel ? '1px solid rgba(239, 68, 68, 0.35)' : '1px solid rgba(148, 163, 184, 0.25)',
                padding: '6px 10px',
                borderRadius: '8px',
                fontSize: '0.78rem',
                cursor: canCancel ? 'pointer' : 'not-allowed'
              }}
            >
              Cancel Order
            </button>
          </div>
        )}
      </div>
    );
  };

  if (!account) {
    return (
      <div style={{ height: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <p>Loading account details...</p>
      </div>
    );
  }

  return (
    <div className="dashboard-container" style={{
      minHeight: '100vh',
      background: 'url(/background.png) no-repeat center center fixed',
      backgroundSize: 'cover',
    }}>
      {/* Dynamic Background Glass Layer */}
      <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(15, 23, 42, 0.4)', backdropFilter: 'blur(30px)', zIndex: 0 }} />

      {/* Header */}
      <header style={{
        position: 'relative',
        zIndex: 1,
        padding: '1.25rem 2rem',
        borderBottom: '1px solid rgba(255,255,255,0.05)',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{ width: '32px', height: '32px', background: 'linear-gradient(135deg, #6366f1, #a855f7)', borderRadius: '8px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5">
              <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
            </svg>
          </div>
          <span style={{ fontWeight: 700, fontSize: '1.2rem', letterSpacing: '0.05rem' }}>TRADING PLATFORM</span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '2rem' }}>
          <div style={{ textAlign: 'right' }}>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Trader</div>
            <div style={{ fontWeight: 600 }}>{username}</div>
          </div>
          <div style={{
            display: 'flex',
            gap: '2rem',
            borderLeft: '1px solid rgba(255,255,255,0.1)',
            paddingLeft: '2rem',
            alignItems: 'center'
          }}>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Available</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, color: 'var(--accent)' }}>
                ${account?.AvailableBalance?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) || '0.00'}
              </div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Total</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, color: 'rgba(255,255,255,0.9)' }}>
                ${account?.TotalBalance?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) || '0.00'}
              </div>
            </div>
          </div>
          <button
            onClick={onLogout}
            style={{
              background: 'rgba(239, 68, 68, 0.1)',
              color: '#ef4444',
              border: '1px solid rgba(239, 68, 68, 0.2)',
              padding: '8px 16px'
            }}
          >
            Logout
          </button>
        </div>
      </header>

      {/* Main Grid */}
      <main style={{
        position: 'relative',
        zIndex: 1,
        padding: '2rem',
        display: 'grid',
        gridTemplateColumns: 'minmax(350px, 1fr) 400px 1fr',
        gap: '1.5rem',
        maxHeight: 'calc(100vh - 120px)'
      }}>

        <div style={{ display: 'grid', gap: '1.5rem', minHeight: 0 }}>
          {/* Market Watch */}
          <div className="glass" style={{ padding: '1.5rem', borderRadius: '16px', overflow: 'hidden', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
            <h2 style={{ fontSize: '1.1rem', marginBottom: '1.5rem', borderBottom: '1px solid rgba(255,255,255,0.05)', paddingBottom: '0.5rem' }}>Market Watch</h2>
            <div style={{ flex: 1, overflowY: 'auto' }}>
              <table style={{ width: '100%', textAlign: 'left', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ color: 'var(--text-muted)', fontSize: '0.8rem' }}>
                    <th style={{ padding: '8px 4px' }}>SYMBOL</th>
                    <th style={{ padding: '8px 4px' }}>BID</th>
                    <th style={{ padding: '8px 4px' }}>ASK</th>
                    <th style={{ padding: '8px 4px' }}>LAST</th>
                  </tr>
                </thead>
                <tbody>
                  {instruments.map(inst => {
                    const q = quotes.get(inst.Symbol);
                    return (
                      <tr key={inst.Symbol}
                        onClick={() => setSelectedSymbol(inst.Symbol)}
                        style={{
                          cursor: 'pointer',
                          borderBottom: '1px solid rgba(255,255,255,0.05)',
                          background: selectedSymbol === inst.Symbol ? 'rgba(99, 102, 241, 0.1)' : 'transparent',
                          transition: 'background 0.2s'
                        }}>
                        <td style={{ padding: '12px 8px', fontWeight: 600 }}>{inst.Symbol}</td>
                        <td style={{ padding: '12px 8px', color: '#10b981', fontFeatureSettings: '"tnum"' }}>{q?.BidPrice?.toFixed(2) || '---'}</td>
                        <td style={{ padding: '12px 8px', color: '#ef4444', fontFeatureSettings: '"tnum"' }}>{q?.AskPrice?.toFixed(2) || '---'}</td>
                        <td style={{ padding: '12px 8px', fontFeatureSettings: '"tnum"' }}>{q?.LastDonePrice?.toFixed(2) || '---'}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        {/* Order Entry */}
        <div className="glass" style={{ padding: '2rem', borderRadius: '16px', display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          <h2 style={{ fontSize: '1.1rem' }}>Order Entry</h2>

          <div style={{ display: 'flex', gap: '8px', background: 'rgba(255,255,255,0.05)', padding: '4px', borderRadius: '10px' }}>
            <button
              onClick={() => setOrderSide('BUY')}
              style={{ flex: 1, padding: '10px', background: orderSide === 'BUY' ? '#10b981' : 'transparent', color: 'white', borderRadius: '8px', border: 'none' }}
            >
              BUY
            </button>
            <button
              onClick={() => setOrderSide('SELL')}
              style={{ flex: 1, padding: '10px', background: orderSide === 'SELL' ? '#ef4444' : 'transparent', color: 'white', borderRadius: '8px', border: 'none' }}
            >
              SELL
            </button>
          </div>

          <form onSubmit={handlePlaceOrder} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            <div>
              <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: '0.5rem' }}>Instrument</label>
              <div style={{ padding: '10px', background: 'rgba(255,255,255,0.05)', borderRadius: '4px', border: '1px solid rgba(255,255,255,0.1)', fontWeight: 600 }}>
                {selectedSymbol || 'Select Symbol'}
              </div>
            </div>
            <div>
              <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: '0.5rem' }}>Quantity</label>
              <input
                type="number"
                step="1"
                value={orderQty}
                onChange={(e) => setOrderQty(Number(e.target.value))}
                style={{ width: '100%' }}
              />
            </div>
            <div>
              <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: '0.5rem' }}>Price ($)</label>
              <input
                type="number"
                step="0.01"
                value={orderPrice}
                onChange={(e) => setOrderPrice(Number(e.target.value))}
                style={{ width: '100%' }}
              />
            </div>

            <button
              type="submit"
              style={{
                marginTop: 'auto',
                padding: '14px',
                background: orderSide === 'BUY' ? '#10b981' : '#ef4444',
                color: 'white',
                fontSize: '1rem',
                fontWeight: 600,
                boxShadow: orderSide === 'BUY' ? '0 8px 20px rgba(16, 185, 129, 0.3)' : '0 8px 20px rgba(239, 68, 68, 0.3)'
              }}
            >
              Place {orderSide} Order
            </button>
          </form>
        </div>

        {/* Holdings and Orders */}
        <div style={{ display: 'grid', gap: '1.5rem', gridTemplateRows: 'auto minmax(0, 1fr)', minHeight: 0 }}>
          <div className="glass" style={{ padding: '1rem 1.25rem', borderRadius: '16px', display: 'flex', flexDirection: 'column' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.6rem' }}>
              <h2 style={{ fontSize: '1rem', marginBottom: 0 }}>My Holdings</h2>
              <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>{positiveHoldings.length} symbols</span>
            </div>
            <div>
              <table style={{ width: '100%', textAlign: 'left', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ color: 'var(--text-muted)', fontSize: '0.72rem' }}>
                    <th style={{ padding: '6px 4px' }}>SYMBOL</th>
                    <th style={{ padding: '6px 4px', textAlign: 'right' }}>TOTAL</th>
                    <th style={{ padding: '6px 4px', textAlign: 'right' }}>AVAIL</th>
                  </tr>
                </thead>
                <tbody>
                  {positiveHoldings.map(holding => (
                    <tr key={holding.Symbol} style={{ borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
                      <td style={{ padding: '7px 4px', fontWeight: 600, fontSize: '0.84rem' }}>{holding.Symbol}</td>
                      <td style={{ padding: '7px 4px', textAlign: 'right', fontFeatureSettings: '"tnum"', fontSize: '0.82rem' }}>
                        {holding.TotalQuantity.toLocaleString()}
                      </td>
                      <td style={{ padding: '7px 4px', textAlign: 'right', fontFeatureSettings: '"tnum"', color: 'var(--text-muted)', fontSize: '0.82rem' }}>
                        {holding.AvailableQuantity.toLocaleString()}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {positiveHoldings.length === 0 && (
                <div style={{ color: 'var(--text-muted)', fontSize: '0.85rem', paddingTop: '0.4rem' }}>
                  No holdings yet.
                </div>
              )}
            </div>
          </div>

          <div className="glass" style={{ padding: '1.25rem', borderRadius: '16px', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.9rem', gap: '0.8rem' }}>
              <h2 style={{ fontSize: '1.05rem', marginBottom: 0 }}>Orders</h2>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                {ordersView === 'ACTIVE' ? `${activeOrders.length} open` : `${settledOrders.length} settled`}
              </span>
            </div>
            <div style={{ display: 'flex', gap: '8px', background: 'rgba(255,255,255,0.05)', padding: '4px', borderRadius: '10px', marginBottom: '0.9rem' }}>
              <button
                onClick={() => setOrdersView('ACTIVE')}
                style={{
                  flex: 1,
                  padding: '8px',
                  background: ordersView === 'ACTIVE' ? 'rgba(16, 185, 129, 0.25)' : 'transparent',
                  color: ordersView === 'ACTIVE' ? '#10b981' : 'var(--text-muted)',
                  borderRadius: '8px',
                  border: 'none',
                  fontSize: '0.8rem',
                  fontWeight: 600
                }}
              >
                Active
              </button>
              <button
                onClick={() => setOrdersView('SETTLED')}
                style={{
                  flex: 1,
                  padding: '8px',
                  background: ordersView === 'SETTLED' ? 'rgba(99, 102, 241, 0.2)' : 'transparent',
                  color: ordersView === 'SETTLED' ? '#a5b4fc' : 'var(--text-muted)',
                  borderRadius: '8px',
                  border: 'none',
                  fontSize: '0.8rem',
                  fontWeight: 600
                }}
              >
                Settled
              </button>
            </div>
            <div style={{ flex: 1, overflowY: 'auto', paddingRight: '2px' }}>
              {displayedOrders.map(order => renderOrderCard(order, ordersView === 'ACTIVE'))}
              {displayedOrders.length === 0 && (
                <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem', paddingTop: '0.5rem' }}>
                  {ordersView === 'ACTIVE' ? 'No active orders.' : 'No settled orders.'}
                </div>
              )}
            </div>
          </div>
        </div>

      </main>

    </div>
  );
};

export default Dashboard;
