# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 8 Blazor Server application showcasing DevExpress Blazor UI components integrated with Azure OpenAI for AI-powered features. The application uses DevExpress v25.2 components with interactive server-side rendering.

## DevExpress Documentation Access

This project is configured with the `dxdocs` MCP server for accessing DevExpress documentation. For ANY question about DevExpress components:

1. Call `devexpress_docs_search` to find relevant help topics
2. Call `devexpress_docs_get_content` to fetch documentation
3. Answer based solely on retrieved information
4. Include code examples from documentation when available
5. Use version-specific tools (e.g., `dxdocs25_1`) when applicable

## Build and Run Commands

Build the project:
```bash
dotnet build yvanGpt.csproj
```

Run the application:
```bash
dotnet run --project yvanGpt.csproj
```

The application runs on:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

## Azure OpenAI Configuration

The application requires Azure OpenAI credentials configured in `appsettings.json`:

```json
"AzureOpenAISettings": {
  "Endpoint": "https://your-endpoint.openai.azure.com",
  "Key": "your-api-key",
  "DeploymentName": "your-deployment-name"
}
```

Default configuration uses DevExpress demo credentials (rate-limited, for demonstration only). Production use requires your own Azure OpenAI credentials.

## Architecture

### Dependency Injection Setup (Program.cs)

The application uses a layered DI configuration:

1. **Blazor Server Components**: Interactive server-side rendering with `AddInteractiveServerComponents()`
2. **DevExpress Blazor**: Configured with Medium size mode
3. **Azure OpenAI Integration**:
   - Settings loaded from configuration (`AzureOpenAIServiceSettings`)
   - `AzureOpenAIClient` registered as scoped `IChatClient` using Microsoft.Extensions.AI
   - DevExpress AI integration enabled via `AddDevExpressAI()`

### Drawer State Management

The application uses a custom drawer (sidebar) state management system that persists sidebar state in the URL:

- **Base Classes**: `DrawerStateComponentBase` and `DrawerStateLayoutComponentBase` in `Components/Shared/`
- **Query Parameter**: `toggledSidebar` tracks whether drawer is open/closed
- **Helper Methods**:
  - `AddDrawerStateToUrl()`: Preserves current drawer state in navigation
  - `AddDrawerStateToUrlToggled()`: Toggles drawer state in navigation
  - `RemoveDrawerStateFromUrl()`: Clears drawer state from URL

Pages and layouts should inherit from these base classes when drawer state needs to be preserved during navigation.

### Component Structure

```
Components/
├── App.razor                    # Root component with theme configuration (Fluent Light)
├── Routes.razor                 # Routing configuration
├── _Imports.razor              # Global using statements
├── Layout/
│   ├── MainLayout.razor        # Main layout with drawer navigation
│   ├── NavMenu.razor           # Navigation menu
│   └── Drawer.razor            # Sidebar drawer component
├── Pages/
│   ├── Index/                  # Home page components
│   ├── AIChat.razor            # DxAIChat component demo with file upload
│   └── Error.razor             # Error page
└── Shared/
    └── DrawerStateComponentBase.cs  # Base classes for drawer state management
```

### Key DevExpress Components Used

- **DxAIChat**: AI chat interface with streaming support and file uploads (`.jpg`, `.pdf`)
- **DxResourceManager**: Theme and script registration
- **DxButton**: Buttons with Light/Secondary render styles
- **Drawer**: Navigation drawer/sidebar component

### Static Assets

- Custom CSS in `wwwroot/css/` (icons.css, site.css)
- SVG icons in `wwwroot/images/`
- Scoped CSS via `yvanGpt.styles.css`

## Key NuGet Packages

- **DevExpress.Blazor** (v25.2): UI component library
- **DevExpress.AIIntegration.Blazor** (v25.2): AI component integration
- **DevExpress.AIIntegration.OpenAI** (v25.2): OpenAI provider for DevExpress AI
- **Azure.AI.OpenAI** (v2.2.0-beta.5): Azure OpenAI client
- **Microsoft.Extensions.AI** (v9.7.1): AI abstraction layer
- **Microsoft.Extensions.AI.OpenAI**: OpenAI implementation for Microsoft.Extensions.AI

## Development Notes

- The project uses .NET 8 with nullable reference types enabled
- Implicit usings are enabled
- Theme is configured in `App.razor` (Fluent Light mode)
- All Razor components use interactive server render mode
- Navigation preserves drawer state through URL query parameters
