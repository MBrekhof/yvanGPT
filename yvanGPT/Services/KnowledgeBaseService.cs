using System.Text.Json;

namespace yvanGPT.Services;

/// <summary>
/// High-level service for managing the knowledge base using Vector Stores
/// </summary>
public class KnowledgeBaseService
{
    private readonly VectorStoreService _vectorStoreService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KnowledgeBaseService> _logger;
    private const string ConfigFileName = "vectorstore.config.json";

    public KnowledgeBaseService(
        VectorStoreService vectorStoreService,
        IConfiguration configuration,
        ILogger<KnowledgeBaseService> logger)
    {
        _vectorStoreService = vectorStoreService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current vector store ID from configuration
    /// </summary>
    public async Task<string?> GetCurrentVectorStoreIdAsync()
    {
        try
        {
            if (!File.Exists(ConfigFileName))
                return null;

            var json = await File.ReadAllTextAsync(ConfigFileName);
            var config = JsonSerializer.Deserialize<VectorStoreConfig>(json);
            return config?.VectorStoreId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading vector store configuration");
            return null;
        }
    }

    /// <summary>
    /// Saves the vector store ID to configuration
    /// </summary>
    public async Task SaveVectorStoreIdAsync(string vectorStoreId, string name)
    {
        var config = new VectorStoreConfig
        {
            VectorStoreId = vectorStoreId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(ConfigFileName, json);
        _logger.LogInformation("Saved vector store configuration: {VectorStoreId}", vectorStoreId);
    }

    /// <summary>
    /// Initializes a new knowledge base with the Labware PDF
    /// </summary>
    public async Task<string> InitializeKnowledgeBaseAsync(
        string pdfFilePath, 
        CancellationToken cancellationToken = default)
    {
        // Check if file exists
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        // Create vector store
        var vectorStore = await _vectorStoreService.CreateVectorStoreAsync(
            "Labware Gebruikershandleiding Knowledge Base",
            cancellationToken);

        // Upload the PDF file
        var fileUpload = await _vectorStoreService.UploadFileAsync(pdfFilePath, cancellationToken);

        // Add file to vector store
        await _vectorStoreService.AddFileToVectorStoreAsync(
            vectorStore.Id,
            fileUpload.Id,
            cancellationToken);

        // Save configuration
        await SaveVectorStoreIdAsync(vectorStore.Id, vectorStore.Name);

        _logger.LogInformation(
            "Initialized knowledge base with vector store {VectorStoreId} and file {FileId}",
            vectorStore.Id,
            fileUpload.Id);

        return vectorStore.Id;
    }

    /// <summary>
    /// Adds an additional file to the existing knowledge base
    /// </summary>
    public async Task<string> AddFileToKnowledgeBaseAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        var vectorStoreId = await GetCurrentVectorStoreIdAsync();
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            throw new InvalidOperationException("No knowledge base exists. Please initialize first.");
        }

        // Check if file exists
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        // Upload the PDF file
        var fileUpload = await _vectorStoreService.UploadFileAsync(pdfFilePath, cancellationToken);

        // Add file to vector store
        await _vectorStoreService.AddFileToVectorStoreAsync(
            vectorStoreId,
            fileUpload.Id,
            cancellationToken);

        _logger.LogInformation(
            "Added file {FileId} to knowledge base {VectorStoreId}",
            fileUpload.Id,
            vectorStoreId);

        return fileUpload.Id;
    }

    /// <summary>
    /// Gets information about the current knowledge base
    /// </summary>
    public async Task<KnowledgeBaseInfo?> GetKnowledgeBaseInfoAsync(CancellationToken cancellationToken = default)
    {
        var vectorStoreId = await GetCurrentVectorStoreIdAsync();
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            return null;
        }

        try
        {
            var vectorStore = await _vectorStoreService.GetVectorStoreAsync(vectorStoreId, cancellationToken);
            var files = await _vectorStoreService.ListVectorStoreFilesAsync(vectorStoreId, cancellationToken);

            return new KnowledgeBaseInfo
            {
                VectorStoreId = vectorStore.Id,
                Name = vectorStore.Name,
                Status = vectorStore.Status,
                FileCount = vectorStore.FileCounts.Total,
                CompletedFiles = vectorStore.FileCounts.Completed,
                InProgressFiles = vectorStore.FileCounts.InProgress,
                FailedFiles = vectorStore.FileCounts.Failed,
                UsageBytes = vectorStore.UsageBytes,
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(vectorStore.CreatedAt).DateTime,
                LastActiveAt = vectorStore.LastActiveAt.HasValue 
                    ? DateTimeOffset.FromUnixTimeSeconds(vectorStore.LastActiveAt.Value).DateTime 
                    : null,
                Files = files.Data.Select(f => new FileInfo
                {
                    FileId = f.Id,
                    Status = f.Status,
                    UsageBytes = f.UsageBytes,
                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(f.CreatedAt).DateTime
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge base info");
            return null;
        }
    }

    /// <summary>
    /// Deletes the current knowledge base
    /// </summary>
    public async Task<bool> DeleteKnowledgeBaseAsync(CancellationToken cancellationToken = default)
    {
        var vectorStoreId = await GetCurrentVectorStoreIdAsync();
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            return false;
        }

        await _vectorStoreService.DeleteVectorStoreAsync(vectorStoreId, cancellationToken);

        // Delete configuration file
        if (File.Exists(ConfigFileName))
        {
            File.Delete(ConfigFileName);
        }

        _logger.LogInformation("Deleted knowledge base {VectorStoreId}", vectorStoreId);
        return true;
    }

    /// <summary>
    /// Checks if a knowledge base is already initialized
    /// </summary>
    public async Task<bool> IsKnowledgeBaseInitializedAsync()
    {
        var vectorStoreId = await GetCurrentVectorStoreIdAsync();
        return !string.IsNullOrEmpty(vectorStoreId);
    }
}

// Configuration model
public class VectorStoreConfig
{
    public string VectorStoreId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}

// Info models
public class KnowledgeBaseInfo
{
    public string VectorStoreId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int CompletedFiles { get; set; }
    public int InProgressFiles { get; set; }
    public int FailedFiles { get; set; }
    public long UsageBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public List<FileInfo> Files { get; set; } = new();
}

public class FileInfo
{
    public string FileId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long UsageBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}
