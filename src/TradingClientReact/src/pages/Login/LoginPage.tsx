import React, { useState } from 'react';
import { useTradingClient } from '../../logic/hooks/useTradingClient';

export const LoginPage: React.FC<{ onLoginSuccess: (username: string, balance: string) => void }> = ({ onLoginSuccess }) => {
  const { createAccount, subscribeToAccount, resetSession } = useTradingClient();
  const [isLogin, setIsLogin] = useState(true);
  const [username, setUsername] = useState('');
  const [balance, setBalance] = useState('10000'); // Default balance for new accounts
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    const normalizedUsername = username.trim();

    if (!normalizedUsername) {
      alert('Username is required.');
      setIsLoading(false);
      return;
    }
    
    try {
      // Clear previous account/order streams so login checks are scoped to this username only.
      resetSession();

      if (isLogin) {
        await subscribeToAccount(normalizedUsername);
      } else {
        await createAccount(normalizedUsername, Number(balance));
        await subscribeToAccount(normalizedUsername);
      }
      onLoginSuccess(normalizedUsername, balance);
    } catch (err) {
      alert(`Authentication failed: ${err}`);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div 
      className="login-container" 
      style={{
        height: '100vh',
        width: '100vw',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'url(/background.png) no-repeat center center fixed',
        backgroundSize: 'cover',
        position: 'relative',
        overflow: 'hidden'
      }}
    >
      {/* Background Overlay */}
      <div style={{
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: 'rgba(15, 23, 42, 0.6)',
        backdropFilter: 'blur(3px)'
      }} />

      <div className="glass login-card" style={{
        position: 'relative',
        zIndex: 1,
        width: '100%',
        maxWidth: '420px',
        padding: '3rem',
        borderRadius: '24px',
        animation: 'fadeIn 0.8s ease-out',
        boxShadow: '0 20px 40px rgba(0,0,0,0.3)',
        textAlign: 'center',
        transition: 'all 0.5s ease'
      }}>
        <div className="logo-section" style={{ marginBottom: '2rem' }}>
          <div style={{
              width: '56px',
              height: '56px',
              background: 'linear-gradient(135deg, #6366f1, #a855f7)',
              borderRadius: '16px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 1.5rem',
              boxShadow: '0 0 20px rgba(99, 102, 241, 0.4)',
              animation: 'slowGlow 3s infinite'
          }}>
             <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5">
               <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
             </svg>
          </div>
          <h1 style={{ fontSize: '2rem', marginBottom: '0.5rem' }}>
            {isLogin ? 'Welcome Back' : 'Join Platform'}
          </h1>
          <p style={{ color: 'var(--text-muted)' }}>
            {isLogin ? 'Enter your username to trade' : 'Create your account to start trading'}
          </p>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <div style={{ textAlign: 'left' }}>
            <label style={{ display: 'block', fontSize: '0.9rem', marginBottom: '0.5rem', color: '#cbd5e1' }}>Username</label>
            <input 
              type="text" 
              placeholder="e.g. trader_pro"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              style={{ width: '100%' }}
            />
          </div>

          {!isLogin && (
            <div style={{ textAlign: 'left', animation: 'fadeIn 0.4s ease' }}>
                <label style={{ display: 'block', fontSize: '0.9rem', marginBottom: '0.5rem', color: '#cbd5e1' }}>Initial Balance ($)</label>
                <input 
                  type="number" 
                  placeholder="10000"
                  value={balance}
                  onChange={(e) => setBalance(e.target.value)}
                  required
                  min="0"
                  step="0.01"
                  style={{ width: '100%' }}
                />
            </div>
          )}

          <button 
            type="submit" 
            disabled={isLoading}
            style={{
                marginTop: '1rem',
                padding: '12px',
                background: 'linear-gradient(135deg, #6366f1, #a855f7)',
                color: 'white',
                fontSize: '1rem',
                boxShadow: isLoading ? 'none' : '0 10px 15px -3px rgba(99, 102, 241, 0.3)',
                opacity: isLoading ? 0.7 : 1,
                transform: isLoading ? 'translateY(1px)' : 'none'
            }}
          >
            {isLoading ? 'Processing...' : (isLogin ? 'Sign In' : 'Register')}
          </button>
        </form>

        <div style={{ marginTop: '2rem', fontSize: '0.9rem', color: 'var(--text-muted)' }}>
          {isLogin ? "Don't have an account?" : "Already have an account?"} 
          <button 
            onClick={() => setIsLogin(!isLogin)}
            style={{ 
                background: 'none', 
                color: 'var(--primary)', 
                fontWeight: '600', 
                marginLeft: '0.5rem',
                fontSize: '0.9rem',
                padding: '0'
            }}
          >
            {isLogin ? 'Sign Up' : 'Log In'}
          </button>
        </div>
      </div>

      <div style={{
          position: 'absolute',
          bottom: '1.5rem',
          right: '1.5rem',
          fontSize: '0.8rem',
          color: 'rgba(255,255,255,0.3)'
      }}>
        Ver. 1.0.5 - Hyper-Secure Terminal
      </div>
    </div>
  );
};

export default LoginPage;
