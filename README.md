# AI MCP Assistant Platform

A modular, voice-driven assistant platform with React frontend and C# (.NET) backend.

## Architecture Overview

```
┌───────────────┐         ┌──────────────┐
│ React Frontend│◄───────►│ Auth Service │
└─────▲──▲──────┘         └────┬─────────┘
      │  │                      │
  Voice UI  ▼                  ▼
     ┌──────┐         ┌─────────────────────┐
     │ Web  │◄───────►│ Command Dispatcher  │
     │Socket│         └────────┬────────────┘
     └──────┘                  │
                               ▼
         ┌────────────┬────────┬──────────────┐
         ▼            ▼        ▼              ▼
  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
  │Email MCP │ │Calendar  │ │Weather   │ │GitHub MCP│
  │ (Google &│ │ MCP      │ │ MCP      │ │          │
  │ Microsoft)│ └──────────┘ └──────────┘ └──────────┘
  └──────────┘
```

## Features

- **Voice Interface**: Speech-to-text using Web Speech API
- **Authentication**: Google OAuth and Microsoft Azure Entra ID
- **Modular MCPs**: Pluggable command processors for different domains
- **Real-time Updates**: WebSocket/SignalR for live notifications
- **OpenAI Integration**: ChatGPT for natural language processing and TTS
- **Office Integration**: Email and calendar access via Microsoft Graph API

## Project Structure

- `/backend` - ASP.NET Core Web API
- `/frontend` - React application
- `/shared` - Shared models and contracts

## Getting Started

### Prerequisites

1. **Google Cloud Console Setup** (for Google OAuth)
2. **OpenAI API Key** (for ChatGPT integration)
3. **.NET 9.0 SDK**
4. **Node.js 18+**

### Google OAuth Setup

#### 1. Create Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the following APIs:
   - Google+ API
   - Gmail API (if using email features)
   - Google Calendar API (if using calendar features)

#### 2. Configure OAuth Consent Screen

1. Navigate to **APIs & Services** > **OAuth consent screen**
2. Choose **External** user type
3. Fill in the required information:
   - **App name**: AI MCP Assistant
   - **User support email**: Your email
   - **Developer contact information**: Your email
4. Add scopes (if needed):
   - `../auth/userinfo.email`
   - `../auth/userinfo.profile`
   - `../auth/gmail.readonly` (for email features)
   - `../auth/calendar.readonly` (for calendar features)
5. Add test users (during development)

#### 3. Create OAuth 2.0 Credentials

1. Navigate to **APIs & Services** > **Credentials**
2. Click **Create Credentials** > **OAuth 2.0 Client IDs**
3. Choose **Web application**
4. Configure:
   - **Name**: AI MCP Assistant Web Client
   - **Authorized JavaScript origins**: 
     - `http://localhost:3000` (for development)
     - Your production domain (for production)
   - **Authorized redirect URIs**:
     - `http://localhost:3000/auth/callback` (for development)
     - `https://yourdomain.com/auth/callback` (for production)
5. Save the **Client ID** and **Client Secret**

### Environment Configuration

#### Backend (.env in `/backend/AIMcpAssistant.Api/`)

```env
# Google OAuth
GOOGLE_CLIENT_ID=your_google_client_id_here
GOOGLE_CLIENT_SECRET=your_google_client_secret_here
GOOGLE_REDIRECT_URI=http://localhost:3000/auth/callback

# OpenAI
OPENAI_API_KEY=your_openai_api_key_here

# Database
CONNECTION_STRING=Server=localhost;Database=AIMcpAssistant;Trusted_Connection=true;

# JWT
JWT_SECRET=your_jwt_secret_key_here
JWT_ISSUER=AIMcpAssistant
JWT_AUDIENCE=AIMcpAssistant
```

#### Frontend (.env in `/frontend/`)

```env
# Google OAuth
REACT_APP_GOOGLE_CLIENT_ID=your_google_client_id_here

# OpenAI (for client-side features)
REACT_APP_OPENAI_API_KEY=your_openai_api_key_here

# API Base URL
REACT_APP_API_BASE_URL=http://localhost:5000
```

### Installation & Running

#### Backend
```bash
cd backend
dotnet restore
dotnet run --project AIMcpAssistant.Api
```

The backend will start on `http://localhost:5000`

#### Frontend
```bash
cd frontend
npm install
npm start
```

The frontend will start on `http://localhost:3000`

### Testing Google OAuth

1. Start both backend and frontend servers
2. Navigate to `http://localhost:3000`
3. Click "Login with Google"
4. Complete the OAuth flow
5. You should be redirected back to the dashboard

### Troubleshooting

#### Common Issues

1. **"redirect_uri_mismatch" error**:
   - Ensure the redirect URI in Google Cloud Console matches exactly: `http://localhost:3000/auth/callback`
   - Check for trailing slashes and protocol (http vs https)

2. **"invalid_client" error**:
   - Verify your Google Client ID and Client Secret are correct
   - Ensure the OAuth consent screen is properly configured

3. **CORS errors**:
   - Ensure the frontend origin is added to "Authorized JavaScript origins" in Google Cloud Console

4. **Backend connection issues**:
   - Verify the backend is running on port 5000
   - Check that the `REACT_APP_API_BASE_URL` matches your backend URL