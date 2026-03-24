# 📄 AI Resume Assistant

A full-stack AI-powered resume analysis web application built with **ASP.NET Core MVC** and **Groq AI API**.

Upload your resume (PDF) and get instant AI-powered feedback — summary improvements, cover letter generation, skill suggestions, and more.

![.NET](https://img.shields.io/badge/.NET-10-purple)
![C#](https://img.shields.io/badge/C%23-14-blue)
![License](https://img.shields.io/badge/License-MIT-green)

🌐 **Live Demo:** [https://ai-resume-assistant-l1qz.onrender.com](https://ai-resume-assistant-l1qz.onrender.com)

---

## ✨ Features

- **PDF Resume Upload** — Extract text from PDF using iText7
- **AI Chat Interface** — Real-time chat with AI for resume feedback
- **Multiple AI Personas** — Get advice from HR, Recruiter, Career Coach, or Resume Writer perspectives
- **Quick Actions** — One-click prompts for common tasks
- **Download Responses** — Save AI-generated cover letters and feedback
- **Session Management** — In-memory chat history with session-based storage
- **Night Mode** — Toggle dark/light theme with preference saved in localStorage
- **Responsive UI** — Modern Bootstrap 5 design with gradient theme

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| **Backend** | ASP.NET Core MVC (.NET 10), C# 14 |
| **AI Provider** | Groq API (Llama 3.3 70B) — Free tier |
| **PDF Parsing** | iText7 |
| **Frontend** | Razor Views, Bootstrap 5, jQuery/AJAX |
| **Architecture** | Controllers → Services → PromptBuilder → Models |

## 📁 Project Structure

```
AIResumeAssistant/
├── .github/
│   └── workflows/
│       └── deploy.yml              # GitHub Actions → Azure App Service
├── Controllers/
│   ├── HomeController.cs           # Landing page
│   └── ResumeController.cs         # Upload, AskAI, Download, Clear
├── Models/
│   ├── AskAiRequest.cs             # Chat request model
│   ├── AiResponse.cs               # JSON response model
│   ├── ChatMessage.cs              # Chat history entry
│   └── OpenAIResult.cs             # AI service result wrapper
├── Services/
│   ├── IPdfParserService.cs        # PDF extraction interface
│   ├── PdfParserService.cs         # iText7 implementation
│   ├── IOpenAIService.cs           # AI service interface
│   ├── GroqService.cs              # Groq API implementation
│   ├── GeminiService.cs            # Google Gemini implementation (alternative)
│   ├── IResumeSessionService.cs    # Session storage interface
│   └── ResumeSessionService.cs     # In-memory session store
├── PromptBuilder/
│   └── ResumePromptBuilder.cs      # Role-based prompt engineering
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml            # Landing page with hero section
│   │   └── Privacy.cshtml          # Privacy policy
│   ├── Resume/
│   │   └── Index.cshtml            # Chat-style AI assistant UI
│   └── Shared/
│       ├── _Layout.cshtml          # App layout with gradient navbar
│       └── _Layout.cshtml.css      # Scoped layout styles
├── wwwroot/
│   ├── css/site.css                # Custom styles + dark theme
│   ├── favicon.svg                 # App icon
│   └── js/
│       ├── site.js                 # Night mode toggle logic
│       └── resume.js               # AJAX chat & upload logic
├── appsettings.json                # App configuration & AI model settings
└── Program.cs                      # App entry point & service registration
```

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A free [Groq API key](https://console.groq.com/keys)

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/nanditha-satheesh/AI-Resume-Assistant.git
   cd AI-Resume-Assistant
   ```

2. **Store your API key securely**
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Groq:ApiKey" "gsk_YOUR_KEY_HERE"
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

4. **Open** `https://localhost:7122` in your browser

### Alternative: Google Gemini

To use Google Gemini instead of Groq, update `Program.cs`:
```csharp
// Change this line:
builder.Services.AddHttpClient<IOpenAIService, GroqService>();
// To:
builder.Services.AddHttpClient<IOpenAIService, GeminiService>();
```
And set your Gemini key:
```bash
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"
```

## 🚢 Deployment

The project includes a GitHub Actions workflow (`.github/workflows/deploy.yml`) that automatically builds and deploys to **Azure App Service** on every push to `main`.

To set it up:

1. Create an Azure Web App
2. Download the **Publish Profile** from the Azure portal
3. Add it as a GitHub repository secret named `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Update the `AZURE_WEBAPP_NAME` value in `deploy.yml` to match your app name
5. Set your API key in Azure → **Configuration → Application settings**:
   - Name: `Groq__ApiKey`, Value: your key

## 🏗️ Architecture Highlights

- **Dependency Injection** — All services registered via `IServiceCollection`
- **Interface-based design** — Easy to swap AI providers (Groq ↔ Gemini ↔ OpenAI)
- **Prompt Engineering** — Structured system + user prompts with persona-based roles
- **XSS Prevention** — All user input HTML-escaped before rendering
- **Secure secrets** — API keys stored in .NET User Secrets, never in source code

## 📝 License

This project is open source under the [MIT License](LICENSE).
