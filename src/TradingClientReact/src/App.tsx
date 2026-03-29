import React, { useState } from 'react';
import LoginPage from './pages/Login/LoginPage';
import Dashboard from './pages/Dashboard/Dashboard';

function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [profile, setProfile] = useState<{ username: string; balance: string } | null>(null);

  const handleLogin = (username: string, balance: string = '10000') => {
    setProfile({ username, balance });
    setIsLoggedIn(true);
  };

  const handleLogout = () => {
    setIsLoggedIn(false);
    setProfile(null);
  };

  return (
    <>
      {isLoggedIn && profile ? (
        <Dashboard 
          username={profile.username} 
          balance={profile.balance} 
          onLogout={handleLogout} 
        />
      ) : (
        <LoginPage onLoginSuccess={handleLogin} />
      )}
    </>
  );
}

export default App;
