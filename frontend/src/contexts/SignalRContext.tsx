import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';

interface Notification {
  id: string;
  moduleId: string;
  type: string;
  title: string;
  message: string;
  data?: any;
  timestamp: string;
  priority: string;
  metadata?: Record<string, any>;
}

interface CommandResponse {
  success: boolean;
  message: string;
  data?: any;
  errorCode?: string;
  metadata?: Record<string, any>;
  suggestedActions?: any[];
  requiresFollowUp?: boolean;
  followUpPrompt?: string;
  timestamp: string;
}

interface StatusUpdate {
  status: string;
  data?: any;
  timestamp: string;
}

interface SignalRContextType {
  connection: any | null;
  isConnected: boolean;
  notifications: Notification[];
  lastCommandResponse: CommandResponse | null;
  lastStatusUpdate: StatusUpdate | null;
  subscribeToModule: (moduleId: string) => Promise<void>;
  unsubscribeFromModule: (moduleId: string) => Promise<void>;
  clearNotifications: () => void;
  removeNotification: (id: string) => void;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

export function SignalRProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [connection, setConnection] = useState<any | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [lastCommandResponse, setLastCommandResponse] = useState<CommandResponse | null>(null);
  const [lastStatusUpdate, setLastStatusUpdate] = useState<StatusUpdate | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      startConnection();
    } else {
      stopConnection();
    }

    return () => {
      stopConnection();
    };
  }, [isAuthenticated]);

  const startConnection = async () => {
    try {
      const token = localStorage.getItem('authToken');
      if (!token) return;

      const newConnection = new (signalR as any).HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/hubs/notifications`, {
          accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .configureLogging((signalR as any).LogLevel.Information)
        .build();

      // Set up event handlers
      newConnection.on('Connected', (data: any) => {
        console.log('SignalR connected:', data);
        setIsConnected(true);
      });

      newConnection.on('NotificationReceived', (notification: Notification) => {
        console.log('Notification received:', notification);
        setNotifications(prev => [notification, ...prev].slice(0, 50)); // Keep last 50 notifications
      });

      newConnection.on('CommandResponseReceived', (response: CommandResponse) => {
        console.log('Command response received:', response);
        setLastCommandResponse(response);
      });

      newConnection.on('StatusUpdate', (update: StatusUpdate) => {
        console.log('Status update received:', update);
        setLastStatusUpdate(update);
      });

      newConnection.on('SubscriptionConfirmed', (data: any) => {
        console.log('Subscription confirmed:', data);
      });

      newConnection.on('UnsubscriptionConfirmed', (data: any) => {
        console.log('Unsubscription confirmed:', data);
      });

      newConnection.onreconnecting(() => {
        console.log('SignalR reconnecting...');
        setIsConnected(false);
      });

      newConnection.onreconnected(() => {
        console.log('SignalR reconnected');
        setIsConnected(true);
      });

      newConnection.onclose(() => {
        console.log('SignalR connection closed');
        setIsConnected(false);
      });

      await newConnection.start();
      setConnection(newConnection);
      console.log('SignalR connection started');
    } catch (error) {
      console.error('Error starting SignalR connection:', error);
    }
  };

  const stopConnection = async () => {
    if (connection) {
      try {
        await connection.stop();
        console.log('SignalR connection stopped');
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      } finally {
        setConnection(null);
        setIsConnected(false);
      }
    }
  };

  const subscribeToModule = async (moduleId: string) => {
    if (connection && isConnected) {
      try {
        await connection.invoke('SubscribeToModule', moduleId);
        console.log(`Subscribed to module: ${moduleId}`);
      } catch (error) {
        console.error(`Error subscribing to module ${moduleId}:`, error);
      }
    }
  };

  const unsubscribeFromModule = async (moduleId: string) => {
    if (connection && isConnected) {
      try {
        await connection.invoke('UnsubscribeFromModule', moduleId);
        console.log(`Unsubscribed from module: ${moduleId}`);
      } catch (error) {
        console.error(`Error unsubscribing from module ${moduleId}:`, error);
      }
    }
  };

  const clearNotifications = () => {
    setNotifications([]);
  };

  const removeNotification = (id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id));
  };

  const value: SignalRContextType = {
    connection,
    isConnected,
    notifications,
    lastCommandResponse,
    lastStatusUpdate,
    subscribeToModule,
    unsubscribeFromModule,
    clearNotifications,
    removeNotification
  };

  return (
    <SignalRContext.Provider value={value}>
      {children}
    </SignalRContext.Provider>
  );
}

export function useSignalR() {
  const context = useContext(SignalRContext);
  if (context === undefined) {
    throw new Error('useSignalR must be used within a SignalRProvider');
  }
  return context;
}