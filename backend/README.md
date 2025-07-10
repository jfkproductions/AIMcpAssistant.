# AI MCP Assistant Backend

This is the backend API for the AI MCP (Model Context Protocol) Assistant application.

## Project Structure

- **AIMcpAssistant.Api** - Main API project with controllers and endpoints
- **AIMcpAssistant.Core** - Core business logic and interfaces
- **AIMcpAssistant.Data** - Data access layer with Entity Framework
- **AIMcpAssistant.Authentication** - Authentication services for Google and Microsoft
- **AIMcpAssistant.MCPs** - MCP modules for email, calendar, and weather
- **AIMcpAssistant.Tests** - Unit tests for all projects

## Features

- OAuth2 authentication with Google and Microsoft
- JWT token-based authorization
- Entity Framework Core with SQLite database
- MCP modules for:
  - Email management (Gmail/Outlook)
  - Calendar integration (Google Calendar/Outlook Calendar)
  - Weather information
- SignalR for real-time notifications
- Comprehensive unit tests

## Setup

1. **Prerequisites**
   - .NET 9.0 SDK
   - Visual Studio 2022 or VS Code
   - Google Cloud Console project (for OAuth)
   - OpenAI API key

2. **Google OAuth Configuration**
   
   Before configuring the application, set up Google OAuth:
   
   a. **Create Google Cloud Project**:
      - Go to [Google Cloud Console](https://console.cloud.google.com/)
      - Create a new project or select existing one
      - Enable Google+ API, Gmail API, and Google Calendar API
   
   b. **Configure OAuth Consent Screen**:
      - Navigate to APIs & Services > OAuth consent screen
      - Choose External user type
      - Fill in app information (name: "AI MCP Assistant")
      - Add required scopes for email and profile access
   
   c. **Create OAuth 2.0 Credentials**:
      - Navigate to APIs & Services > Credentials
      - Create OAuth 2.0 Client ID for Web application
      - Add authorized origins: `http://localhost:3000`
      - Add redirect URIs: `http://localhost:3000/auth/callback`
      - Save Client ID and Client Secret

3. **Environment Configuration**
   
   Create a `.env` file in the `AIMcpAssistant.Api` directory:
   
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
   
   # Weather API (optional)
   WEATHER_API_KEY=your_openweathermap_api_key_here
   ```
   
   Alternatively, update `appsettings.json` with your configuration values

4. **Database Setup**
   ```bash
   dotnet ef database update --project AIMcpAssistant.Data --startup-project AIMcpAssistant.ApiThis module is disabled in configuration
   ```

5. **Build and Run**
   ```bash
   dotnet build
   dotnet run --project AIMcpAssistant.Api
   ```

6. **Run Tests**
   ```bash
   dotnet test
   ```

## API Endpoints

### Authentication
- `GET /api/auth/login/google` - Initiate Google OAuth
- `GET /api/auth/login/microsoft` - Initiate Microsoft OAuth
- `GET /api/auth/callback/google` - Google OAuth callback
- `GET /api/auth/callback/microsoft` - Microsoft OAuth callback
- `POST /api/auth/refresh` - Refresh JWT token
- `POST /api/auth/logout` - Logout user
- `GET /api/auth/user` - Get current user info

### Commands
- `POST /api/command/process` - Process natural language commands
- `GET /api/command/modules` - Get available MCP modules
- `POST /api/command/analyze` - Analyze command intent

### System
- `GET /health` - Health check
- `GET /api/status` - MCP modules status

## Architecture

The application follows a clean architecture pattern with:

- **Controllers** - Handle HTTP requests and responses
- **Services** - Business logic implementation
- **Repositories** - Data access abstraction
- **Entities** - Database models
- **DTOs** - Data transfer objects
- **Interfaces** - Contracts for dependency injection

## Authentication Flow

1. User initiates OAuth with Google/Microsoft
2. After successful authentication, JWT token is generated
3. JWT token contains user info and OAuth tokens
4. Subsequent API calls use JWT for authorization
5. MCP modules use stored OAuth tokens to access external services

## MCP Modules

Each MCP module implements the `IMcpModule` interface and provides:
- Command processing capabilities
- Integration with external services
- User context awareness
- Error handling and logging

## Development

- Follow SOLID principles
- Use dependency injection for loose coupling
- Implement comprehensive unit tests
- Use async/await for I/O operations
- Follow RESTful API conventions