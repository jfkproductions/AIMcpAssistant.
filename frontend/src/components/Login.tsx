import React from 'react';
import { useAuth } from '../contexts/AuthContext';
import './Login.css';

const Login: React.FC = () => {
  const { login } = useAuth();

  const handleGoogleLogin = () => {
    login('google');
  };

  const handleMicrosoftLogin = () => {
    login('microsoft');
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <div className="login-avatar">
            <div className="avatar-circle">
              <img src="/ai-audio.jpg" alt="AI Assistant" className="microphone-icon" />
              <div className="pulse-ring ring-1"></div>
              <div className="pulse-ring ring-2"></div>
            </div>
          </div>
          <h1 className="login-title">AI MCP Assistant</h1>
          <p className="login-subtitle">
            Your next-generation voice-driven modular assistant platform
          </p>
        </div>
        
        <div className="login-buttons">
          <button
            onClick={handleGoogleLogin}
            className="login-button google"
          >
            <span className="button-icon">
              <svg className="icon" viewBox="0 0 24 24">
                <path
                  fill="currentColor"
                  d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
                />
                <path
                  fill="currentColor"
                  d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
                />
                <path
                  fill="currentColor"
                  d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"
                />
                <path
                  fill="currentColor"
                  d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
                />
              </svg>
            </span>
            Sign in with Google
          </button>
          
          <button
            onClick={handleMicrosoftLogin}
            className="login-button microsoft"
          >
            <span className="button-icon">
              <svg className="icon" viewBox="0 0 24 24">
                <path
                  fill="currentColor"
                  d="M11.4 24H0V12.6h11.4V24zM24 24H12.6V12.6H24V24zM11.4 11.4H0V0h11.4v11.4zm12.6 0H12.6V0H24v11.4z"
                />
              </svg>
            </span>
            Sign in with Microsoft
          </button>
        </div>
        
        <div className="features-section">
          <div className="divider">
            <span className="divider-text">Features</span>
          </div>
          
          <div className="features-list">
            <div className="feature-item">
              <span className="feature-icon">✓</span>
              <span className="feature-text">Voice-driven interface with speech recognition</span>
            </div>
            
            <div className="feature-item">
              <span className="feature-icon">✓</span>
              <span className="feature-text">Email and calendar management</span>
            </div>
            
            <div className="feature-item">
              <span className="feature-icon">✓</span>
              <span className="feature-text">Weather updates and information</span>
            </div>
            
            <div className="feature-item">
              <span className="feature-icon">✓</span>
              <span className="feature-text">Real-time notifications and updates</span>
            </div>
            
            <div className="feature-item">
              <span className="feature-icon">✓</span>
              <span className="feature-text">Modular and extensible architecture</span>
            </div>
          </div>
        </div>
        
        <div className="login-footer">
          <p className="footer-text">
            By signing in, you agree to our terms of service and privacy policy.
          </p>
        </div>
      </div>
    </div>
  );
};

export default Login;