import React, { useState } from 'react';
import { useSignalR } from '../contexts/SignalRContext';
import axios from '../utils/axios';

const EmailNotificationTest: React.FC = () => {
  const { connection, isConnected } = useSignalR();
  const [isLoading, setIsLoading] = useState(false);
  const [lastTestResult, setLastTestResult] = useState<string>('');

  const triggerTestEmailNotification = async () => {
    if (!isConnected) {
      setLastTestResult('❌ SignalR not connected');
      return;
    }

    setIsLoading(true);
    setLastTestResult('🔄 Sending test notification...');
    
    try {
      // Call backend endpoint to send test email notification
      const response = await axios.post('/api/test/email-notification', {
        testEmail: 'jfkproductions@gmail.com',
        subject: 'Test Email Notification',
        from: 'test@example.com',
        content: 'This is a test email to verify the notification system is working.'
      });

      if (response.data.success) {
        setLastTestResult('✅ Test notification sent successfully!');
        console.log('✅ Test email notification sent:', response.data);
      } else {
        setLastTestResult(`❌ Failed: ${response.data.message}`);
        console.error('❌ Test notification failed:', response.data);
      }
    } catch (error: any) {
      const errorMsg = error.response?.data?.message || error.message || 'Unknown error';
      setLastTestResult(`❌ Error: ${errorMsg}`);
      console.error('❌ Error sending test notification:', error);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={{
      position: 'fixed',
      bottom: '20px',
      left: '20px',
      zIndex: 1000,
      background: 'rgba(0, 0, 0, 0.9)',
      color: 'white',
      padding: '15px',
      borderRadius: '8px',
      border: '1px solid #333',
      minWidth: '280px',
      maxWidth: '350px'
    }}>
      <h4 style={{ margin: '0 0 10px 0', fontSize: '14px' }}>📧 Email Notification Test</h4>
      
      <div style={{ marginBottom: '10px' }}>
        <div style={{ fontSize: '11px', opacity: 0.8, marginBottom: '5px' }}>
          SignalR: {isConnected ? '🟢 Connected' : '🔴 Disconnected'}
        </div>
        <div style={{ fontSize: '11px', opacity: 0.8 }}>
          Target: jfkproductions@gmail.com
        </div>
      </div>
      
      <button
        onClick={triggerTestEmailNotification}
        disabled={isLoading || !isConnected}
        style={{
          background: isLoading ? '#6c757d' : (isConnected ? '#007bff' : '#dc3545'),
          color: 'white',
          border: 'none',
          padding: '8px 12px',
          borderRadius: '4px',
          cursor: isLoading || !isConnected ? 'not-allowed' : 'pointer',
          fontSize: '12px',
          width: '100%',
          marginBottom: '8px'
        }}
      >
        {isLoading ? '🔄 Testing...' : '🧪 Test Email Notification'}
      </button>
      
      {lastTestResult && (
        <div style={{
          fontSize: '11px',
          padding: '6px 8px',
          borderRadius: '4px',
          background: lastTestResult.includes('✅') ? 'rgba(40, 167, 69, 0.2)' : 'rgba(220, 53, 69, 0.2)',
          border: `1px solid ${lastTestResult.includes('✅') ? '#28a745' : '#dc3545'}`,
          marginTop: '5px'
        }}>
          {lastTestResult}
        </div>
      )}
      
      <p style={{ margin: '8px 0 0 0', fontSize: '10px', opacity: 0.6 }}>
        Tests the complete email notification flow from backend to frontend
      </p>
    </div>
  );
};

export default EmailNotificationTest;