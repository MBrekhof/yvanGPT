# yvanGPT

A .NET 8 Blazor Server application showcasing DevExpress Blazor UI components integrated with Azure OpenAI for AI-powered features.

## Features

- **AI Chat Interface**: Interactive chat powered by Azure OpenAI with file upload support (JPG, PDF)
- **DevExpress UI Components**: Modern, responsive UI built with DevExpress Blazor v25.2
- **Real-time Interaction**: Server-side rendering with interactive components
- **Smart Navigation**: Persistent drawer state management through URL parameters
- **Fluent Design**: Clean UI with Fluent Light theme

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Azure OpenAI account (or use demo credentials with rate limits)
- Visual Studio 2022 or VS Code (recommended)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/MBrekhof/yvanGPT.git
cd yvanGPT
```

### 2. Configure Azure OpenAI

Edit `yvanGPT/appsettings.json` and add your Azure OpenAI credentials:

```json
"AzureOpenAISettings": {
  "Endpoint": "https://your-endpoint.openai.azure.com",
  "Key": "your-api-key",
  "DeploymentName": "your-deployment-name"
}
```

**Note**: The default configuration includes DevExpress demo credentials (rate-limited). For production use, provide your own Azure OpenAI credentials.

### 3. Build and Run

```bash
# Build the project
dotnet build DXApplication1/DXApplication1.csproj

# Run the application
dotnet run --project DXApplication1/DXApplication1.csproj
```

The application will be available at:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

## Project Structure

```
yvanGPT/
├── Components/
│   ├── Layout/          # Main layout and navigation components
│   ├── Pages/           # Page components (Home, AI Chat, etc.)
│   └── Shared/          # Shared utilities and base classes
├── Services/            # Azure OpenAI configuration
├── wwwroot/            # Static assets (CSS, images, icons)
└── Program.cs          # Application entry point and DI configuration
```

## Key Technologies

- **.NET 8**: Latest .NET framework
- **Blazor Server**: Interactive server-side rendering
- **DevExpress Blazor v25.2**: Professional UI component library
- **Azure OpenAI**: GPT-powered AI integration
- **Microsoft.Extensions.AI**: AI abstraction layer

## DevExpress Components

This project demonstrates several DevExpress Blazor components:

- **DxAIChat**: AI chat interface with streaming and file uploads
- **DxResourceManager**: Theme and script management
- **DxButton**: Styled buttons with various render styles
- **Drawer**: Responsive navigation sidebar

## Development

### Architecture Highlights

- **Dependency Injection**: Configured in `Program.cs` with scoped `IChatClient`
- **Drawer State Management**: Custom base classes preserve sidebar state in URL
- **DevExpress AI Integration**: Seamless integration via `AddDevExpressAI()`

### Adding New Pages

1. Create a new `.razor` file in `Components/Pages/`
2. Inherit from `DrawerStateComponentBase` for drawer state support
3. Add navigation link in `Components/Layout/NavMenu.razor`

## Documentation

For detailed DevExpress component documentation, visit:
- [DevExpress Blazor Documentation](https://docs.devexpress.com/Blazor/400725/blazor-components)
- [DevExpress AI Integration](https://docs.devexpress.com/AIIntegration/)

## License

This project uses DevExpress components. Please ensure you have appropriate DevExpress licenses for production use.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions:
- Open an issue in this repository
- Check [DevExpress Support Center](https://supportcenter.devexpress.com/)
