import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import axios from '../utils/axios';

const AuthCallback: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { setToken } = useAuth();
  const [status, setStatus] = useState<'processing' | 'success' | 'error'>('processing');
  const [error, setError] = useState<string>('');

  useEffect(() => {
    handleAuthCallback();
  }, []);

  const handleAuthCallback = async () => {
    try {
      const token = searchParams.get('token');
      const provider = searchParams.get('provider');
      const error = searchParams.get('error');
      const errorDescription = searchParams.get('error_description');

      if (error) {
        setStatus('error');
        setError(errorDescription || error);
        return;
      }

      if (!token) {
        setStatus('error');
        setError('Missing authentication token');
        return;
      }

      // Store the JWT token and update auth context
      setToken(token);
      setStatus('success');
      
      // Redirect to dashboard after a short delay
      setTimeout(() => {
        navigate('/dashboard');
      }, 1500);
    } catch (error: any) {
      console.error('Auth callback error:', error);
      setStatus('error');
      setError('An unexpected error occurred during authentication');
    }
  };

  const handleRetry = () => {
    navigate('/login');
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <div className="mx-auto h-12 w-12 flex items-center justify-center rounded-full bg-blue-100">
            {status === 'processing' && (
              <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-blue-600"></div>
            )}
            {status === 'success' && (
              <span className="text-2xl text-green-600">✓</span>
            )}
            {status === 'error' && (
              <span className="text-2xl text-red-600">✗</span>
            )}
          </div>
          
          <h2 className="mt-6 text-center text-3xl font-extrabold text-gray-900">
            {status === 'processing' && 'Authenticating...'}
            {status === 'success' && 'Authentication Successful!'}
            {status === 'error' && 'Authentication Failed'}
          </h2>
          
          <div className="mt-4">
            {status === 'processing' && (
              <div className="space-y-2">
                <p className="text-sm text-gray-600">
                  Please wait while we complete your authentication...
                </p>
                <div className="flex justify-center space-x-1">
                  <div className="w-2 h-2 bg-blue-600 rounded-full animate-bounce"></div>
                  <div className="w-2 h-2 bg-blue-600 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }}></div>
                  <div className="w-2 h-2 bg-blue-600 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }}></div>
                </div>
              </div>
            )}
            
            {status === 'success' && (
              <div className="space-y-3">
                <p className="text-sm text-gray-600">
                  You have been successfully authenticated!
                </p>
                <p className="text-sm text-gray-500">
                  Redirecting to your dashboard...
                </p>
              </div>
            )}
            
            {status === 'error' && (
              <div className="space-y-4">
                <div className="bg-red-50 border border-red-200 rounded-md p-4">
                  <div className="flex">
                    <div className="flex-shrink-0">
                      <span className="text-red-400">⚠️</span>
                    </div>
                    <div className="ml-3">
                      <h3 className="text-sm font-medium text-red-800">
                        Authentication Error
                      </h3>
                      <div className="mt-2 text-sm text-red-700">
                        <p>{error}</p>
                      </div>
                    </div>
                  </div>
                </div>
                
                <div className="flex space-x-3">
                  <button
                    onClick={handleRetry}
                    className="flex-1 bg-blue-600 hover:bg-blue-700 text-white font-medium py-2 px-4 rounded-md transition-colors"
                  >
                    Try Again
                  </button>
                  <button
                    onClick={() => navigate('/')}
                    className="flex-1 bg-gray-300 hover:bg-gray-400 text-gray-700 font-medium py-2 px-4 rounded-md transition-colors"
                  >
                    Go Home
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
        
        {/* Debug information (only in development) */}
        {process.env.NODE_ENV === 'development' && (
          <div className="mt-8 p-4 bg-gray-100 rounded-md">
            <h4 className="text-sm font-medium text-gray-700 mb-2">Debug Info:</h4>
            <div className="text-xs text-gray-600 space-y-1">
              <p><strong>Token:</strong> {searchParams.get('token') ? 'Present' : 'Missing'}</p>
              <p><strong>Provider:</strong> {searchParams.get('provider') || 'Missing'}</p>
              <p><strong>Error:</strong> {searchParams.get('error') || 'None'}</p>
              <p><strong>Status:</strong> {status}</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default AuthCallback;