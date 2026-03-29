import React, { useState, useEffect } from 'react';
import { useTradingClient } from '../../logic/hooks/useTradingClient';

const Dashboard: React.FC<{ username: string; balance: string; onLogout: () => void }> = ({ username, onLogout }) => {
  const {
    account,
    orders,
    quotes,
    instruments,
    fetchInstruments,
    subscribeToQuote,
    placeOrder
  } = useTradingClient();

  const [selectedSymbol, setSelectedSymbol] = useState('');
  const [orderQty, setOrderQty] = useState(1);
  const [orderPrice, setOrderPrice] = useState(0);
  const [orderSide, setOrderSide] = useState<'BUY' | 'SELL'>('BUY');

  // Load instruments on mount
  useEffect(() => {
    fetchInstruments();
  }, [fetchInstruments]);

  // Subscribe to all instruments once they are loaded
  useEffect(() => {
    if (instruments.length > 0) {
      instruments.forEach(inst => subscribeToQuote(inst.Symbol));
      if (!selectedSymbol) {
        setSelectedSymbol(instruments[0].Symbol);
        setOrderPrice(quotes.get(instruments[0].Symbol)?.AskPrice || 0);
      }
    }
  }, [instruments, subscribeToQuote, selectedSymbol, quotes]);

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
          <span style={{ fontWeight: 700, fontSize: '1.2rem', letterSpacing: '0.05rem' }}>CRYPTO PLATFORM</span>
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

        {/* Market Watch */}
        <div className="glass" style={{ padding: '1.5rem', borderRadius: '16px', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
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

        {/* Active Orders */}
        <div className="glass" style={{ padding: '1.5rem', borderRadius: '16px', display: 'flex', flexDirection: 'column' }}>
          <h2 style={{ fontSize: '1.1rem', marginBottom: '1.5rem' }}>Active Orders</h2>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {orders.map(order => (
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
                    color: order.Status === 2 ? '#10b981' : '#fbbf24'
                  }}>
                    {order.Status === 2 ? 'Filled' :
                      order.Status === 0 ? 'New' :
                        order.Status === 1 ? 'Partial' :
                          order.Status === 4 ? 'Cancelled' :
                            'Pending'}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>

      </main>

      <footer style={{ position: 'relative', zIndex: 1, padding: '1rem 2rem', borderTop: '1px solid rgba(255,255,255,0.05)', fontSize: '0.8rem', color: 'var(--text-muted)', display: 'flex', justifyContent: 'space-between' }}>
        <div>© 2026 Advanced Trading Systems v1.0.6</div>
      </footer>
    </div>
  );
};

export default Dashboard;
