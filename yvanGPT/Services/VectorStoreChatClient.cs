using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace yvanGPT.Services;

/// <summary>
/// Chat client decorator that adds Vector Store context to conversations
/// </summary>
public class VectorStoreChatClient : IChatClient
{
    private readonly IChatClient _innerClient;
    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<VectorStoreChatClient> _logger;
    private string? _vectorStoreId;
    private bool _vectorStoreChecked = false;

    public VectorStoreChatClient(
        IChatClient innerClient,
        KnowledgeBaseService knowledgeBaseService,
        ILogger<VectorStoreChatClient> logger)
    {
        _innerClient = innerClient;
        _knowledgeBaseService = knowledgeBaseService;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureVectorStoreIdAsync(cancellationToken);

        // Add system message about Vector Store if available
        var messagesList = chatMessages.ToList();
        var messagesWithContext = await AddVectorStoreContextAsync(messagesList, cancellationToken);

        return await _innerClient.GetResponseAsync(messagesWithContext, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureVectorStoreIdAsync(cancellationToken);

        // Add system message about Vector Store if available
        var messagesList = chatMessages.ToList();
        var messagesWithContext = await AddVectorStoreContextAsync(messagesList, cancellationToken);

        await foreach (var update in _innerClient.GetStreamingResponseAsync(messagesWithContext, options, cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType?.IsInstanceOfType(this) == true ? this : _innerClient.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        if (_innerClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task EnsureVectorStoreIdAsync(CancellationToken cancellationToken)
    {
        if (_vectorStoreChecked)
            return;

        try
        {
            _vectorStoreId = await _knowledgeBaseService.GetCurrentVectorStoreIdAsync();
            
            if (!string.IsNullOrEmpty(_vectorStoreId))
            {
                _logger.LogInformation("Vector Store enabled for chat: {VectorStoreId}", _vectorStoreId);
            }
            else
            {
                _logger.LogInformation("No Vector Store configured for chat");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Vector Store ID");
        }
        finally
        {
            _vectorStoreChecked = true;
        }
    }

    private async Task<IList<ChatMessage>> AddVectorStoreContextAsync(
        IList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        // If no Vector Store, return original messages
        if (string.IsNullOrEmpty(_vectorStoreId))
            return messages;

        // Check if we already have a system message with Vector Store context
        var hasVectorStoreContext = messages.Any(m => 
            m.Role == ChatRole.System && 
            m.Text?.Contains("Labware") == true);

        if (hasVectorStoreContext)
            return messages;

        // Get knowledge base info
        KnowledgeBaseInfo? kbInfo = null;
        try
        {
            kbInfo = await _knowledgeBaseService.GetKnowledgeBaseInfoAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge base info");
        }

        // Create enhanced system message
        var systemMessage = CreateSystemMessageWithVectorStore(kbInfo);

        // Add system message at the beginning
        var enhancedMessages = new List<ChatMessage> { systemMessage };
        enhancedMessages.AddRange(messages);

        return enhancedMessages;
    }

    private ChatMessage CreateSystemMessageWithVectorStore(KnowledgeBaseInfo? kbInfo)
    {
        var contextInfo = kbInfo != null && kbInfo.CompletedFiles > 0
            ? $"You have access to the Labware user manual (version 2025 v2.04) through Vector Store ID: {_vectorStoreId}. " +
              $"The knowledge base contains {kbInfo.CompletedFiles} processed document(s). "
            : $"You have access to a knowledge base (Vector Store ID: {_vectorStoreId}), but it may still be processing. ";

        var instructions = @"
You are a helpful AI assistant with access to the Labware user manual documentation.

IMPORTANT INSTRUCTIONS:
1. When users ask questions about Labware, use the knowledge from the uploaded PDF document
2. Provide accurate, step-by-step instructions based on the manual
3. Reference specific sections or page numbers when possible
4. If you're not sure about something, say so clearly
5. For questions about Labware functionality, always check the documentation first
6. You can answer in Dutch or English, matching the user's language

KNOWLEDGE BASE:
" + contextInfo + @"

The manual covers:
- Login procedures (Inloggen)
- User interface navigation
- Customer service workflows (Klantenservice)
- Sample management (Monsters)
- Planning and scheduling
- Laboratory operations (Biology and Chemistry)
- Quality control and validation
- And more...

When answering questions about Labware, be specific and reference the manual content.";

        return new ChatMessage(ChatRole.System, instructions);
    }
}
