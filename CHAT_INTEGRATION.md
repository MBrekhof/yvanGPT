# AI Chat - Automatic Vector Store Integration

## 🎉 Your Chat Now Uses the Labware Knowledge Base!

The AI Chat has been enhanced to **automatically use your Vector Store** from [`vectorstore.config.json`](yvanGPT/vectorstore.config.json).

### Your Configuration

- **Vector Store ID**: `vs_694bc0dcb9c8819193781482c267c302`
- **Document**: Gebruikershandleiding-Labware-2025_v204.pdf
- **Status**: Active and ready to use!

## How to Use

1. **Navigate to AI Chat** - Click "AI Chat" in the menu
2. **Look for the badge** - You'll see a green badge: 📚 "Labware Knowledge Base Active"
3. **Ask questions** - Just chat normally about Labware!

### Example Questions

Try asking:
- "Hoe log ik in op Labware?"
- "How do I create a batch in Labware?"
- "Wat is de workflow voor monsters vrijgeven?"
- "Explain the customer service workflows"
- "How do I use the planning module?"

The AI will automatically reference the Labware user manual to answer your questions!

## How It Works

### Automatic Integration

The [`VectorStoreChatClient`](yvanGPT/Services/VectorStoreChatClient.cs) decorator wraps the standard OpenAI chat client:

1. **Reads Config**: Automatically reads [`vectorstore.config.json`](yvanGPT/vectorstore.config.json) on startup
2. **Adds Context**: Injects system instructions about the Labware manual into every conversation
3. **Seamless**: Users chat normally - the AI knows about Labware automatically!

### System Instructions Added

Every conversation automatically includes:

```
You are a helpful AI assistant with access to the Labware user manual documentation.

You have access to the Labware user manual (version 2025 v2.04) through Vector Store ID: vs_694bc0dcb9c8819193781482c267c302.

IMPORTANT INSTRUCTIONS:
1. When users ask questions about Labware, use the knowledge from the uploaded PDF document
2. Provide accurate, step-by-step instructions based on the manual
3. Reference specific sections or page numbers when possible
4. If you're not sure about something, say so clearly
5. For questions about Labware functionality, always check the documentation first
6. You can answer in Dutch or English, matching the user's language

The manual covers:
- Login procedures (Inloggen)
- User interface navigation
- Customer service workflows (Klantenservice)
- Sample management (Monsters)
- Planning and scheduling
- Laboratory operations (Biology and Chemistry)
- Quality control and validation
- And more...
```

### Visual Indicators

The chat page shows a status badge:

| Badge | Status | Meaning |
|-------|--------|---------|
| 📚 Green | "Labware Knowledge Base Active (X file(s))" | Ready! Ask away! |
| ⏳ Yellow | "Knowledge Base Processing..." | Files still being indexed |
| 💬 Yellow | "No Knowledge Base (Standard Chat Mode)" | No Vector Store configured |

## Technical Implementation

### 1. VectorStoreChatClient Decorator

[`VectorStoreChatClient.cs`](yvanGPT/Services/VectorStoreChatClient.cs) implements `IChatClient` and wraps the base OpenAI client:

```csharp
public class VectorStoreChatClient : IChatClient
{
    private readonly IChatClient _innerClient;
    private readonly KnowledgeBaseService _knowledgeBaseService;
    
    public async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Add Vector Store context to messages
        var messagesWithContext = await AddVectorStoreContextAsync(chatMessages, cancellationToken);
        
        // Call the base client
        return await _innerClient.CompleteAsync(messagesWithContext, options, cancellationToken);
    }
}
```

### 2. Service Registration

In [`Program.cs`](yvanGPT/Program.cs), the decorator is registered:

```csharp
// Register Vector Store services first
builder.Services.AddHttpClient<VectorStoreService>();
builder.Services.AddScoped<KnowledgeBaseService>();

// Create base OpenAI chat client
var openAiClient = new OpenAIClient(openAiServiceSettings.ApiKey);
var baseChatClient = openAiClient.GetChatClient(openAiServiceSettings.Model).AsIChatClient();

// Wrap with Vector Store decorator
builder.Services.AddScoped<IChatClient>((provider) =>
{
    var knowledgeBaseService = provider.GetRequiredService<KnowledgeBaseService>();
    var logger = provider.GetRequiredService<ILogger<VectorStoreChatClient>>();
    return new VectorStoreChatClient(baseChatClient, knowledgeBaseService, logger);
});
```

### 3. Chat Page Enhancement

[`AIChat.razor`](yvanGPT/Components/Pages/AIChat.razor) checks the Vector Store status and displays it:

```csharp
protected override async Task OnInitializedAsync()
{
    await CheckVectorStoreStatus();
}

private async Task CheckVectorStoreStatus()
{
    var vectorStoreId = await KnowledgeBaseService.GetCurrentVectorStoreIdAsync();
    
    if (!string.IsNullOrEmpty(vectorStoreId))
    {
        var info = await KnowledgeBaseService.GetKnowledgeBaseInfoAsync();
        
        if (info != null && info.CompletedFiles > 0)
        {
            vectorStoreStatus = $"Labware Knowledge Base Active ({info.CompletedFiles} file(s))";
            vectorStoreClass = "enabled";
            vectorStoreIcon = "📚";
        }
    }
}
```

## Benefits

✅ **Zero Configuration** - Works automatically if vectorstore.config.json exists  
✅ **Seamless UX** - Users don't need to know about Vector Stores  
✅ **Context Aware** - AI knows about Labware without being told  
✅ **Bilingual** - Answers in Dutch or English based on the question  
✅ **Accurate** - References the actual user manual  
✅ **Persistent** - Vector Store ID saved across sessions  

## Troubleshooting

### Badge Shows "No Knowledge Base"

- Check if [`vectorstore.config.json`](yvanGPT/vectorstore.config.json) exists in the project root
- Verify the Vector Store ID is valid
- Go to "Knowledge Base" page to initialize

### Badge Shows "Processing..."

- Files are still being indexed by OpenAI
- Wait a few minutes and refresh the page
- Check the "Knowledge Base" page for detailed status

### AI Doesn't Reference the Manual

- Ensure the badge shows "Active"
- Try asking more specific questions about Labware
- Check the console logs for any errors

## Related Files

- [`VectorStoreChatClient.cs`](yvanGPT/Services/VectorStoreChatClient.cs) - Chat client decorator
- [`KnowledgeBaseService.cs`](yvanGPT/Services/KnowledgeBaseService.cs) - Knowledge base management
- [`AIChat.razor`](yvanGPT/Components/Pages/AIChat.razor) - Chat UI with status badge
- [`Program.cs`](yvanGPT/Program.cs) - Service registration
- [`vectorstore.config.json`](yvanGPT/vectorstore.config.json) - Your Vector Store configuration

## Next Steps

- Try asking questions about Labware in the chat!
- Check the "Vector Store Demo" page to see the configuration details
- Visit the "Knowledge Base" page to manage your Vector Store

---

**Created**: 2025-12-24  
**Your Vector Store**: `vs_694bc0dcb9c8819193781482c267c302`
