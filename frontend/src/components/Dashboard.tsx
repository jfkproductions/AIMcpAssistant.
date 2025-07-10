import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useSignalR } from '../contexts/SignalRContext';
import axios from '../utils/axios';
import { Link } from 'react-router-dom';
import GlobalHeader from './GlobalHeader';
import DashboardVoiceInterface from './DashboardVoiceInterface';

interface Module {
  id: string;
  name: string;
  description: string;
  supportedCommands: string[];
  priority: number;
  icon?: string;
  color?: string;
}

interface ModuleSettings {
  enabled: boolean;
  registered: boolean;
  isSubscribed: boolean;
  name: string;
  description: string;
  version: string;
}

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { notifications, clearNotifications, isConnected, subscribeToModule, unsubscribeFromModule } = useSignalR();
  const [modules, setModules] = useState<Module[]>([]);
  const [moduleSettings, setModuleSettings] = useState<Record<string, ModuleSettings>>({});
  const [loading, setLoading] = useState(true);
  
  // Save module settings to localStorage for persistence
  const saveModuleSettingsToStorage = (settings: Record<string, ModuleSettings>) => {
    try {
      localStorage.setItem(`moduleSettings_${user?.userId}`, JSON.stringify(settings));
    } catch (error) {
      console.warn('Failed to save module settings to localStorage:', error);
    }
  };
  
  // Load module settings from localStorage
  const loadModuleSettingsFromStorage = (): Record<string, ModuleSettings> => {
    try {
      const stored = localStorage.getItem(`moduleSettings_${user?.userId}`);
      return stored ? JSON.parse(stored) : {};
    } catch (error) {
      console.warn('Failed to load module settings from localStorage:', error);
      return {};
    }
  };

  useEffect(() => {
    // Load cached settings first for immediate UI update
    if (user?.userId) {
      const cachedSettings = loadModuleSettingsFromStorage();
      if (Object.keys(cachedSettings).length > 0) {
        setModuleSettings(cachedSettings);
        setLoading(false);
      }
      // Always fetch fresh data from backend
      fetchModules();
    } else {
      // Clear settings if no user
      setModuleSettings({});
      setLoading(false);
    }
    
    // Refresh subscription state when the component becomes visible
    const handleVisibilityChange = () => {
      if (!document.hidden) {
        fetchModules();
      }
    };
    
    // Refresh subscription state when window gains focus
    const handleFocus = () => {
      fetchModules();
    };
    
    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('focus', handleFocus);
    
    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('focus', handleFocus);
    };
  }, [user?.userId]);

  const fetchModules = async () => {
    try {
      // Fetch module settings (includes subscription info)
      const settingsResponse = await axios.get('/api/module/settings');
      console.log('Module Settings Response:', settingsResponse.data);
      const backendSettings = settingsResponse.data.modules || {};
      
      // Fetch available modules
      const modulesResponse = await axios.get('/api/command/modules');
      console.log('Modules Response:', modulesResponse.data);
      setModules(modulesResponse.data.modules || []);
      
      // Fetch user subscriptions to get the most accurate subscription state
      const subscriptionsResponse = await axios.get('/api/module/subscriptions');
      console.log('User Subscriptions Response:', subscriptionsResponse.data);
      
      // Merge backend settings with subscription data
      const mergedSettings = { ...backendSettings };
      if (subscriptionsResponse.data.subscriptions) {
        subscriptionsResponse.data.subscriptions.forEach((sub: any) => {
          if (mergedSettings[sub.moduleId]) {
            // Update existing module settings with subscription status
            mergedSettings[sub.moduleId] = {
              ...mergedSettings[sub.moduleId],
              isSubscribed: sub.isSubscribed
            };
          } else {
            // Create module settings for subscribed modules not in backend settings
            mergedSettings[sub.moduleId] = {
              enabled: true,
              registered: true,
              isSubscribed: sub.isSubscribed,
              name: sub.moduleName || sub.moduleId,
              description: `${sub.moduleName || sub.moduleId} module`,
              version: '1.0.0'
            };
          }
        });
      }
      
      // Update state and save to localStorage
      setModuleSettings(mergedSettings);
      saveModuleSettingsToStorage(mergedSettings);
      
    } catch (error: any) {
      console.error('Error fetching modules:', error);
      console.error('Error details:', error.response?.data);
      setModules([]);
      // Don't clear settings on error, keep cached data
    } finally {
      setLoading(false);
    }
  };

  const handleModuleSubscription = async (moduleId: string, subscribe: boolean) => {
    try {
      // Update subscription in backend
      const response = await axios.post('/api/module/subscription', {
        moduleId,
        isSubscribed: subscribe
      });
      
      console.log('Subscription update response:', response.data);
      
      // Update local state immediately
      setModuleSettings(prev => {
        const updated = {
          ...prev,
          [moduleId]: {
            ...prev[moduleId],
            isSubscribed: subscribe
          }
        };
        saveModuleSettingsToStorage(updated);
        return updated;
      });
      
      // Also update SignalR subscription
      if (subscribe) {
        await subscribeToModule(moduleId);
      } else {
        await unsubscribeFromModule(moduleId);
      }
      
      // Note: Removed automatic refresh to prevent overriding subscription state
      // The state is already updated locally and will be refreshed on next page load/focus
      
    } catch (error) {
      console.error('Error managing module subscription:', error);
      // Revert local state on error
      setModuleSettings(prev => {
        const reverted = {
          ...prev,
          [moduleId]: {
            ...prev[moduleId],
            isSubscribed: !subscribe
          }
        };
        saveModuleSettingsToStorage(reverted);
        return reverted;
      });
    }
  };

  const getModuleIcon = (moduleId: string, moduleData?: Module) => {
    // Use module-specific icon if available in backend data, otherwise use default
    if (moduleData?.icon) {
      return moduleData.icon;
    }
    
    // Fallback to basic icons based on module type
    switch (moduleId.toLowerCase()) {
     
      case 'email':
        return '📧';
       case 'calendar':
        return '📅';
      case 'chatgpt':
      case 'general':
        return '🤖';
      default:
        return '🔧';
    }
  };

  const getModuleStyle = (moduleId: string, isEnabled: boolean, isSubscribed: boolean, moduleData?: Module): React.CSSProperties => {
    let baseStyle: React.CSSProperties = {
      background: 'rgba(255, 255, 255, 0.1)',
      border: '1px solid rgba(255, 255, 255, 0.2)',
      borderRadius: '12px',
      padding: '1rem',
      transition: 'all 0.3s ease'
    };
    
    if (moduleData?.color) {
      // Use module-specific color if available
      baseStyle.background = moduleData.color;
    } else {
      // Fallback to basic colors based on module type
      switch (moduleId.toLowerCase()) {
        case 'email':
          baseStyle.background = 'rgba(59, 130, 246, 0.2)';
          baseStyle.border = '1px solid rgba(59, 130, 246, 0.3)';
          break;
        case 'calendar':
          baseStyle.background = 'rgba(34, 197, 94, 0.2)';
          baseStyle.border = '1px solid rgba(34, 197, 94, 0.3)';
          break;
        case 'chatgpt':
        case 'general':
          baseStyle.background = 'rgba(147, 51, 234, 0.2)';
          baseStyle.border = '1px solid rgba(147, 51, 234, 0.3)';
          break;
        default:
          baseStyle.background = 'rgba(255, 255, 255, 0.1)';
          baseStyle.border = '1px solid rgba(255, 255, 255, 0.2)';
      }
    }
    
    if (!isEnabled) {
      baseStyle.opacity = 0.5;
    }
    
    if (isSubscribed) {
      baseStyle.boxShadow = '0 0 0 2px rgba(34, 197, 94, 0.5)';
    }
    
    return baseStyle;
  };

  return (
    <div className="min-h-screen" style={{
      background: 'linear-gradient(135deg, #0f0f23 0%, #1a1a2e 50%, #16213e 100%)',
      color: '#ffffff',
      fontFamily: '\'Segoe UI\', Tahoma, Geneva, Verdana, sans-serif'
    }}>
      <GlobalHeader 
        title="AI MCP Assistant" 
        isConnected={isConnected}
        additionalInfo={
          <div className="flex items-center space-x-2">
            <div className={`w-3 h-3 rounded-full ${
              isConnected ? 'bg-green-500' : 'bg-red-500'
            }`}></div>
            <span className="text-sm text-gray-300">
              {isConnected ? 'Connected' : 'Disconnected'}
            </span>
          </div>
        }
      />

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">


        {/* Notifications */}
        {notifications.length > 0 && (
          <div className="mb-8">
            <div style={{
              background: 'rgba(255, 255, 255, 0.05)',
              backdropFilter: 'blur(10px)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: '12px',
              padding: '1.5rem'
            }}>
              <h2 className="text-lg font-semibold text-white mb-4">Recent Notifications</h2>
              <div className="space-y-3 max-h-64 overflow-y-auto">
                {notifications.slice(0, 10).map((notification) => (
                  <div
                    key={notification.id}
                    style={{
                      padding: '0.75rem',
                      borderRadius: '8px',
                      borderLeft: `4px solid ${
                        notification.priority === 'high'
                          ? '#ef4444'
                          : notification.priority === 'medium'
                          ? '#f59e0b'
                          : '#3b82f6'
                      }`,
                      background: `${
                        notification.priority === 'high'
                          ? 'rgba(239, 68, 68, 0.1)'
                          : notification.priority === 'medium'
                          ? 'rgba(245, 158, 11, 0.1)'
                          : 'rgba(59, 130, 246, 0.1)'
                      }`
                    }}
                  >
                    <div className="flex justify-between items-start">
                      <div>
                        <h4 className="font-medium text-white">{notification.title}</h4>
                        <p className="text-sm text-gray-300 mt-1">{notification.message}</p>
                        <p className="text-xs text-gray-400 mt-2">
                          {notification.timestamp.toLocaleString()}
                        </p>
                      </div>
                      <span style={{
                        fontSize: '0.75rem',
                        background: 'rgba(255, 255, 255, 0.2)',
                        color: '#e0e0e0',
                        padding: '0.25rem 0.5rem',
                        borderRadius: '4px'
                      }}>
                        {notification.moduleId}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Available Modules */}
        <div className="mb-8">
          <div style={{
            background: 'rgba(255, 255, 255, 0.05)',
            backdropFilter: 'blur(10px)',
            border: '1px solid rgba(255, 255, 255, 0.1)',
            borderRadius: '12px',
            padding: '1.5rem'
          }}>
            <h2 className="text-lg font-semibold text-white mb-4">Available Modules</h2>
            
            {loading ? (
              <div className="text-center py-8">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-400 mx-auto"></div>
                <p className="text-gray-300 mt-2">Loading modules...</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {modules.length > 0 ? modules.map((module) => {
                  const settings = moduleSettings[module.id];
                  // Only show modules that exist in both backend modules and have settings
                  if (!settings) return null;
                  
                  return (
                    <div
                      key={module.id}
                      style={getModuleStyle(module.id, settings.enabled, settings.isSubscribed, module)}
                    >
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center space-x-2">
                          <span className="text-2xl">{getModuleIcon(module.id, module)}</span>
                          <div>
                            <h3 className="font-semibold text-white">{settings.name}</h3>
                            <span className="text-xs text-gray-300">v{settings.version}</span>
                          </div>
                        </div>
                        <div className="flex items-center space-x-2">
                          <div className="flex flex-col items-end space-y-1">
                            <span style={{
                              fontSize: '0.75rem',
                              padding: '0.25rem 0.5rem',
                              borderRadius: '4px',
                              background: settings.enabled ? 'rgba(34, 197, 94, 0.2)' : 'rgba(239, 68, 68, 0.2)',
                              color: settings.enabled ? '#4ade80' : '#f87171',
                              border: `1px solid ${settings.enabled ? 'rgba(34, 197, 94, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`
                            }}>
                              {settings.enabled ? 'Enabled' : 'Disabled'}
                            </span>
                            <span style={{
                              fontSize: '0.75rem',
                              padding: '0.25rem 0.5rem',
                              borderRadius: '4px',
                              background: settings.registered ? 'rgba(59, 130, 246, 0.2)' : 'rgba(107, 114, 128, 0.2)',
                              color: settings.registered ? '#60a5fa' : '#9ca3af',
                              border: `1px solid ${settings.registered ? 'rgba(59, 130, 246, 0.3)' : 'rgba(107, 114, 128, 0.3)'}`
                            }}>
                              {settings.registered ? 'Registered' : 'Not Registered'}
                            </span>
                          </div>
                          <button
                            onClick={() => handleModuleSubscription(
                              module.id,
                              !settings.isSubscribed
                            )}
                            disabled={!settings.enabled || !settings.registered}
                            style={{
                              fontSize: '0.75rem',
                              padding: '0.5rem 0.75rem',
                              borderRadius: '6px',
                              border: 'none',
                              cursor: (!settings.enabled || !settings.registered) ? 'not-allowed' : 'pointer',
                              opacity: (!settings.enabled || !settings.registered) ? 0.5 : 1,
                              background: settings.isSubscribed ? 'rgba(34, 197, 94, 0.8)' : 'rgba(59, 130, 246, 0.8)',
                              color: 'white',
                              transition: 'all 0.3s ease'
                            }}
                            onMouseEnter={(e) => {
                              if (settings.enabled && settings.registered) {
                                e.currentTarget.style.background = settings.isSubscribed ? 'rgba(34, 197, 94, 1)' : 'rgba(59, 130, 246, 1)';
                              }
                            }}
                            onMouseLeave={(e) => {
                              if (settings.enabled && settings.registered) {
                                e.currentTarget.style.background = settings.isSubscribed ? 'rgba(34, 197, 94, 0.8)' : 'rgba(59, 130, 246, 0.8)';
                              }
                            }}
                          >
                            {settings.isSubscribed ? 'Subscribed' : 'Subscribe'}
                          </button>
                        </div>
                      </div>
                      
                      <p className="text-sm text-gray-300 mb-3">{settings.description}</p>
                      
                      <div>
                        <h4 className="text-xs font-medium text-gray-200 mb-2">Supported Commands:</h4>
                        <div className="flex flex-wrap gap-1">
                          {module.supportedCommands.slice(0, 3).map((command, index) => (
                            <span
                              key={index}
                              style={{
                                fontSize: '0.75rem',
                                background: 'rgba(255, 255, 255, 0.1)',
                                color: '#e0e0e0',
                                padding: '0.25rem 0.5rem',
                                borderRadius: '4px',
                                border: '1px solid rgba(255, 255, 255, 0.2)'
                              }}
                            >
                              {command}
                            </span>
                          ))}
                          {module.supportedCommands.length > 3 && (
                            <span className="text-xs text-gray-400 px-2 py-1">
                              +{module.supportedCommands.length - 3} more
                            </span>
                          )}
                        </div>
                      </div>
                      
                      {!settings.enabled && (
                        <div style={{
                          marginTop: '0.75rem',
                          padding: '0.5rem',
                          background: 'rgba(245, 158, 11, 0.1)',
                          border: '1px solid rgba(245, 158, 11, 0.3)',
                          borderRadius: '6px',
                          fontSize: '0.75rem',
                          color: '#fbbf24'
                        }}>
                          ⚠️ This module is disabled in configuration
                        </div>
                      )}
                    </div>
                  );
                }).filter(Boolean) : (
                  <div className="text-center py-8">
                    <p className="text-gray-300">No modules available.</p>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* User Information */}
        <div style={{
          background: 'rgba(255, 255, 255, 0.05)',
          backdropFilter: 'blur(10px)',
          border: '1px solid rgba(255, 255, 255, 0.1)',
          borderRadius: '12px',
          padding: '1.5rem'
        }}>
          <h2 className="text-lg font-semibold text-white mb-4">Account Information</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <h3 className="text-sm font-medium text-gray-200 mb-2">Profile</h3>
              <div className="space-y-2">
                <p className="text-sm text-gray-300"><span className="font-medium text-white">Name:</span> {user?.name}</p>
                <p className="text-sm text-gray-300"><span className="font-medium text-white">Email:</span> {user?.email}</p>
                <p className="text-sm text-gray-300"><span className="font-medium text-white">Provider:</span> {user?.provider}</p>
              </div>
            </div>
            
            <div>
              <h3 className="text-sm font-medium text-gray-200 mb-2">Account Details</h3>
              <div className="space-y-2">
                <p className="text-sm text-gray-300">
                  <span className="font-medium text-white">Provider:</span> {user?.provider}
                </p>
                <p className="text-sm text-gray-300">
                  <span className="font-medium text-white">User ID:</span> {user?.userId}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      {/* Persistent Voice Interface */}
      <DashboardVoiceInterface />
      
      <nav className="bottom-navigation">
        <button className="nav-button" onClick={() => navigate('/')}>
          <span className="nav-icon">🏠</span>
          <span className="nav-label">Home</span>
        </button>
        <button className="nav-button active">
          <span className="nav-icon">⚙️</span>
          <span className="nav-label">Dashboard</span>
        </button>
      </nav>
    </div>
  );
};

export default Dashboard;