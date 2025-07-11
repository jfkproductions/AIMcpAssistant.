import React, { useState, useEffect } from 'react';
import { XMarkIcon } from '@heroicons/react/24/outline';
import { clsx } from 'clsx';
import { useSignalR } from '../contexts/SignalRContext';

interface LocalNotification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  timestamp: Date;
}

const NotificationCenter: React.FC = () => {
  const { notifications: signalRNotifications, removeNotification: removeSignalRNotification } = useSignalR();
  const [localNotifications, setLocalNotifications] = useState<LocalNotification[]>([]);

  const removeNotification = (id: string) => {
    setLocalNotifications(prev => prev.filter(notification => notification.id !== id));
    removeSignalRNotification(id);
  };

  // Convert SignalR notifications to local notifications
  useEffect(() => {
    if (signalRNotifications.length > 0) {
      const newLocalNotifications = signalRNotifications
        .filter(notification => !localNotifications.some(local => local.id === notification.id))
        .map(notification => ({
          id: notification.id,
          type: notification.type === 'NewEmail' ? 'info' as const : 'info' as const,
          title: notification.title,
          message: notification.message,
          timestamp: new Date(notification.timestamp)
        }));
      
      if (newLocalNotifications.length > 0) {
        setLocalNotifications(prev => [...prev, ...newLocalNotifications]);
      }
    }
  }, [signalRNotifications, localNotifications]);

  // Auto-remove notifications after 5 seconds
  useEffect(() => {
    const timers = localNotifications.map(notification => {
      return setTimeout(() => {
        removeNotification(notification.id);
      }, 5000);
    });

    return () => {
      timers.forEach(timer => clearTimeout(timer));
    };
  }, [localNotifications]);

  const getNotificationStyles = (type: LocalNotification['type']) => {
    switch (type) {
      case 'success':
        return 'bg-green-50 border-green-200 text-green-800';
      case 'error':
        return 'bg-red-50 border-red-200 text-red-800';
      case 'warning':
        return 'bg-yellow-50 border-yellow-200 text-yellow-800';
      case 'info':
      default:
        return 'bg-blue-50 border-blue-200 text-blue-800';
    }
  };

  if (localNotifications.length === 0) {
    return null;
  }

  return (
    <div className="fixed top-4 right-4 z-50 space-y-2">
      {localNotifications.map((notification) => (
        <div
          key={notification.id}
          className={clsx(
            'max-w-sm w-full border rounded-lg p-4 shadow-lg transition-all duration-300',
            getNotificationStyles(notification.type)
          )}
        >
          <div className="flex items-start">
            <div className="flex-1">
              <h4 className="text-sm font-medium">{notification.title}</h4>
              <p className="mt-1 text-sm opacity-90">{notification.message}</p>
              <p className="mt-1 text-xs opacity-75">
                {notification.timestamp.toLocaleTimeString()}
              </p>
            </div>
            <button
              type="button"
              className="ml-4 inline-flex text-gray-400 hover:text-gray-600 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
              onClick={() => removeNotification(notification.id)}
            >
              <span className="sr-only">Close</span>
              <XMarkIcon className="h-5 w-5" aria-hidden="true" />
            </button>
          </div>
        </div>
      ))}
    </div>
  );
};

export default NotificationCenter;