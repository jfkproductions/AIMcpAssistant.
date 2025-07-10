import React, { useState, useEffect } from 'react';
import { CogIcon, CheckCircleIcon, XCircleIcon } from '@heroicons/react/24/outline';
import axios from '../utils/axios';

interface ModuleInfo {
  Enabled: boolean;
  Registered: boolean;
  Name: string;
  Description: string;
}

interface ModuleSettingsProps {
  className?: string;
  onSettingsChange?: (moduleId: string, enabled: boolean) => void;
}

const ModuleSettings: React.FC<ModuleSettingsProps> = ({ className = '', onSettingsChange }) => {
  const [modules, setModules] = useState<Record<string, ModuleInfo>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [isOpen, setIsOpen] = useState(false);

  useEffect(() => {
    fetchModuleSettings();
  }, []);

  const fetchModuleSettings = async () => {
    try {
      setLoading(true);
      const response = await axios.get('/api/module/settings');
      setModules(response.data.modules);
      setError('');
    } catch (error: any) {
      console.error('Error fetching module settings:', error);
      setError('Failed to load module settings');
    } finally {
      setLoading(false);
    }
  };

  const handleToggleModule = async (moduleId: string, currentEnabled: boolean) => {
    const newEnabled = !currentEnabled;
    
    try {
      // Update local state optimistically
      setModules(prev => ({
        ...prev,
        [moduleId]: {
          ...prev[moduleId],
          Enabled: newEnabled
        }
      }));

      // Save to backend
      const response = await axios.post('/api/module/settings', {
        moduleId,
        enabled: newEnabled
      });

      // Notify parent component
      if (onSettingsChange) {
        onSettingsChange(moduleId, newEnabled);
      }

      // Show success message
      alert(`${response.data.message}`);
    } catch (error: any) {
      console.error('Error updating module setting:', error);
      
      // Revert local state on error
      setModules(prev => ({
        ...prev,
        [moduleId]: {
          ...prev[moduleId],
          Enabled: currentEnabled
        }
      }));
      
      // Show error message
      const errorMessage = error.response?.data?.error || 'Failed to update module setting';
      alert(`Error: ${errorMessage}`);
    }
  };

  const getModuleStatusIcon = (module: ModuleInfo) => {
    if (module.Enabled && module.Registered) {
      return <CheckCircleIcon className="w-5 h-5 text-green-500" />;
    } else if (module.Enabled && !module.Registered) {
      return <XCircleIcon className="w-5 h-5 text-yellow-500" />;
    } else {
      return <XCircleIcon className="w-5 h-5 text-red-500" />;
    }
  };

  const getModuleStatusText = (module: ModuleInfo) => {
    if (module.Enabled && module.Registered) {
      return 'Active';
    } else if (module.Enabled && !module.Registered) {
      return 'Enabled (Restart Required)';
    } else {
      return 'Disabled';
    }
  };

  if (loading) {
    return (
      <div className={`${className}`}>
        <div className="animate-pulse">
          <div className="h-4 bg-gray-200 rounded w-1/4 mb-2"></div>
          <div className="h-4 bg-gray-200 rounded w-1/2"></div>
        </div>
      </div>
    );
  }

  return (
    <div className={`${className}`}>
      {/* Settings Toggle Button */}
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center space-x-2 px-4 py-2 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors duration-200"
      >
        <CogIcon className="w-5 h-5 text-gray-600" />
        <span className="text-sm font-medium text-gray-700">Module Settings</span>
      </button>

      {/* Settings Panel */}
      {isOpen && (
        <div className="mt-4 bg-white border border-gray-200 rounded-lg shadow-lg p-4">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-lg font-semibold text-gray-900">MCP Module Configuration</h3>
            <button
              onClick={() => setIsOpen(false)}
              className="text-gray-400 hover:text-gray-600"
            >
              ×
            </button>
          </div>

          {error && (
            <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-md">
              <p className="text-sm text-red-600">{error}</p>
            </div>
          )}

          <div className="space-y-4">
            {Object.entries(modules).map(([moduleId, module]) => (
              <div key={moduleId} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div className="flex-1">
                  <div className="flex items-center space-x-2 mb-1">
                    {getModuleStatusIcon(module)}
                    <h4 className="font-medium text-gray-900">{module.Name}</h4>
                    <span className="text-xs px-2 py-1 bg-gray-200 text-gray-600 rounded-full">
                      {getModuleStatusText(module)}
                    </span>
                  </div>
                  <p className="text-sm text-gray-600">{module.Description}</p>
                </div>
                <div className="ml-4">
                  <label className="flex items-center cursor-pointer">
                    <input
                      type="checkbox"
                      checked={module.Enabled}
                      onChange={() => handleToggleModule(moduleId, module.Enabled)}
                      className="sr-only"
                    />
                    <div className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                      module.Enabled ? 'bg-blue-600' : 'bg-gray-200'
                    }`}>
                      <span className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                        module.Enabled ? 'translate-x-6' : 'translate-x-1'
                      }`} />
                    </div>
                  </label>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-4 p-3 bg-yellow-50 border border-yellow-200 rounded-md">
            <p className="text-sm text-yellow-800">
              <strong>Note:</strong> Changes to module settings require a backend restart to take effect.
              Disabled modules will not process voice commands.
            </p>
          </div>

          <div className="mt-4 flex justify-end">
            <button
              onClick={fetchModuleSettings}
              className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors duration-200"
            >
              Refresh Status
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default ModuleSettings;