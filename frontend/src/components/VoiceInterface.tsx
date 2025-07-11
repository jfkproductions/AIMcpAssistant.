import React, { useState, useEffect, useRef } from 'react';
import { MicrophoneIcon, StopIcon, SpeakerWaveIcon, SpeakerXMarkIcon, ChevronDownIcon } from '@heroicons/react/24/solid';
import axios from '../utils/axios';
import { useSignalR } from '../contexts/SignalRContext';
import { useAIMCPContext } from '../hooks/useAIMCPContext';
import './VoiceInterface.css';


interface VoiceInterfaceProps {
  className?: string;
}

interface SpeechRecognitionEvent {
  results: SpeechRecognitionResultList;
  resultIndex: number;
}

interface SpeechRecognitionErrorEvent {
  error: string;
  message: string;
}

declare global {
  interface Window {
    SpeechRecognition: any;
    webkitSpeechRecognition: any;
  }
}

interface ModuleInfo {
  Enabled: boolean;
  Registered: boolean;
  Name: string;
  Description: string;
}

const VoiceInterface: React.FC<VoiceInterfaceProps> = ({ className = '' }) => {
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [response, setResponse] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [speakerEnabled, setSpeakerEnabled] = useState(true);
  const [error, setError] = useState('');
  const [conversationHistory, setConversationHistory] = useState<Array<{id: string, type: 'user' | 'assistant', content: string, timestamp: Date}>>([]);
  const [enabledModules, setEnabledModules] = useState<Record<string, ModuleInfo>>({});
  const [selectedModule, setSelectedModule] = useState<string>('auto');
  const [showModuleDropdown, setShowModuleDropdown] = useState(false);
  const [emailWorkflowState, setEmailWorkflowState] = useState<{
    active: boolean;
    step: 'notification' | 'askRead' | 'reading' | 'askDelete' | 'askReply' | 'replyMode';
    currentEmail?: any;
    waitingForResponse?: boolean;
  }>({ active: false, step: 'notification' });
  
  const recognitionRef = useRef<any>(null);
  const audioRef = useRef<HTMLAudioElement>(null);
  const { lastCommandResponse, isConnected, notifications } = useSignalR();
  const { currentMCPContext, handleAIMCPCommand, getCurrentMCPContext, setCurrentMCPContext } = useAIMCPContext();

  const fetchEnabledModules = async () => {
    try {
      const response = await axios.get('/api/module/settings');
      const modules = response.data.modules || {};
      const enabled: Record<string, ModuleInfo> = {};
      Object.entries(modules).forEach(([key, module]: [string, any]) => {
        if (module.Enabled && module.Registered) {
          enabled[key] = module as ModuleInfo;
        }
      });
      setEnabledModules(enabled);
    } catch (error) {
      console.error('Error fetching enabled modules:', error);
    }
  };

  useEffect(() => {
    fetchEnabledModules();
  }, []);

  useEffect(() => {
    // Initialize speech recognition
    if ('SpeechRecognition' in window || 'webkitSpeechRecognition' in window) {
      const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      recognitionRef.current = new SpeechRecognition();
      
      recognitionRef.current.continuous = false;
      recognitionRef.current.interimResults = false;
      recognitionRef.current.lang = 'en-US';
      
      recognitionRef.current.onresult = (event: SpeechRecognitionEvent) => {
        const spokenText = event.results[0][0].transcript;
        setTranscript(spokenText);
        handleVoiceCommand(spokenText);
      };
      
      recognitionRef.current.onerror = (event: SpeechRecognitionErrorEvent) => {
        console.error('Speech recognition error:', event.error);
        setError(`Speech recognition error: ${event.error}`);
        setIsListening(false);
      };
      
      recognitionRef.current.onend = () => {
        setIsListening(false);
      };
    } else {
      setError('Speech recognition not supported in this browser');
    }
  }, []);

  useEffect(() => {
    // Handle real-time command responses from SignalR
    if (lastCommandResponse) {
      setResponse(lastCommandResponse.message);
      setIsProcessing(false);
      
      // Add to conversation history
      addToHistory('assistant', lastCommandResponse.message);
      
      // Speak the response if speaker is enabled
      if (lastCommandResponse.success && speakerEnabled) {
        speakText(lastCommandResponse.message);
      }
    }
  }, [lastCommandResponse]);

  useEffect(() => {
    // Handle email notifications
    const emailNotifications = notifications.filter(n => 
      n.moduleId === 'EmailMcp' && 
      n.type === 'NewEmail' && 
      n.metadata?.requiresVoiceResponse
    );
    
    if (emailNotifications.length > 0 && !emailWorkflowState.active) {
      const latestEmail = emailNotifications[0];
      handleEmailNotification(latestEmail);
    }
  }, [notifications, emailWorkflowState.active]);

  const addToHistory = (type: 'user' | 'assistant', content: string) => {
    const newEntry = {
      id: Date.now().toString(),
      type,
      content,
      timestamp: new Date()
    };
    setConversationHistory(prev => [newEntry, ...prev].slice(0, 20)); // Keep last 20 entries
  };

  const startListening = () => {
    if (recognitionRef.current && !isListening) {
      setError('');
      setIsListening(true);
      recognitionRef.current.start();
    }
  };

  const stopListening = () => {
    if (recognitionRef.current && isListening) {
      recognitionRef.current.stop();
      setIsListening(false);
    }
  };

  const sendCommand = async (command: string) => {
    try {
      setIsProcessing(true);
      setError('');
      
      // Check if this is an AIMCP context command
      const aimcpResult = handleAIMCPCommand(command);
      if (aimcpResult.handled) {
        // Add to conversation history
        const userEntry = {
          id: Date.now().toString() + '-user',
          type: 'user' as const,
          content: command,
          timestamp: new Date()
        };
        
        const assistantEntry = {
          id: Date.now().toString() + '-assistant',
          type: 'assistant' as const,
          content: aimcpResult.message,
          timestamp: new Date()
        };
        
        setConversationHistory(prev => [...prev, userEntry, assistantEntry]);
        setResponse(aimcpResult.message);
        
        // Speak the response if speaker is enabled
        if (speakerEnabled && aimcpResult.message) {
          await speakText(aimcpResult.message);
        }
        
        setIsProcessing(false);
        return;
      }
      
      const requestBody: any = {
        input: command
      };
      
      // Add preferred module if one is selected, or use current MCP context
      const contextModule = getCurrentMCPContext();
      if (contextModule) {
        requestBody.preferredModule = contextModule;
      } else if (selectedModule !== 'auto') {
        requestBody.preferredModule = selectedModule;
      }
      
      const response = await axios.post('/api/command/process', requestBody);
      
      const assistantResponse = response.data.message || 'Command processed successfully';
      setResponse(assistantResponse);
      
      // Add to conversation history
      const userEntry = {
        id: Date.now().toString() + '-user',
        type: 'user' as const,
        content: command,
        timestamp: new Date()
      };
      
      const assistantEntry = {
        id: Date.now().toString() + '-assistant',
        type: 'assistant' as const,
        content: assistantResponse,
        timestamp: new Date()
      };
      
      setConversationHistory(prev => [...prev, userEntry, assistantEntry]);
      
      // Speak the response if speaker is enabled
      if (speakerEnabled && assistantResponse) {
        await speakText(assistantResponse);
      }
    } catch (error: any) {
      console.error('Error sending command:', error);
      const errorMessage = error.response?.data?.message || 'Failed to process command';
      setError(errorMessage);
      setResponse('');
    } finally {
      setIsProcessing(false);
    }
  };

  const handleVoiceCommand = async (spokenText: string) => {
    // Add user input to history
    addToHistory('user', spokenText);
    
    // Check for stop commands first
    const normalizedText = spokenText.toLowerCase();
    if ((normalizedText.includes('stop') && (normalizedText.includes('reading') || normalizedText.includes('speaking') || normalizedText.includes('talking'))) ||
        normalizedText.includes('stop reading') || normalizedText.includes('stop speaking') || normalizedText.includes('stop talking')) {
      stopSpeaking();
      addToHistory('assistant', 'Stopped speaking.');
      
      // If we're in an email workflow and currently reading, transition to ask delete
      if (emailWorkflowState.active && emailWorkflowState.step === 'reading') {
        setTimeout(async () => {
          const deleteMessage = 'Would you like me to delete this email?';
          await speakText(deleteMessage);
          
          setEmailWorkflowState(prev => ({
            ...prev,
            step: 'askDelete',
            waitingForResponse: true
          }));
          
          addToHistory('assistant', deleteMessage);
          
          // Automatically start listening for user response
          setTimeout(() => {
            if (!isListening) {
              startListening();
            }
          }, 500);
        }, 500);
      }
      return;
    }
    
    // Check if we're in an email workflow
    if (emailWorkflowState.active) {
      await handleEmailWorkflowResponse(spokenText);
    } else {
      // Use the sendCommand function
      await sendCommand(spokenText);
    }
  };

  const handleEmailNotification = async (notification: any) => {
    const emailData = notification.data;
    
    setEmailWorkflowState({
      active: true,
      step: 'notification',
      currentEmail: emailData,
      waitingForResponse: false
    });
    
    // Speak "New Email" and ask if user wants to read it
    const message = notification.metadata?.voiceMessage || 'New Email';
    await speakText(message);
    
    // Wait a moment then ask if they want to read it
    setTimeout(async () => {
      const askMessage = `You have a new email from ${emailData.From} with subject "${emailData.Subject}". Would you like me to read it to you?`;
      await speakText(askMessage);
      
      setEmailWorkflowState(prev => ({
        ...prev,
        step: 'askRead',
        waitingForResponse: true
      }));
      
      addToHistory('assistant', askMessage);
      
      // Automatically start listening for user response
      setTimeout(() => {
        if (!isListening) {
          startListening();
        }
      }, 500);
    }, 1000);
  };

  const handleEmailWorkflowResponse = async (spokenText: string) => {
    const normalizedText = spokenText.toLowerCase();
    
    switch (emailWorkflowState.step) {
      case 'askRead':
        if (normalizedText.includes('yes') || normalizedText.includes('read') || normalizedText.includes('sure')) {
          await readEmailAloud();
        } else if (normalizedText.includes('no') || normalizedText.includes('skip')) {
          await endEmailWorkflow('Okay, I won\'t read the email.');
        } else {
          await speakText('I didn\'t understand. Please say yes to read the email or no to skip it.');
        }
        break;
        
      case 'askDelete':
        if (normalizedText.includes('yes') || normalizedText.includes('delete')) {
          await deleteEmail();
        } else if (normalizedText.includes('no') || normalizedText.includes('keep')) {
          await askAboutReply();
        } else {
          await speakText('Please say yes to delete the email or no to keep it.');
        }
        break;
        
      case 'askReply':
        if (normalizedText.includes('yes') || normalizedText.includes('reply')) {
          await askReplyMode();
        } else if (normalizedText.includes('no') || normalizedText.includes('done')) {
          await endEmailWorkflow('Okay, we\'re done with this email.');
        } else {
          await speakText('Please say yes to reply to the email or no if you\'re done.');
        }
        break;
        
      case 'replyMode':
        if (normalizedText.includes('custom') || normalizedText.includes('myself')) {
          await handleCustomReply();
        } else if (normalizedText.includes('ai') || normalizedText.includes('generate')) {
          await handleAIReply(spokenText);
        } else {
          await speakText('Please say "custom" to write your own reply or "AI" to have me generate a response.');
        }
        break;
    }
  };

  const readEmailAloud = async () => {
    setEmailWorkflowState(prev => ({ ...prev, step: 'reading', waitingForResponse: false }));
    
    const email = emailWorkflowState.currentEmail;
    const emailContent = `Email from ${email.From}, subject: ${email.Subject}. ${email.Snippet || 'No preview available.'}`;
    
    addToHistory('assistant', `Reading email: ${emailContent}`);
    await speakText(emailContent);
    
    // After reading, ask about deletion and automatically start listening
    setTimeout(async () => {
      const deleteMessage = 'Would you like me to delete this email?';
      await speakText(deleteMessage);
      
      setEmailWorkflowState(prev => ({
        ...prev,
        step: 'askDelete',
        waitingForResponse: true
      }));
      
      addToHistory('assistant', deleteMessage);
      
      // Automatically start listening for user response
      setTimeout(() => {
        if (!isListening) {
          startListening();
        }
      }, 500);
    }, 1000);
  };

  const deleteEmail = async () => {
    // Note: Email deletion is not implemented in the backend yet
    const message = 'Email deletion feature is not yet implemented for safety reasons. Let me ask about replying instead.';
    await speakText(message);
    addToHistory('assistant', message);
    
    setTimeout(() => {
      askAboutReply();
    }, 1000);
  };

  const askAboutReply = async () => {
    const replyMessage = 'Would you like to reply to this email?';
    await speakText(replyMessage);
    
    setEmailWorkflowState(prev => ({
      ...prev,
      step: 'askReply',
      waitingForResponse: true
    }));
    
    addToHistory('assistant', replyMessage);
    
    // Automatically start listening for user response
    setTimeout(() => {
      if (!isListening) {
        startListening();
      }
    }, 500);
  };

  const askReplyMode = async () => {
    const modeMessage = 'How would you like to reply? Say "custom" to write your own message, or "AI" to have me generate a response based on your instructions.';
    await speakText(modeMessage);
    
    setEmailWorkflowState(prev => ({
      ...prev,
      step: 'replyMode',
      waitingForResponse: true
    }));
    
    addToHistory('assistant', modeMessage);
    
    // Automatically start listening for user response
    setTimeout(() => {
      if (!isListening) {
        startListening();
      }
    }, 500);
  };

  const handleCustomReply = async () => {
    const message = 'Custom email composition is not yet implemented. This feature will allow you to dictate your reply.';
    await speakText(message);
    addToHistory('assistant', message);
    await endEmailWorkflow();
  };

  const handleAIReply = async (instructions: string) => {
    const message = 'AI-generated email replies are not yet implemented. This feature will generate a response based on your instructions.';
    await speakText(message);
    addToHistory('assistant', message);
    await endEmailWorkflow();
  };

  const endEmailWorkflow = async (message?: string) => {
    if (message) {
      await speakText(message);
      addToHistory('assistant', message);
    }
    
    setEmailWorkflowState({
      active: false,
      step: 'notification'
    });
  };

  const speakText = async (text: string): Promise<void> => {
    return new Promise(async (resolve) => {
      try {
        setIsSpeaking(true);
        
        // Check if OpenAI API key is available
        const apiKey = process.env.REACT_APP_OPENAI_API_KEY;
        console.log('OpenAI API Key available:', !!apiKey);
        
        if (apiKey && apiKey.startsWith('sk-')) {
          console.log('Attempting OpenAI TTS...');
          // Use OpenAI TTS API
          const response = await axios.post(
            'https://api.openai.com/v1/audio/speech',
            {
              model: 'tts-1',
              voice: 'nova',
              input: text,
            },
            {
              responseType: 'blob',
              headers: {
                'Authorization': `Bearer ${apiKey}`,
                'Content-Type': 'application/json',
              },
            }
          );
          
          console.log('OpenAI TTS response received, creating audio URL...');
          const audioUrl = URL.createObjectURL(response.data);
          
          if (audioRef.current) {
            audioRef.current.src = audioUrl;
            audioRef.current.onended = () => {
              console.log('Audio playback ended');
              setIsSpeaking(false);
              URL.revokeObjectURL(audioUrl);
              resolve();
            };
            audioRef.current.onerror = (e) => {
              console.error('Audio playback error:', e);
              setIsSpeaking(false);
              URL.revokeObjectURL(audioUrl);
              resolve();
            };
            console.log('Starting audio playback...');
            await audioRef.current.play();
          }
        } else {
          console.log('No valid OpenAI API key, using browser speech synthesis');
          throw new Error('No valid OpenAI API key');
        }
      } catch (error) {
        console.error('Error with OpenAI TTS, falling back to browser speech synthesis:', error);
        // Fallback to browser's speech synthesis
        if ('speechSynthesis' in window) {
          console.log('Using browser speech synthesis...');
          const utterance = new SpeechSynthesisUtterance(text);
          utterance.rate = 0.9;
          utterance.pitch = 1;
          utterance.volume = 1;
          utterance.onstart = () => {
            console.log('Speech synthesis started');
          };
          utterance.onend = () => {
            console.log('Speech synthesis ended');
            setIsSpeaking(false);
            resolve();
          };
          utterance.onerror = (e) => {
            console.error('Speech synthesis error:', e);
            setIsSpeaking(false);
            resolve();
          };
          speechSynthesis.speak(utterance);
        } else {
          console.error('Speech synthesis not supported');
          setIsSpeaking(false);
          resolve();
        }
      }
    });
  };

  const stopSpeaking = () => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.currentTime = 0;
    }
    if ('speechSynthesis' in window) {
      speechSynthesis.cancel();
    }
    setIsSpeaking(false);
  };

  const clearHistory = () => {
    setConversationHistory([]);
    setTranscript('');
    setResponse('');
    setError('');
  };

  return (
    <div className={`max-w-4xl mx-auto p-6 ${className}`}>
      <div className="bg-white rounded-lg shadow-lg p-6">
        <div className="text-center mb-8">
          <div className="flex justify-between items-start mb-4">
            <div className="flex-1"></div>
            <div className="flex-1">
              <h1 className="text-3xl font-bold text-gray-900 mb-2">🎤 Voice Assistant</h1>
              <p className="text-gray-600">
                Click the microphone to start talking, or use the conversation history below
              </p>
            </div>
            <div className="flex-1 flex justify-end">
              {/* Module settings moved to dashboard */}
            </div>
          </div>
          <div className="flex items-center justify-center space-x-2">
            <div className={`w-3 h-3 rounded-full ${
              isConnected ? 'bg-green-500' : 'bg-red-500'
            }`}></div>
            <span className="text-sm text-gray-500">
              {isConnected ? 'Connected' : 'Disconnected'}
            </span>
          </div>
        </div>

        {/* MCP Context Indicator */}
        {currentMCPContext && (
          <div className="mcp-context-indicator mb-6">
            <div className="mcp-context-content">
              <div className="mcp-context-badge">
                🎯 {currentMCPContext.toUpperCase()} Context Active
              </div>
              <div className="mcp-context-hint">
                All commands will be routed to the {currentMCPContext} module
              </div>
              <button 
                onClick={() => {
                  setCurrentMCPContext(null);
                  addToHistory('assistant', `✅ Exited ${currentMCPContext?.toUpperCase()} context. Commands will now be routed automatically based on intent.`);
                }}
                className="mcp-context-exit"
              >
                Exit Context
              </button>
            </div>
          </div>
        )}

        {/* Module Selection */}
        <div className="mb-6">
          <div className="relative max-w-xs mx-auto">
            <button
              onClick={() => setShowModuleDropdown(!showModuleDropdown)}
              className="w-full flex items-center justify-between px-4 py-2 bg-white border border-gray-300 rounded-lg shadow-sm hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <span className="text-sm font-medium text-gray-700">
                {selectedModule === 'auto' ? 'Auto-select Module' : enabledModules[selectedModule]?.Name || selectedModule}
              </span>
              <ChevronDownIcon className="w-4 h-4 text-gray-500" />
            </button>
            
            {showModuleDropdown && (
              <div className="absolute z-10 w-full mt-1 bg-white border border-gray-300 rounded-lg shadow-lg">
                <button
                  onClick={() => {
                    setSelectedModule('auto');
                    setShowModuleDropdown(false);
                  }}
                  className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-50 ${
                    selectedModule === 'auto' ? 'bg-blue-50 text-blue-700' : 'text-gray-700'
                  }`}
                >
                  Auto-select Module
                </button>
                {Object.entries(enabledModules).map(([key, module]) => (
                  <button
                    key={key}
                    onClick={() => {
                      setSelectedModule(key);
                      setShowModuleDropdown(false);
                    }}
                    className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-50 ${
                      selectedModule === key ? 'bg-blue-50 text-blue-700' : 'text-gray-700'
                    }`}
                  >
                    <div className="font-medium">{module.Name}</div>
                    <div className="text-xs text-gray-500">{module.Description}</div>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Voice Controls */}
        <div className="flex justify-center space-x-4 mb-8">
          <button
            onClick={isListening ? stopListening : startListening}
            disabled={isProcessing}
            className={`flex items-center justify-center w-16 h-16 rounded-full transition-all duration-200 ${
              isListening
                ? 'bg-red-500 hover:bg-red-600 animate-pulse'
                : 'bg-blue-500 hover:bg-blue-600'
            } text-white disabled:opacity-50 disabled:cursor-not-allowed`}
          >
            {isListening ? (
              <StopIcon className="w-8 h-8" />
            ) : (
              <MicrophoneIcon className="w-8 h-8" />
            )}
          </button>
          
          <button
            onClick={() => {
              if (isSpeaking) {
                stopSpeaking();
              } else {
                setSpeakerEnabled(!speakerEnabled);
              }
            }}
            className={`flex items-center justify-center w-16 h-16 rounded-full transition-all duration-200 ${
              isSpeaking
                ? 'bg-green-500 hover:bg-green-600 animate-pulse'
                : speakerEnabled
                ? 'bg-green-500 hover:bg-green-600'
                : 'bg-gray-400 hover:bg-gray-500'
            } text-white`}
            title={isSpeaking ? 'Stop speaking' : speakerEnabled ? 'Disable speaker' : 'Enable speaker'}
          >
            {isSpeaking ? (
              <SpeakerXMarkIcon className="w-8 h-8" />
            ) : speakerEnabled ? (
              <SpeakerWaveIcon className="w-8 h-8" />
            ) : (
              <SpeakerXMarkIcon className="w-8 h-8" />
            )}
          </button>
        </div>

        {/* Status */}
        <div className="text-center mb-6">
          {isListening && (
            <p className="text-blue-600 font-medium">🎤 Listening...</p>
          )}
          {isProcessing && (
            <p className="text-yellow-600 font-medium">🤔 Processing...</p>
          )}
          {isSpeaking && (
            <p className="text-green-600 font-medium">🔊 Speaking...</p>
          )}
          {error && (
            <p className="text-red-600 font-medium">❌ {error}</p>
          )}
        </div>

        {/* Current Interaction */}
        {(transcript || response) && (
          <div className="bg-gray-50 rounded-lg p-4 mb-6">
            {transcript && (
              <div className="mb-3">
                <p className="text-sm font-medium text-gray-700 mb-1">You said:</p>
                <p className="text-gray-900">{transcript}</p>
              </div>
            )}
            {response && (
              <div>
                <p className="text-sm font-medium text-gray-700 mb-1">Assistant:</p>
                <p className="text-gray-900">{response}</p>
              </div>
            )}
          </div>
        )}

        {/* Conversation History */}
        <div className="border-t pt-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-lg font-semibold text-gray-900">Conversation History</h3>
            {conversationHistory.length > 0 && (
              <button
                onClick={clearHistory}
                className="text-sm text-red-600 hover:text-red-800"
              >
                Clear History
              </button>
            )}
          </div>
          
          <div className="space-y-3 max-h-96 overflow-y-auto">
            {conversationHistory.length === 0 ? (
              <p className="text-gray-500 text-center py-8">
                No conversation yet. Start by saying something!
              </p>
            ) : (
              conversationHistory.map((entry) => (
                <div
                  key={entry.id}
                  className={`flex ${entry.type === 'user' ? 'justify-end' : 'justify-start'}`}
                >
                  <div
                    className={`max-w-xs lg:max-w-md px-4 py-2 rounded-lg ${
                      entry.type === 'user'
                        ? 'bg-blue-500 text-white'
                        : 'bg-gray-200 text-gray-900'
                    }`}
                  >
                    <p className="text-sm">{entry.content}</p>
                    <p className="text-xs opacity-75 mt-1">
                      {entry.timestamp.toLocaleTimeString()}
                    </p>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
      
      {/* Hidden audio element for TTS */}
      <audio ref={audioRef} style={{ display: 'none' }} />
    </div>
  );
};

export default VoiceInterface;