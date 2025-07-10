import React, { useState, useEffect, useRef } from 'react';
import { MicrophoneIcon, StopIcon, SpeakerWaveIcon, SpeakerXMarkIcon } from '@heroicons/react/24/solid';
import axios from '../utils/axios';
import { useSignalR } from '../contexts/SignalRContext';

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

const DashboardVoiceInterface: React.FC = () => {
  const [isListening, setIsListening] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [speakerEnabled, setSpeakerEnabled] = useState(true);
  const [isProcessing, setIsProcessing] = useState(false);
  const [currentTranscript, setCurrentTranscript] = useState('');
  const [emailWorkflowState, setEmailWorkflowState] = useState<{
    active: boolean;
    step: 'notification' | 'askRead' | 'reading' | 'askDelete' | 'askReply' | 'replyMode';
    currentEmail?: any;
    waitingForResponse?: boolean;
  }>({ active: false, step: 'notification' });
  
  const recognitionRef = useRef<any>(null);
  const { lastCommandResponse, isConnected, notifications } = useSignalR();

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
        setCurrentTranscript(spokenText);
        handleVoiceCommand(spokenText);
      };
      
      recognitionRef.current.onerror = (event: SpeechRecognitionErrorEvent) => {
        console.error('Speech recognition error:', event.error);
        setIsListening(false);
      };
      
      recognitionRef.current.onend = () => {
        setIsListening(false);
      };
    }
  }, []);

  useEffect(() => {
    // Handle real-time command responses from SignalR
    if (lastCommandResponse) {
      setIsProcessing(false);
      
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

  const handleEmailNotification = (notification: any) => {
    console.log('Handling email notification:', notification);
    
    setEmailWorkflowState({
      active: true,
      step: 'askRead',
      currentEmail: notification,
      waitingForResponse: true
    });
    
    // Speak the notification
    const message = `New Email from ${notification.data?.from || 'unknown sender'}. Subject: ${notification.data?.subject || 'No subject'}. Would you like me to read it aloud?`;
    speakText(message);
  };

  const handleEmailWorkflowResponse = (response: string) => {
    const lowerResponse = response.toLowerCase();
    
    switch (emailWorkflowState.step) {
      case 'askRead':
        if (lowerResponse.includes('yes') || lowerResponse.includes('read')) {
          setEmailWorkflowState(prev => ({ ...prev, step: 'reading', waitingForResponse: false }));
          const emailContent = emailWorkflowState.currentEmail?.data?.content || 'Email content not available';
          speakText(`Reading email: ${emailContent}. Would you like to reply or delete this email?`);
          setTimeout(() => {
            setEmailWorkflowState(prev => ({ ...prev, step: 'askDelete', waitingForResponse: true }));
          }, 2000);
        } else {
          speakText('Okay, I won\'t read the email. Would you like to delete it or reply to it?');
          setEmailWorkflowState(prev => ({ ...prev, step: 'askDelete', waitingForResponse: true }));
        }
        break;
        
      case 'askDelete':
        if (lowerResponse.includes('delete')) {
          speakText('Email deleted successfully.');
          // TODO: Implement actual email deletion
          setEmailWorkflowState({ active: false, step: 'notification' });
        } else if (lowerResponse.includes('reply')) {
          speakText('What would you like to reply?');
          setEmailWorkflowState(prev => ({ ...prev, step: 'replyMode', waitingForResponse: true }));
        } else {
          speakText('I didn\'t understand. Please say "delete" to delete the email or "reply" to reply to it.');
        }
        break;
        
      case 'replyMode':
        speakText(`Sending reply: "${response}". Reply sent successfully.`);
        // TODO: Implement actual email reply
        setEmailWorkflowState({ active: false, step: 'notification' });
        break;
    }
  };

  const handleVoiceCommand = async (command: string) => {
    console.log('Voice command received:', command);
    
    // If we're in an email workflow, handle the response
    if (emailWorkflowState.active && emailWorkflowState.waitingForResponse) {
      handleEmailWorkflowResponse(command);
      return;
    }
    
    // Otherwise, process as a regular command
    try {
      setIsProcessing(true);
      
      const response = await axios.post('/api/command/process', {
        input: command
      });
      
      const assistantResponse = response.data.message || 'Command processed successfully';
      
      // Speak the response if speaker is enabled
      if (speakerEnabled && assistantResponse) {
        speakText(assistantResponse);
      }
    } catch (error) {
      console.error('Error processing voice command:', error);
      if (speakerEnabled) {
        speakText('Sorry, I encountered an error processing your command.');
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const speakText = async (text: string) => {
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

  const startListening = () => {
    if (recognitionRef.current && !isListening) {
      setIsListening(true);
      setCurrentTranscript('');
      recognitionRef.current.start();
    }
  };

  const stopListening = () => {
    if (recognitionRef.current && isListening) {
      recognitionRef.current.stop();
      setIsListening(false);
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

  return (
    <div className="fixed bottom-6 right-6 z-50">
      <div className="bg-white rounded-full shadow-lg border border-gray-200 p-4 flex items-center space-x-3">
        {/* Speaker Toggle */}
        <button
          onClick={toggleSpeaker}
          className={`p-2 rounded-full transition-colors ${
            speakerEnabled 
              ? 'bg-green-100 text-green-600 hover:bg-green-200' 
              : 'bg-gray-100 text-gray-400 hover:bg-gray-200'
          }`}
          title={speakerEnabled ? 'Speaker On' : 'Speaker Off'}
        >
          {speakerEnabled ? (
            <SpeakerWaveIcon className="h-5 w-5" />
          ) : (
            <SpeakerXMarkIcon className="h-5 w-5" />
          )}
        </button>

        {/* Microphone Button */}
        <button
          onClick={isListening ? stopListening : startListening}
          disabled={isProcessing || !isConnected}
          className={`p-3 rounded-full transition-all duration-200 ${
            isListening
              ? 'bg-red-500 text-white hover:bg-red-600 animate-pulse'
              : isProcessing
              ? 'bg-yellow-500 text-white'
              : 'bg-blue-500 text-white hover:bg-blue-600'
          } disabled:opacity-50 disabled:cursor-not-allowed`}
          title={isListening ? 'Stop Listening' : 'Start Listening'}
        >
          {isListening ? (
            <StopIcon className="h-6 w-6" />
          ) : (
            <MicrophoneIcon className="h-6 w-6" />
          )}
        </button>

        {/* Status Indicator */}
        {(isListening || isProcessing || isSpeaking) && (
          <div className="flex items-center space-x-2">
            <div className={`w-2 h-2 rounded-full ${
              isListening ? 'bg-red-500 animate-pulse' :
              isProcessing ? 'bg-yellow-500 animate-spin' :
              isSpeaking ? 'bg-green-500 animate-pulse' : 'bg-gray-400'
            }`}></div>
            <span className="text-xs text-gray-600">
              {isListening ? 'Listening...' :
               isProcessing ? 'Processing...' :
               isSpeaking ? 'Speaking...' : ''}
            </span>
          </div>
        )}

        {/* Current Transcript */}
        {currentTranscript && (
          <div className="max-w-xs">
            <p className="text-xs text-gray-600 truncate" title={currentTranscript}>
              "{currentTranscript}"
            </p>
          </div>
        )}
      </div>

      {/* Email Workflow Indicator */}
      {emailWorkflowState.active && (
        <div className="mt-2 bg-blue-50 border border-blue-200 rounded-lg p-3 max-w-sm">
          <div className="flex items-center space-x-2">
            <div className="w-2 h-2 bg-blue-500 rounded-full animate-pulse"></div>
            <span className="text-sm text-blue-700 font-medium">
              Email Assistant Active
            </span>
          </div>
          <p className="text-xs text-blue-600 mt-1">
            {emailWorkflowState.step === 'askRead' && 'Waiting for read confirmation...'}
            {emailWorkflowState.step === 'reading' && 'Reading email...'}
            {emailWorkflowState.step === 'askDelete' && 'Waiting for action (reply/delete)...'}
            {emailWorkflowState.step === 'replyMode' && 'Listening for reply message...'}
          </p>
        </div>
      )}
    </div>
  );
};

export default DashboardVoiceInterface;