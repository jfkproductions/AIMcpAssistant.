import React from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';

interface GlobalHeaderProps {
  title?: string;
  showLogout?: boolean;
  isConnected?: boolean;
  additionalInfo?: React.ReactNode;
}

const GlobalHeader: React.FC<GlobalHeaderProps> = ({ 
  title = "AI MCP Assistant", 
  showLogout = true,
  isConnected,
  additionalInfo
}) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <header style={{
      background: 'linear-gradient(135deg, #0f0f23 0%, #1a1a2e 50%, #16213e 100%)',
      backdropFilter: 'blur(10px)',
      border: '1px solid rgba(255, 255, 255, 0.1)',
      borderBottom: '1px solid rgba(255, 255, 255, 0.2)',
      padding: '1rem 1.5rem',
      color: '#ffffff',
      boxShadow: '0 4px 15px rgba(0, 0, 0, 0.2)'
    }}>
      <div className="flex items-center justify-between max-w-7xl mx-auto">
        <div className="flex items-center space-x-4">
          <div className="flex items-center space-x-2">
            <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
              <span className="text-white font-bold text-sm">AI</span>
            </div>
            <h1 className="text-xl font-semibold text-white">{title}</h1>
          </div>
        </div>
        
        <div className="flex items-center space-x-4">
          {additionalInfo && additionalInfo}
          {user && (
            <>
              {!additionalInfo && (
                <div className="flex items-center space-x-3">
                  <div className={`w-2 h-2 rounded-full ${
                    isConnected !== undefined ? (isConnected ? 'bg-green-500' : 'bg-red-500') : 'bg-green-500'
                  }`}></div>
                  <span className="text-sm text-gray-300">
                    {isConnected !== undefined ? (isConnected ? 'Connected' : 'Disconnected') : 'Connected'}
                  </span>
                </div>
              )}
              
              <div className="flex items-center space-x-2">
                <div className="w-8 h-8 bg-gray-600 rounded-full flex items-center justify-center">
                  <span className="text-white text-sm font-medium">
                    {user.name?.charAt(0).toUpperCase() || user.email?.charAt(0).toUpperCase() || 'U'}
                  </span>
                </div>
                <span className="text-sm text-gray-300">{user.name || user.email}</span>
              </div>
              
              {showLogout && (
                <button
                  onClick={handleLogout}
                  style={{
                    padding: '0.5rem 1rem',
                    fontSize: '0.875rem',
                    fontWeight: '500',
                    color: '#ffffff',
                    background: 'rgba(255, 255, 255, 0.1)',
                    border: '1px solid rgba(255, 255, 255, 0.2)',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    transition: 'all 0.2s ease'
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = 'rgba(255, 255, 255, 0.2)';
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = 'rgba(255, 255, 255, 0.1)';
                  }}
                >
                  Logout
                </button>
              )}
            </>
          )}
        </div>
      </div>
    </header>
  );
};

export default GlobalHeader;