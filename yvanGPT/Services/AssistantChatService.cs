using System.Text.Json;
using System.Text.Json.Serialization;

namespace yvanGPT.Services;

/// <summary>
/// Service for creating and managing OpenAI Assistants with Vector Store knowledge base
/// </summary>
public class AssistantChatService
{
    private readonly VectorStoreService _vectorStoreService;
    private readonly KnowledgeBaseService _knowledgeBaseService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssistantChatService> _logger;

    public AssistantChatService(
        VectorStoreService vectorStoreService,
        KnowledgeBaseService knowledgeBaseService,
        IConfiguration configuration,
        ILogger<AssistantChatService> logger)
    {
        _vectorStoreService = vectorStoreService;
        _knowledgeBaseService = knowledgeBaseService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the Vector Store ID from the configuration file
    /// </summary>
    public async Task<string?> GetVectorStoreIdAsync()
    {
        var vectorStoreId = await _knowledgeBaseService.GetCurrentVectorStoreIdAsync();
        
        if (!string.IsNullOrEmpty(vectorStoreId))
        {
            _logger.LogInformation("Using Vector Store ID from config: {VectorStoreId}", vectorStoreId);
        }
        else
        {
            _logger.LogWarning("No Vector Store ID found in configuration");
        }
        
        return vectorStoreId;
    }

    /// <summary>
    /// Checks if the knowledge base is ready to use
    /// </summary>
    public async Task<bool> IsKnowledgeBaseReadyAsync()
    {
        var vectorStoreId = await GetVectorStoreIdAsync();
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            return false;
        }

        try
        {
            var vectorStore = await _vectorStoreService.GetVectorStoreAsync(vectorStoreId);
            return vectorStore.Status == "completed" || vectorStore.FileCounts.Completed > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking knowledge base status");
            return false;
        }
    }

    /// <summary>
    /// Gets information about the current knowledge base
    /// </summary>
    public async Task<KnowledgeBaseStatus> GetKnowledgeBaseStatusAsync()
    {
        var vectorStoreId = await GetVectorStoreIdAsync();
        
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            return new KnowledgeBaseStatus
            {
                IsInitialized = false,
                Message = "Knowledge base not initialized. Please initialize it from the Knowledge Base page."
            };
        }

        try
        {
            var info = await _knowledgeBaseService.GetKnowledgeBaseInfoAsync();
            
            if (info == null)
            {
                return new KnowledgeBaseStatus
                {
                    IsInitialized = false,
                    Message = "Could not retrieve knowledge base information."
                };
            }

            var isReady = info.CompletedFiles > 0;
            var message = isReady
                ? $"Knowledge base ready with {info.CompletedFiles} file(s)"
                : info.InProgressFiles > 0
                    ? $"Knowledge base processing ({info.InProgressFiles} file(s) in progress)"
                    : "Knowledge base has no completed files";

            return new KnowledgeBaseStatus
            {
                IsInitialized = true,
                IsReady = isReady,
                VectorStoreId = vectorStoreId,
                Name = info.Name,
                TotalFiles = info.FileCount,
                CompletedFiles = info.CompletedFiles,
                InProgressFiles = info.InProgressFiles,
                FailedFiles = info.FailedFiles,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge base status");
            return new KnowledgeBaseStatus
            {
                IsInitialized = true,
                IsReady = false,
                VectorStoreId = vectorStoreId,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Example: Creates an assistant configuration with the Vector Store
    /// This returns the configuration that can be used with OpenAI's API
    /// </summary>
    public async Task<AssistantConfiguration?> GetAssistantConfigurationAsync()
    {
        var vectorStoreId = await GetVectorStoreIdAsync();
        
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            _logger.LogWarning("Cannot create assistant configuration without Vector Store ID");
            return null;
        }

        return new AssistantConfiguration
        {
            Model = _configuration["OpenAISettings:Model"] ?? "gpt-4o",
            Name = "Labware Assistant",
            Instructions = @"You are a helpful assistant with access to the Labware user manual. 
Use the file_search tool to find relevant information from the manual when answering questions.
Always provide accurate information based on the documentation.
If you cannot find the answer in the documentation, say so clearly.",
            Tools = new[] { new ToolDefinition { Type = "file_search" } },
            VectorStoreId = vectorStoreId
        };
    }
}

/// <summary>
/// Status information about the knowledge base
/// </summary>
public class KnowledgeBaseStatus
{
    public bool IsInitialized { get; set; }
    public bool IsReady { get; set; }
    public string? VectorStoreId { get; set; }
    public string? Name { get; set; }
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public int InProgressFiles { get; set; }
    public int FailedFiles { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for creating an OpenAI Assistant with Vector Store
/// </summary>
public class AssistantConfiguration
{
    public string Model { get; set; } = "gpt-4o";
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public ToolDefinition[] Tools { get; set; } = Array.Empty<ToolDefinition>();
    public string VectorStoreId { get; set; } = string.Empty;

    /// <summary>
    /// Converts to JSON format for OpenAI API
    /// </summary>
    public string ToJson()
    {
        var config = new
        {
            model = Model,
            name = Name,
            instructions = Instructions,
            tools = Tools,
            tool_resources = new
            {
                file_search = new
                {
                    vector_store_ids = new[] { VectorStoreId }
                }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
}

public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
