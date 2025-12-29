# OpenAI Vector Store Knowledge Base Setup

This document explains how to use OpenAI Vector Stores to create a persistent knowledge base from your PDF documents that can be used across multiple sessions.

## Overview

OpenAI Vector Stores provide a way to store and search through documents using semantic search. This implementation allows you to:

1. Upload PDF documents (like the Labware user manual) to OpenAI
2. Store them in a Vector Store for persistent access
3. Use the Vector Store ID across sessions as a knowledge base
4. Query the documents using natural language
5. **Automatically integrate with AI Chat** - The chat now uses your Vector Store by default!

## 🎉 Quick Start

Your Vector Store is **already configured** and will be **automatically used** in the AI Chat!

1. Your Vector Store ID: `vs_694bc0dcb9c8819193781482c267c302`
2. Go to the **AI Chat** page
3. Ask questions about Labware - the AI will reference the user manual automatically!

Example questions:
- "Hoe log ik in op Labware?"
- "How do I create a batch?"
- "Wat is de workflow voor monsters vrijgeven?"

## Architecture

### Components Created

1. **[`VectorStoreService.cs`](yvanGPT/Services/VectorStoreService.cs)** - Low-level service for OpenAI Vector Store API operations
   - Create, list, retrieve, and delete vector stores
   - Upload files to OpenAI
   - Add/remove files from vector stores
   - List files in vector stores

2. **[`KnowledgeBaseService.cs`](yvanGPT/Services/KnowledgeBaseService.cs)** - High-level service for knowledge base management
   - Initialize knowledge base with PDF
   - Save/load vector store configuration
   - Get knowledge base information
   - Add additional files to existing knowledge base

3. **[`KnowledgeBase.razor`](yvanGPT/Components/Pages/KnowledgeBase.razor)** - Blazor page for UI management
   - Initialize new knowledge base
   - View knowledge base status and files
   - Refresh information
   - Delete knowledge base

4. **[`vectorstore.config.json`](yvanGPT/vectorstore.config.json.example)** - Configuration file (auto-generated)
   - Stores the Vector Store ID for persistence
   - Tracks creation and update timestamps

## Setup Instructions

### 1. Prerequisites

- OpenAI API key configured in [`appsettings.json`](yvanGPT/appsettings.json)
- PDF file in the project root: `Gebruikershandleiding-Labware-2025_v204.pdf`

### 2. Configuration

Ensure your [`appsettings.json`](yvanGPT/appsettings.json) has the OpenAI configuration:

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here"
  },
  "OpenAISettings": {
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-4o"
  }
}
```

### 3. Initialize Knowledge Base

1. Run the application
2. Navigate to the "Knowledge Base" page from the menu
3. Click "Initialize Knowledge Base"
4. The system will:
   - Create a new Vector Store
   - Upload the PDF file
   - Add the file to the Vector Store
   - Save the Vector Store ID to [`vectorstore.config.json`](yvanGPT/vectorstore.config.json.example)

### 4. Using the Vector Store in Your Application

Once initialized, you can use the Vector Store ID in your AI chat or assistant implementations:

```csharp
// Get the current vector store ID
var knowledgeBaseService = serviceProvider.GetRequiredService<KnowledgeBaseService>();
var vectorStoreId = await knowledgeBaseService.GetCurrentVectorStoreIdAsync();

// Use with OpenAI Assistants API
var assistant = await client.CreateAssistantAsync(new AssistantCreateRequest
{
    Model = "gpt-4o",
    Tools = new[] { new ToolDefinition { Type = "file_search" } },
    ToolResources = new ToolResources
    {
        FileSearch = new FileSearchResources
        {
            VectorStoreIds = new[] { vectorStoreId }
        }
    }
});
```

## How Vector Stores Work

### File Processing

1. **Upload**: PDF is uploaded to OpenAI's file storage
2. **Indexing**: OpenAI automatically chunks and embeds the document
3. **Storage**: Embeddings are stored in the Vector Store
4. **Search**: Semantic search is performed using the embeddings

### Status Tracking

Files in a Vector Store can have the following statuses:
- `in_progress` - File is being processed
- `completed` - File is ready for search
- `failed` - Processing failed
- `cancelled` - Processing was cancelled

### Persistence

The Vector Store ID is saved to [`vectorstore.config.json`](yvanGPT/vectorstore.config.json.example):

```json
{
  "VectorStoreId": "vs_xxxxxxxxxxxxxxxxxxxxx",
  "Name": "Labware Gebruikershandleiding Knowledge Base",
  "CreatedAt": "2025-12-24T10:00:00.000Z",
  "LastUpdated": "2025-12-24T10:00:00.000Z"
}
```

This file is automatically created and updated by the [`KnowledgeBaseService`](yvanGPT/Services/KnowledgeBaseService.cs). It's excluded from git via [`.gitignore`](.gitignore) to keep your Vector Store IDs private.

## API Reference

### VectorStoreService Methods

- `CreateVectorStoreAsync(name)` - Create a new vector store
- `ListVectorStoresAsync()` - List all vector stores
- `GetVectorStoreAsync(vectorStoreId)` - Get vector store details
- `DeleteVectorStoreAsync(vectorStoreId)` - Delete a vector store
- `UploadFileAsync(filePath)` - Upload a file to OpenAI
- `AddFileToVectorStoreAsync(vectorStoreId, fileId)` - Add file to vector store
- `ListVectorStoreFilesAsync(vectorStoreId)` - List files in vector store
- `RemoveFileFromVectorStoreAsync(vectorStoreId, fileId)` - Remove file from vector store

### KnowledgeBaseService Methods

- `GetCurrentVectorStoreIdAsync()` - Get the saved vector store ID
- `SaveVectorStoreIdAsync(vectorStoreId, name)` - Save vector store configuration
- `InitializeKnowledgeBaseAsync(pdfFilePath)` - Initialize with PDF
- `AddFileToKnowledgeBaseAsync(pdfFilePath)` - Add additional file
- `GetKnowledgeBaseInfoAsync()` - Get detailed information
- `DeleteKnowledgeBaseAsync()` - Delete knowledge base
- `IsKnowledgeBaseInitializedAsync()` - Check if initialized

## Using with Assistants API

To use the Vector Store with OpenAI's Assistants API for chat:

```csharp
// 1. Get the vector store ID
var vectorStoreId = await knowledgeBaseService.GetCurrentVectorStoreIdAsync();

// 2. Create an assistant with file_search tool
var assistant = await openAiClient.CreateAssistantAsync(new
{
    model = "gpt-4o",
    tools = new[] { new { type = "file_search" } },
    tool_resources = new
    {
        file_search = new
        {
            vector_store_ids = new[] { vectorStoreId }
        }
    }
});

// 3. Create a thread and add messages
var thread = await openAiClient.CreateThreadAsync();

await openAiClient.CreateMessageAsync(thread.Id, new
{
    role = "user",
    content = "Hoe log ik in op Labware?"
});

// 4. Run the assistant
var run = await openAiClient.CreateRunAsync(thread.Id, new
{
    assistant_id = assistant.Id
});

// 5. Wait for completion and get response
// The assistant will automatically search the vector store
```

## Cost Considerations

- **Storage**: Vector stores are charged based on storage size (~$0.10/GB/day)
- **File Processing**: One-time charge when files are added
- **Expiration**: Vector stores can be configured to expire after inactivity (default: 365 days)

## Troubleshooting

### File Not Found Error
Ensure `Gebruikershandleiding-Labware-2025_v204.pdf` is in the project root directory.

### API Key Issues
Verify your OpenAI API key is correctly configured in [`appsettings.json`](yvanGPT/appsettings.json).

### Processing Status
Files may take a few minutes to process. Check the status on the Knowledge Base page.

### Vector Store Not Found
If the vector store was deleted externally, delete [`vectorstore.config.json`](yvanGPT/vectorstore.config.json.example) and reinitialize.

## Security Notes

- The [`vectorstore.config.json`](yvanGPT/vectorstore.config.json.example) file is excluded from git
- Vector Store IDs are not sensitive but should be kept private
- API keys should never be committed to source control
- Use environment variables or Azure Key Vault for production

## Next Steps

1. **Integrate with Chat**: Modify [`AIChat.razor`](yvanGPT/Components/Pages/AIChat.razor) to use the Vector Store
2. **Add More Documents**: Use the [`KnowledgeBaseService`](yvanGPT/Services/KnowledgeBaseService.cs) to add additional PDFs
3. **Implement Search**: Create a search interface for direct document queries
4. **Monitor Usage**: Track Vector Store usage and costs in OpenAI dashboard

## References

- [OpenAI Vector Stores Documentation](https://platform.openai.com/docs/api-reference/vector-stores)
- [OpenAI Assistants API](https://platform.openai.com/docs/assistants/overview)
- [File Search Tool](https://platform.openai.com/docs/assistants/tools/file-search)

## Example: Complete Integration

Here's a complete example of using the Vector Store in a chat scenario:

```csharp
@page "/labware-chat"
@inject KnowledgeBaseService KnowledgeBaseService
@inject OpenAIClient OpenAIClient

<h3>Labware Assistant</h3>

<div class="chat-container">
    @foreach (var message in messages)
    {
        <div class="message @message.Role">
            <strong>@message.Role:</strong> @message.Content
        </div>
    }
</div>

<input @bind="userInput" placeholder="Ask about Labware..." />
<button @onclick="SendMessage">Send</button>

@code {
    private string userInput = "";
    private List<ChatMessage> messages = new();
    private string? assistantId;
    private string? threadId;

    protected override async Task OnInitializedAsync()
    {
        // Get vector store ID
        var vectorStoreId = await KnowledgeBaseService.GetCurrentVectorStoreIdAsync();
        
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            messages.Add(new ChatMessage 
            { 
                Role = "system", 
                Content = "Please initialize the knowledge base first." 
            });
            return;
        }

        // Create assistant with vector store
        // Implementation depends on your OpenAI client setup
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrEmpty(userInput)) return;

        messages.Add(new ChatMessage { Role = "user", Content = userInput });
        
        // Send to OpenAI and get response
        // Add response to messages
        
        userInput = "";
        StateHasChanged();
    }

    private class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
```

---

**Created**: 2025-12-24  
**Version**: 1.0  
**Author**: AI Assistant
