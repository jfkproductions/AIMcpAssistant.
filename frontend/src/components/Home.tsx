import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useSignalR } from '../contexts/SignalRContext';
import { useAIMCPContext } from '../hooks/useAIMCPContext';
import GlobalHeader from './GlobalHeader';
import NotificationCenter from './NotificationCenter';
import EmailNotificationTest from './EmailNotificationTest';

import axios from '../utils/axios';
import './Home.css';

interface Message {
  id: string;
  type: 'user' | 'ai';
  content: string;
  timestamp: Date;
}

const Home: React.FC = () => {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { lastCommandResponse, isConnected, notifications } = useSignalR();
  const [isListening, setIsListening] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [speakerEnabled, setSpeakerEnabled] = useState(true);
  const [isProcessing, setIsProcessing] = useState(false);
  const [emailFlash, setEmailFlash] = useState<{show: boolean, subject: string, from: string}>({show: false, subject: '', from: ''});
  const { currentMCPContext, handleAIMCPCommand, getCurrentMCPContext, setCurrentMCPContext } = useAIMCPContext();
  const [messages, setMessages] = useState<Message[]>([
    {
      id: '1',
      type: 'ai',
      content: "Hello! I'm your AI assistant. How can I help you today?",
      timestamp: new Date()
    }
  ]);
  const [status, setStatus] = useState('Ready to assist');
  const recognitionRef = useRef<any>(null);
  const audioRef = useRef<HTMLAudioElement>(null);
  const conversationRef = useRef<HTMLDivElement>(null);



  useEffect(() => {
    // Initialize speech recognition if available
    if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
      const SpeechRecognition = (window as any).webkitSpeechRecognition || (window as any).SpeechRecognition;
      recognitionRef.current = new SpeechRecognition();
      recognitionRef.current.continuous = false;
      recognitionRef.current.interimResults = false;
      recognitionRef.current.lang = 'en-US';

      recognitionRef.current.onstart = () => {
        setIsListening(true);
        setStatus('Listening...');
      };

      recognitionRef.current.onend = () => {
        setIsListening(false);
        setStatus('Ready to assist');
      };

      recognitionRef.current.onresult = (event: any) => {
        const transcript = event.results[0][0].transcript;
        handleUserInput(transcript);
      };

      recognitionRef.current.onerror = (event: any) => {
        console.error('Speech recognition error:', event.error);
        setIsListening(false);
        setStatus('Error occurred');
      };
    }

    // Keyboard event listener for spacebar
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.code === 'Space' && !event.repeat) {
        event.preventDefault();
        toggleListening();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  const toggleListening = () => {
    if (!recognitionRef.current) {
      alert('Speech recognition is not supported in this browser.');
      return;
    }

    if (isListening) {
      recognitionRef.current.stop();
    } else {
      recognitionRef.current.start();
    }
  };

  const handleUserInput = async (input: string) => {
    const userMessage: Message = {
      id: Date.now().toString(),
      type: 'user',
      content: input,
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    setStatus('Processing...');
    setIsProcessing(true);

    try {
      // Check for AIMCP context commands
      const aimcpResult = handleAIMCPCommand(input);
      if (aimcpResult.handled) {
        const aiMessage: Message = {
          id: (Date.now() + 1).toString(),
          type: 'ai',
          content: aimcpResult.message,
          timestamp: new Date()
        };
        setMessages(prev => [...prev, aiMessage]);
        
        if (speakerEnabled) {
          await speakText(aimcpResult.message);
        }
        return;
      }

      const response = await axios.post('/api/command/process', {
        input: input,
        preferredModule: getCurrentMCPContext()
      });

      if (response.data.success) {
        const aiMessage: Message = {
          id: (Date.now() + 1).toString(),
          type: 'ai',
          content: response.data.message || 'I processed your request.',
          timestamp: new Date()
        };
        setMessages(prev => [...prev, aiMessage]);
        
        // Speak the response if speaker is enabled
        if (speakerEnabled) {
          await speakText(response.data.message || 'I processed your request.');
        }
      } else {
        throw new Error(response.data.message || 'Failed to process command');
      }
    } catch (error: any) {
      console.error('Error processing command:', error);
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        type: 'ai',
        content: 'Sorry, I encountered an error processing your request. Please try again.',
        timestamp: new Date()
      };
      setMessages(prev => [...prev, errorMessage]);
      
      if (speakerEnabled) {
        await speakText('Sorry, I encountered an error processing your request.');
      }
    } finally {
      setStatus('Ready to assist');
      setIsProcessing(false);
    }
  };

  const speakText = async (text: string): Promise<void> => {
    if (!speakerEnabled || isSpeaking) return;
    
    setIsSpeaking(true);
    
    try {
      // Try OpenAI TTS first if API key is available
      const openaiApiKey = process.env.REACT_APP_OPENAI_API_KEY;
      
      if (openaiApiKey && openaiApiKey.startsWith('sk-')) {
        try {
          const response = await fetch('https://api.openai.com/v1/audio/speech', {
            method: 'POST',
            headers: {
              'Authorization': `Bearer ${openaiApiKey}`,
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({
              model: 'tts-1',
              input: text,
              voice: 'alloy',
              speed: 1.0
            })
          });
          
          if (response.ok) {
            const audioBlob = await response.blob();
            const audioUrl = URL.createObjectURL(audioBlob);
            const audio = new Audio(audioUrl);
            
            audio.onended = () => {
              setIsSpeaking(false);
              URL.revokeObjectURL(audioUrl);
            };
            
            audio.onerror = () => {
              setIsSpeaking(false);
              URL.revokeObjectURL(audioUrl);
              // Fallback to browser TTS
              fallbackToSpeechSynthesis(text);
            };
            
            await audio.play();
            return;
          }
        } catch (error) {
          console.warn('OpenAI TTS failed, falling back to browser TTS:', error);
        }
      }
      
      // Fallback to browser's SpeechSynthesis
      fallbackToSpeechSynthesis(text);
    } catch (error) {
      console.error('Error in text-to-speech:', error);
      setIsSpeaking(false);
    }
  };

  const fallbackToSpeechSynthesis = (text: string) => {
    if ('speechSynthesis' in window) {
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.rate = 1;
      utterance.pitch = 1;
      utterance.volume = 1;
      
      utterance.onend = () => {
        setIsSpeaking(false);
      };
      
      utterance.onerror = () => {
        setIsSpeaking(false);
      };
      
      speechSynthesis.speak(utterance);
    } else {
      setIsSpeaking(false);
    }
  };

  const toggleSpeaker = () => {
    setSpeakerEnabled(!speakerEnabled);
    if (isSpeaking && !speakerEnabled) {
      // Stop current speech if disabling speaker
      if ('speechSynthesis' in window) {
        speechSynthesis.cancel();
      }
      setIsSpeaking(false);
    }
  };

  // Quick commands removed as requested

  useEffect(() => {
    if (conversationRef.current) {
      conversationRef.current.scrollTop = conversationRef.current.scrollHeight;
    }
  }, [messages]);

  // Handle real-time command responses from SignalR
  useEffect(() => {
    if (lastCommandResponse) {
      setIsProcessing(false);
      
      const aiMessage: Message = {
        id: (Date.now() + 1).toString(),
        type: 'ai',
        content: lastCommandResponse.message || 'Command processed.',
        timestamp: new Date()
      };
      setMessages(prev => [...prev, aiMessage]);
      
      // Speak the response if speaker is enabled
      if (lastCommandResponse.success && speakerEnabled) {
        speakText(lastCommandResponse.message);
      }
    }
  }, [lastCommandResponse, speakerEnabled]);

  // Handle email notifications from SignalR
  useEffect(() => {
    const emailNotifications = notifications.filter(n => n.moduleId === 'EmailMcp' && n.type === 'NewEmail');
    if (emailNotifications.length > 0) {
      const latestEmail = emailNotifications[0];
      const subject = latestEmail.data?.Subject || 'No Subject';
      const from = latestEmail.data?.From || 'Unknown Sender';
      
      // Show flash message
      setEmailFlash({ show: true, subject, from });
      
      // Auto-hide flash after 5 seconds
      setTimeout(() => {
        setEmailFlash(prev => ({ ...prev, show: false }));
      }, 5000);
      
      // Speak the email notification if speaker is enabled
      if (speakerEnabled) {
        const voiceMessage = latestEmail.metadata?.voiceMessage || `New email from ${from}. Subject: ${subject}`;
        speakText(voiceMessage);
      }
      
      console.log('📧 Email notification processed:', { subject, from });
    }
  }, [notifications, speakerEnabled]);

  return (
    <div className="home-container">
      <GlobalHeader title="AIMCP Virtual Assistant" />
      <NotificationCenter />
      
      {/* MCP Context Indicator */}
      {currentMCPContext && (
        <div className="mcp-context-indicator">
          <div className="mcp-context-content">
            <span className="mcp-context-icon">🎯</span>
            <span className="mcp-context-text">
              Active Context: <strong>{currentMCPContext.toUpperCase()}</strong>
            </span>
            <span className="mcp-context-hint">
              Commands will be routed to {currentMCPContext} module
            </span>
            <button 
              className="mcp-context-exit"
              onClick={() => {
                setCurrentMCPContext(null);
                const exitMessage: Message = {
                  id: Date.now().toString(),
                  type: 'ai',
                  content: `✅ Exited ${currentMCPContext?.toUpperCase()} context. Commands will now be routed automatically based on intent.`,
                  timestamp: new Date()
                };
                setMessages(prev => [...prev, exitMessage]);
              }}
              title="Exit current context"
            >
              ×
            </button>
          </div>
        </div>
      )}
      
      {/* Email Flash Notification */}
      {emailFlash.show && (
        <div className="email-flash-notification">
          <div className="email-flash-content">
            <div className="email-flash-icon">📧</div>
            <div className="email-flash-text">
              <div className="email-flash-title">New Email</div>
              <div className="email-flash-from">From: {emailFlash.from}</div>
              <div className="email-flash-subject">Subject: {emailFlash.subject}</div>
            </div>
            <button 
              className="email-flash-close"
              onClick={() => setEmailFlash(prev => ({ ...prev, show: false }))}
            >
              ×
            </button>
          </div>
        </div>
      )}

      <main className="main-content">
        <div className="ai-avatar">
          <div className={`avatar-circle ${isListening ? 'listening' : ''}`}>
            <img src="/ai-audio.jpg" alt="AI Assistant" className="microphone-icon" />
            <div className="pulse-ring ring-1"></div>
            <div className="pulse-ring ring-2"></div>
            <div className="pulse-ring ring-3"></div>
          </div>
        </div>

        <div className="status-indicator">
          <span className="status-dot"></span>
          <span className="status-text">{status}</span>
        </div>

        <div className="voice-controls">
          {/* Speaker Toggle */}
          <button
            onClick={toggleSpeaker}
            className={`speaker-button ${speakerEnabled ? 'enabled' : 'disabled'}`}
            title={speakerEnabled ? 'Speaker On' : 'Speaker Off'}
          >
            {speakerEnabled ? '🔊' : '🔇'}
          </button>
          
          <button 
            className={`mic-button ${isListening ? 'listening' : isProcessing ? 'processing' : ''}`}
            onClick={toggleListening}
            disabled={isProcessing || !isConnected}
          >
            <img src="/ai-audio.jpg" alt="Microphone" className="mic-icon" />
          </button>
          <div className="tap-text">
            {isListening ? 'Listening...' : 
             isProcessing ? 'Processing...' : 
             isSpeaking ? 'Speaking...' : 
             'Tap to speak'}
          </div>
          
          <div className="voice-visualizer">
            {Array.from({ length: 5 }, (_, i) => (
              <div 
                key={i} 
                className={`visualizer-bar ${(isListening || isProcessing || isSpeaking) ? 'active' : ''}`}
                style={{ animationDelay: `${i * 0.1}s` }}
              ></div>
            ))}
          </div>
        </div>

        <div className="conversation-area" ref={conversationRef}>
          {messages.map((message) => (
            <div key={message.id} className={`message ${message.type}`}>
              <div className="message-avatar">
                {message.type === 'ai' ? '🤖' : '👤'}
              </div>
              <div className="message-content">
                <div className="message-text">{message.content}</div>
                <div className="message-time">
                  {message.timestamp.toLocaleTimeString()}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Quick Commands removed as requested */}
      </main>

      <nav className="bottom-navigation">
        <button className="nav-button active">
          <span className="nav-icon">🏠</span>
          <span className="nav-label">Home</span>
        </button>
        <button className="nav-button" onClick={() => navigate('/dashboard')}>
          <span className="nav-icon">⚙️</span>
          <span className="nav-label">Dashboard</span>
        </button>
      </nav>
      
      {/* Email Notification Test Component */}
      <EmailNotificationTest />
 
    </div>
  );
};

export default Home;