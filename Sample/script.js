class AIVoiceAssistant {
    constructor() {
        this.isListening = false;
        this.recognition = null;
        this.conversationHistory = [];
        this.initializeElements();
        this.initializeSpeechRecognition();
        this.bindEvents();
    }

    initializeElements() {
        this.voiceBtn = document.getElementById('voiceBtn');
        this.micIcon = document.getElementById('micIcon');
        this.visualizer = document.getElementById('visualizer');
        this.conversation = document.getElementById('conversation');
        this.statusText = document.querySelector('.status-text');
        this.statusDot = document.querySelector('.status-dot');
    }

    initializeSpeechRecognition() {
        if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
            const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
            this.recognition = new SpeechRecognition();
            this.recognition.continuous = false;
            this.recognition.interimResults = false;
            this.recognition.lang = 'en-US';

            this.recognition.onstart = () => {
                this.setListeningState(true);
            };

            this.recognition.onresult = (event) => {
                const transcript = event.results[0][0].transcript;
                this.handleUserInput(transcript);
            };

            this.recognition.onerror = (event) => {
                console.error('Speech recognition error:', event.error);
                this.setListeningState(false);
                this.updateStatus('Error occurred. Try again.', 'error');
            };

            this.recognition.onend = () => {
                this.setListeningState(false);
            };
        } else {
            this.updateStatus('Speech recognition not supported', 'error');
        }
    }

    bindEvents() {
        this.voiceBtn.addEventListener('click', () => {
            this.toggleListening();
        });

        // Add keyboard shortcut
        document.addEventListener('keydown', (e) => {
            if (e.code === 'Space' && !this.isListening) {
                e.preventDefault();
                this.startListening();
            }
        });

        document.addEventListener('keyup', (e) => {
            if (e.code === 'Space' && this.isListening) {
                e.preventDefault();
                this.stopListening();
            }
        });
    }

    toggleListening() {
        if (this.isListening) {
            this.stopListening();
        } else {
            this.startListening();
        }
    }

    startListening() {
        if (!this.recognition) return;
        
        try {
            this.recognition.start();
            this.updateStatus('Listening...', 'listening');
        } catch (error) {
            console.error('Error starting recognition:', error);
        }
    }

    stopListening() {
        if (this.recognition && this.isListening) {
            this.recognition.stop();
        }
    }

    setListeningState(listening) {
        this.isListening = listening;
        this.voiceBtn.classList.toggle('listening', listening);
        this.visualizer.classList.toggle('active', listening);
        
        if (listening) {
            this.micIcon.className = 'fas fa-stop';
        } else {
            this.micIcon.className = 'fas fa-microphone';
            this.updateStatus('Ready to assist', 'ready');
        }
    }

    updateStatus(text, type = 'ready') {
        this.statusText.textContent = text;
        this.statusDot.className = 'status-dot';
        
        if (type === 'listening') {
            this.statusDot.style.background = '#ff6b6b';
        } else if (type === 'thinking') {
            this.statusDot.style.background = '#ffd93d';
        } else if (type === 'error') {
            this.statusDot.style.background = '#ff4757';
        } else {
            this.statusDot.style.background = '#00ff88';
        }
    }

    async handleUserInput(text) {
        this.addMessage(text, 'user');
        this.updateStatus('Thinking...', 'thinking');
        
        try {
            const response = await this.getAIResponse(text);
            this.addMessage(response, 'ai');
            this.speakResponse(response);
        } catch (error) {
            console.error('Error getting AI response:', error);
            this.addMessage('Sorry, I encountered an error. Please try again.', 'ai');
        }
        
        this.updateStatus('Ready to assist', 'ready');
    }

    async getAIResponse(userMessage) {
        const newMessage = {
            role: "user",
            content: userMessage
        };
        
        this.conversationHistory.push(newMessage);
        this.conversationHistory = this.conversationHistory.slice(-10);

        const completion = await websim.chat.completions.create({
            messages: [
                {
                    role: "system",
                    content: "You are a helpful AI voice assistant. Keep responses concise and conversational, as they will be spoken aloud. Be friendly and engaging. If asked about the time, weather, or other real-time information, provide a helpful response acknowledging the limitation but offer general assistance."
                },
                ...this.conversationHistory
            ]
        });

        const response = completion.content;
        this.conversationHistory.push({
            role: "assistant",
            content: response
        });

        return response;
    }

    async speakResponse(text) {
        try {
            const result = await websim.textToSpeech({
                text: text,
                voice: "en-female"
            });
            
            const audio = new Audio(result.url);
            audio.play();
        } catch (error) {
            console.error('Error with text-to-speech:', error);
        }
    }

    addMessage(content, type) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${type}-message`;
        
        const avatar = document.createElement('div');
        avatar.className = 'message-avatar';
        avatar.innerHTML = type === 'ai' ? '<i class="fas fa-robot"></i>' : '<i class="fas fa-user"></i>';
        
        const messageContent = document.createElement('div');
        messageContent.className = 'message-content';
        messageContent.innerHTML = `<p>${content}</p>`;
        
        messageDiv.appendChild(avatar);
        messageDiv.appendChild(messageContent);
        
        this.conversation.appendChild(messageDiv);
        this.conversation.scrollTop = this.conversation.scrollHeight;
    }
}

// Quick command handler
function sendQuickCommand(command) {
    if (window.assistant) {
        window.assistant.handleUserInput(command);
    }
}

// Initialize the assistant when the page loads
document.addEventListener('DOMContentLoaded', () => {
    window.assistant = new AIVoiceAssistant();
});

// Add some visual feedback for the quick commands
document.addEventListener('DOMContentLoaded', () => {
    const commandBtns = document.querySelectorAll('.command-btn');
    commandBtns.forEach(btn => {
        btn.addEventListener('click', function() {
            this.style.transform = 'scale(0.95)';
            setTimeout(() => {
                this.style.transform = '';
            }, 150);
        });
    });
});

