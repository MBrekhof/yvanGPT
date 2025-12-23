# RAG Document Upload and Retrieval Implementation Plan

## Overview
Add Retrieval Augmented Generation (RAG) capabilities to the DxAIChat component, allowing users to upload documents, store them in SQLite with vector embeddings, and query them through the chat interface.

## Architecture

### High-Level Flow
```
User uploads document → Parse & chunk → Generate embeddings (OpenAI) → Store in SQLite
User asks question → Generate query embedding → Search similar chunks → Inject context → Chat response
```

### Key Components
1. **Document Storage Service**: Handle file uploads, parsing, and storage
2. **Embedding Service**: Generate embeddings using OpenAI API
3. **RAG Service**: Retrieve relevant chunks and inject context into chat
4. **SQLite Database**: Store documents, chunks, and vector embeddings
5. **Chat Integration**: Hook into DxAIChat to add RAG context

## Database Schema

### SQLite with sqlite-vec Extension

**Documents Table:**
```sql
CREATE TABLE Documents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName TEXT NOT NULL,
    FileType TEXT NOT NULL,
    UploadedAt DATETIME NOT NULL,
    FileSize INTEGER NOT NULL,
    ContentHash TEXT NOT NULL
);
```

**Chunks Table:**
```sql
CREATE TABLE Chunks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DocumentId INTEGER NOT NULL,
    ChunkIndex INTEGER NOT NULL,
    Content TEXT NOT NULL,
    TokenCount INTEGER NOT NULL,
    FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
);
```

**Embeddings Table:**
```sql
CREATE TABLE Embeddings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChunkId INTEGER NOT NULL,
    Embedding BLOB NOT NULL,  -- Store as float array
    UNIQUE (ChunkId),
    FOREIGN KEY (ChunkId) REFERENCES Chunks(Id) ON DELETE CASCADE
);
```

## New Files to Create

### 1. Models/Document.cs
```csharp
public class Document
{
    public int Id { get; set; }
    public string FileName { get; set; }
    public string FileType { get; set; }
    public DateTime UploadedAt { get; set; }
    public int FileSize { get; set; }
    public string ContentHash { get; set; }
    public ICollection<Chunk> Chunks { get; set; }
}
```

### 2. Models/Chunk.cs
```csharp
public class Chunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; }
    public int TokenCount { get; set; }
    public Document Document { get; set; }
    public Embedding Embedding { get; set; }
}
```

### 3. Models/Embedding.cs
```csharp
public class Embedding
{
    public int Id { get; set; }
    public int ChunkId { get; set; }
    public byte[] EmbeddingVector { get; set; }
    public Chunk Chunk { get; set; }
}
```

### 4. Data/DocumentDbContext.cs
```csharp
using Microsoft.EntityFrameworkCore;

public class DocumentDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; }
    public DbSet<Chunk> Chunks { get; set; }
    public DbSet<Embedding> Embeddings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=documents.db");
    }
}
```

### 5. Services/DocumentStorageSettings.cs
```csharp
public class DocumentStorageSettings
{
    public string DatabasePath { get; set; } = "documents.db";
    public int MaxFileSizeMB { get; set; } = 10;
    public List<string> AllowedExtensions { get; set; } = new() { ".pdf", ".txt", ".md", ".docx" };
}
```

### 6. Services/OpenAIEmbeddingSettings.cs
```csharp
public class OpenAIEmbeddingSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 1536;
}
```

### 7. Services/RAGSettings.cs
```csharp
public class RAGSettings
{
    public int ChunkSize { get; set; } = 1000; // tokens
    public int ChunkOverlap { get; set; } = 200; // tokens
    public int TopK { get; set; } = 3; // number of chunks to retrieve
}
```

### 8. Services/IDocumentService.cs
```csharp
public interface IDocumentService
{
    Task<Document> UploadDocumentAsync(Stream fileStream, string fileName, string fileType);
    Task<List<Chunk>> GetChunksAsync(int documentId);
    Task DeleteDocumentAsync(int documentId);
    Task<List<Document>> GetAllDocumentsAsync();
}
```

### 9. Services/DocumentService.cs
Implementation of IDocumentService:
- Parse files (use libraries: PdfPig for PDF, DocumentFormat.OpenXml for DOCX)
- Chunk text using TokenCountHelper
- Store in database via EF Core

### 10. Services/IEmbeddingService.cs
```csharp
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts);
}
```

### 11. Services/OpenAIEmbeddingService.cs
Implementation using OpenAI API:
- Use OpenAI .NET SDK
- Call text-embedding-3-small endpoint
- Return 1536-dimensional vectors

### 12. Services/IRAGService.cs
```csharp
public interface IRAGService
{
    Task<string> GetRelevantContextAsync(string query);
    Task ProcessDocumentAsync(int documentId);
}
```

### 13. Services/RAGService.cs
Implementation:
- Generate embeddings for chunks
- Store embeddings in database
- Perform cosine similarity search
- Return concatenated relevant chunks

### 14. Services/VectorSearchService.cs
Helper service for cosine similarity:
```csharp
public class VectorSearchService
{
    public double CosineSimilarity(float[] a, float[] b);
    public List<(int ChunkId, double Score)> FindTopK(float[] query, List<(int, float[])> embeddings, int k);
}
```

### 15. Helpers/TokenCountHelper.cs
Text chunking utility:
- Use SharpToken or Microsoft.DeepDev.TokenizerLib
- Split text into overlapping chunks
- Return list of chunked strings

### 16. Services/RAGChatClient.cs
**Critical Component:** Custom IChatClient wrapper that injects RAG context

```csharp
using Microsoft.Extensions.AI;

public class RAGChatClient : IChatClient
{
    private readonly IChatClient _innerClient;
    private readonly IRAGService _ragService;

    public RAGChatClient(IChatClient innerClient, IRAGService ragService)
    {
        _innerClient = innerClient;
        _ragService = ragService;
    }

    public async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Intercept the request
        var userMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (userMessage != null)
        {
            // Get relevant context from RAG
            var context = await _ragService.GetRelevantContextAsync(userMessage.Text);

            if (!string.IsNullOrEmpty(context))
            {
                // Inject context into the conversation
                var systemMessage = new ChatMessage(ChatRole.System,
                    $"Use the following context from uploaded documents to answer the user's question:\n\n{context}");

                var modifiedMessages = new List<ChatMessage> { systemMessage };
                modifiedMessages.AddRange(chatMessages);
                chatMessages = modifiedMessages;
            }
        }

        // Pass to underlying Azure OpenAI client
        return await _innerClient.CompleteAsync(chatMessages, options, cancellationToken);
    }

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Same context injection logic for streaming
        var userMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (userMessage != null)
        {
            var context = await _ragService.GetRelevantContextAsync(userMessage.Text);

            if (!string.IsNullOrEmpty(context))
            {
                var systemMessage = new ChatMessage(ChatRole.System,
                    $"Use the following context from uploaded documents to answer the user's question:\n\n{context}");

                var modifiedMessages = new List<ChatMessage> { systemMessage };
                modifiedMessages.AddRange(chatMessages);
                chatMessages = modifiedMessages;
            }
        }

        // Stream from underlying client
        await foreach (var update in _innerClient.CompleteStreamingAsync(chatMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    // Implement other IChatClient members by delegating to _innerClient
    public ChatClientMetadata Metadata => _innerClient.Metadata;
    public TService? GetService<TService>(object? key = null) where TService : class
        => _innerClient.GetService<TService>(key);
    public void Dispose() => _innerClient.Dispose();
}
```

## Files to Modify

### Program.cs (DXApplication1/Program.cs)
**Location:** Replace the existing IChatClient registration (lines 25-31) with the RAG-enabled version

**BEFORE:**
```csharp
var chatClient = new AzureOpenAIClient(
     new Uri(openAiServiceSettings.Endpoint),
     new AzureKeyCredential(openAiServiceSettings.Key))
    .GetChatClient(openAiServiceSettings.DeploymentName)
    .AsIChatClient();

builder.Services.AddScoped<IChatClient>((provider) => chatClient);
```

**AFTER:**
```csharp
// Create base Azure OpenAI client (but don't register yet)
var azureOpenAIClient = new AzureOpenAIClient(
     new Uri(openAiServiceSettings.Endpoint),
     new AzureKeyCredential(openAiServiceSettings.Key))
    .GetChatClient(openAiServiceSettings.DeploymentName)
    .AsIChatClient();

// Load RAG configuration
var documentSettings = builder.Configuration.GetSection("DocumentStorageSettings").Get<DocumentStorageSettings>();
var embeddingSettings = builder.Configuration.GetSection("OpenAIEmbeddingSettings").Get<OpenAIEmbeddingSettings>();
var ragSettings = builder.Configuration.GetSection("RAGSettings").Get<RAGSettings>();

if (embeddingSettings == null || string.IsNullOrEmpty(embeddingSettings.ApiKey))
    throw new InvalidOperationException("Specify OpenAI API key in appsettings.json");

builder.Services.AddSingleton(documentSettings ?? new DocumentStorageSettings());
builder.Services.AddSingleton(embeddingSettings);
builder.Services.AddSingleton(ragSettings ?? new RAGSettings());

// Register DbContext
builder.Services.AddDbContext<DocumentDbContext>();

// Register RAG services
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
builder.Services.AddScoped<IRAGService, RAGService>();
builder.Services.AddSingleton<VectorSearchService>();

// Add HttpClient for OpenAI API
builder.Services.AddHttpClient<IEmbeddingService, OpenAIEmbeddingService>();

// Register RAGChatClient as the IChatClient (wraps Azure OpenAI with RAG)
builder.Services.AddScoped<IChatClient>((provider) =>
{
    var ragService = provider.GetRequiredService<IRAGService>();
    return new RAGChatClient(azureOpenAIClient, ragService);
});
```

### appsettings.json (DXApplication1/appsettings.json)
Add new configuration sections:
```json
{
  "DocumentStorageSettings": {
    "DatabasePath": "documents.db",
    "MaxFileSizeMB": 10,
    "AllowedExtensions": [".pdf", ".txt", ".md", ".docx"]
  },
  "OpenAIEmbeddingSettings": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "text-embedding-3-small",
    "Dimensions": 1536
  },
  "RAGSettings": {
    "ChunkSize": 1000,
    "ChunkOverlap": 200,
    "TopK": 3
  }
}
```

### AIChat.razor (Components/Pages/AIChat.razor)
**Changes needed:**

1. **Add MessageSent event handler for file uploads:**
```csharp
@inject IDocumentService DocumentService
@inject IRAGService RAGService

async Task OnMessageSent(MessageSentEventArgs args)
{
    // Process any uploaded files
    if (args.Files != null && args.Files.Any())
    {
        foreach (var file in args.Files)
        {
            var document = await DocumentService.UploadDocumentAsync(
                file.FileContent,
                file.FileName,
                Path.GetExtension(file.FileName)
            );
            await RAGService.ProcessDocumentAsync(document.Id);
        }
    }
}
```

2. **Update DxAIChat configuration:**
```razor
<DxAIChat AllowResizeInput="true"
          FileUploadEnabled="true"
          MessageSent="OnMessageSent"
          Initialized="ChatInitialized"
          UseStreaming="true"
          CssClass="h-100 overflow-hidden">
```

3. **Update file upload settings:**
```razor
<DxAIChatFileUploadSettings MaxFileCount="5"
                            MaxFileSize="10485760"
                            AllowedFileExtensions="@(new List<string> { ".pdf", ".txt", ".md", ".docx" })" />
```

**Note:** RAG context injection happens in the custom IChatClient wrapper (see RAGChatClient below), not in the component.

## NuGet Packages to Install

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package OpenAI --version 2.1.0
dotnet add package PdfPig --version 0.1.9
dotnet add package DocumentFormat.OpenXml --version 3.2.0
dotnet add package SharpToken --version 2.0.3
```

## Implementation Steps (In Order)

### Phase 1: Database Foundation
1. Create Models folder with Document, Chunk, Embedding models
2. Create Data folder with DocumentDbContext
3. Install EF Core SQLite packages
4. Create initial migration and database

### Phase 2: Configuration
5. Create Settings classes in Services folder
6. Update appsettings.json with new configuration sections
7. Register settings in Program.cs

### Phase 3: Core Services
8. Create TokenCountHelper for text chunking
9. Create VectorSearchService for cosine similarity
10. Implement OpenAIEmbeddingService with OpenAI API integration
11. Implement DocumentService for file parsing and storage
12. Implement RAGService for context retrieval

### Phase 4: Chat Integration
13. Modify AIChat.razor to add file upload handler
14. Add message interceptor for RAG context injection
15. Update file upload settings (size, types)

### Phase 5: Testing & Refinement
16. Test document upload flow
17. Test embedding generation
18. Test retrieval and context injection
19. Optimize chunk size and retrieval parameters

## Integration Points

### How RAG hooks into IChatClient flow:

**Document Upload Flow:**
```
1. User uploads document with message → DxAIChat component
2. MessageSent event fires → AIChat.OnMessageSent handler
3. Document saved & parsed → DocumentService.UploadDocumentAsync()
4. Text chunked → TokenCountHelper
5. Chunks embedded → OpenAIEmbeddingService.GenerateBatchEmbeddingsAsync()
6. Stored in SQLite → DocumentDbContext.SaveChanges()
```

**Chat Query Flow (with RAG):**
```
1. User sends message → DxAIChat component
2. Message routed to IChatClient → RAGChatClient (our wrapper)
3. Query embedded → OpenAIEmbeddingService.GenerateEmbeddingAsync()
4. Search similar chunks → VectorSearchService.FindTopK()
5. Retrieve context → RAGService.GetRelevantContextAsync()
6. Inject system message with context → RAGChatClient.CompleteStreamingAsync()
7. Forward to Azure OpenAI → azureOpenAIClient.CompleteStreamingAsync()
8. Stream response back → DxAIChat component
9. Display to user → Real-time token streaming
```

**Key Insight:** The RAGChatClient acts as a transparent middleware layer. DxAIChat and DevExpress AI integration are unaware of the RAG logic - they just see a standard IChatClient.

## Key Design Decisions

1. **SQLite with manual vector search**: Use EF Core with SQLite, implement cosine similarity in C#. Alternative: Use sqlite-vec extension (requires native binaries).

2. **OpenAI API (not Azure)**: Direct OpenAI SDK for embeddings. Separate from Azure OpenAI used for chat.

3. **Scoped services**: Follow existing pattern (IChatClient is scoped). All RAG services registered as scoped.

4. **File size limit**: Increased from 20KB to 10MB to accommodate real documents.

5. **Supported file types**: PDF, TXT, MD, DOCX (most common document types).

6. **Chunking**: 1000 tokens with 200 overlap (balanced context preservation).

7. **Retrieval**: Top 3 chunks (focused context, manageable token count).

## Critical Files to Modify

- `DXApplication1/Program.cs` - Service registration
- `DXApplication1/appsettings.json` - Configuration
- `DXApplication1/Components/Pages/AIChat.razor` - UI integration

## Notes and References

### DevExpress Documentation Sources:
- [DxAIChat Class Reference](https://docs.devexpress.com/Blazor/DevExpress.AIIntegration.Blazor.Chat.DxAIChat)
- [DxAIChat MessageSent Event](https://docs.devexpress.com/Blazor/DevExpress.AIIntegration.Blazor.Chat.DxAIChat.MessageSent)
- [DxAIChatFileUploadSettings](https://docs.devexpress.com/Blazor/DevExpress.AIIntegration.Blazor.Chat.DxAIChatFileUploadSettings)
- [DevExpress AI Integration Guide](https://docs.devexpress.com/Blazor/405228/ai-powered-extensions)
- [AI Chat Component Guide](https://docs.devexpress.com/Blazor/405290/components/ai-chat)
- [GitHub Examples](https://github.com/DevExpress-Examples/devexpress-ai-chat-samples)

### Implementation Notes:
- DxAIChat uses **MessageSent** event (fires after user sends message), not "OnMessageSending"
- File uploads accessible via `args.Files` in MessageSent handler
- RAG context injection via custom `IChatClient` wrapper (RAGChatClient)
- Consider adding UI to show uploaded documents and manage them
- Consider adding loading indicators during document processing
- May need to handle large documents asynchronously with background processing
- Test with various document sizes and types to optimize chunk size
- Monitor OpenAI API costs (embeddings + chat completions)
