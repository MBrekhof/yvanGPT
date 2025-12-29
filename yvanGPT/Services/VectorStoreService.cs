using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace yvanGPT.Services;

/// <summary>
/// Service for managing OpenAI Vector Stores for persistent knowledge bases
/// </summary>
public class VectorStoreService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VectorStoreService> _logger;
    private const string BaseUrl = "https://api.openai.com/v1";

    public VectorStoreService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<VectorStoreService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var apiKey = configuration["OpenAISettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured");
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
    }

    /// <summary>
    /// Creates a new vector store
    /// </summary>
    public async Task<VectorStoreResponse> CreateVectorStoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            name = name,
            expires_after = new
            {
                anchor = "last_active_at",
                days = 365
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}/vector_stores", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var vectorStore = JsonSerializer.Deserialize<VectorStoreResponse>(responseContent);

        _logger.LogInformation("Created vector store: {VectorStoreId}", vectorStore?.Id);
        return vectorStore!;
    }

    /// <summary>
    /// Lists all vector stores
    /// </summary>
    public async Task<VectorStoreListResponse> ListVectorStoresAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/vector_stores", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<VectorStoreListResponse>(responseContent);

        return result!;
    }

    /// <summary>
    /// Retrieves a specific vector store
    /// </summary>
    public async Task<VectorStoreResponse> GetVectorStoreAsync(string vectorStoreId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/vector_stores/{vectorStoreId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var vectorStore = JsonSerializer.Deserialize<VectorStoreResponse>(responseContent);

        return vectorStore!;
    }

    /// <summary>
    /// Deletes a vector store
    /// </summary>
    public async Task<bool> DeleteVectorStoreAsync(string vectorStoreId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/vector_stores/{vectorStoreId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Deleted vector store: {VectorStoreId}", vectorStoreId);
        return true;
    }

    /// <summary>
    /// Uploads a file to OpenAI
    /// </summary>
    public async Task<FileUploadResponse> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, cancellationToken));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        form.Add(new StringContent("assistants"), "purpose");

        var response = await _httpClient.PostAsync($"{BaseUrl}/files", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var fileUpload = JsonSerializer.Deserialize<FileUploadResponse>(responseContent);

        _logger.LogInformation("Uploaded file: {FileId}", fileUpload?.Id);
        return fileUpload!;
    }

    /// <summary>
    /// Adds a file to a vector store
    /// </summary>
    public async Task<VectorStoreFileResponse> AddFileToVectorStoreAsync(
        string vectorStoreId, 
        string fileId, 
        CancellationToken cancellationToken = default)
    {
        var request = new { file_id = fileId };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            $"{BaseUrl}/vector_stores/{vectorStoreId}/files", 
            content, 
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var vectorStoreFile = JsonSerializer.Deserialize<VectorStoreFileResponse>(responseContent);

        _logger.LogInformation("Added file {FileId} to vector store {VectorStoreId}", fileId, vectorStoreId);
        return vectorStoreFile!;
    }

    /// <summary>
    /// Lists files in a vector store
    /// </summary>
    public async Task<VectorStoreFileListResponse> ListVectorStoreFilesAsync(
        string vectorStoreId, 
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{BaseUrl}/vector_stores/{vectorStoreId}/files", 
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<VectorStoreFileListResponse>(responseContent);

        return result!;
    }

    /// <summary>
    /// Removes a file from a vector store
    /// </summary>
    public async Task<bool> RemoveFileFromVectorStoreAsync(
        string vectorStoreId, 
        string fileId, 
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"{BaseUrl}/vector_stores/{vectorStoreId}/files/{fileId}", 
            cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Removed file {FileId} from vector store {VectorStoreId}", fileId, vectorStoreId);
        return true;
    }
}

// Response models
public class VectorStoreResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("usage_bytes")]
    public long UsageBytes { get; set; }

    [JsonPropertyName("file_counts")]
    public FileCounts FileCounts { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expires_after")]
    public ExpiresAfter? ExpiresAfter { get; set; }

    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    [JsonPropertyName("last_active_at")]
    public long? LastActiveAt { get; set; }
}

public class FileCounts
{
    [JsonPropertyName("in_progress")]
    public int InProgress { get; set; }

    [JsonPropertyName("completed")]
    public int Completed { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("cancelled")]
    public int Cancelled { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class ExpiresAfter
{
    [JsonPropertyName("anchor")]
    public string Anchor { get; set; } = string.Empty;

    [JsonPropertyName("days")]
    public int Days { get; set; }
}

public class VectorStoreListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<VectorStoreResponse> Data { get; set; } = new();

    [JsonPropertyName("first_id")]
    public string? FirstId { get; set; }

    [JsonPropertyName("last_id")]
    public string? LastId { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

public class FileUploadResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;
}

public class VectorStoreFileResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("usage_bytes")]
    public long UsageBytes { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("vector_store_id")]
    public string VectorStoreId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("last_error")]
    public object? LastError { get; set; }
}

public class VectorStoreFileListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<VectorStoreFileResponse> Data { get; set; } = new();

    [JsonPropertyName("first_id")]
    public string? FirstId { get; set; }

    [JsonPropertyName("last_id")]
    public string? LastId { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
