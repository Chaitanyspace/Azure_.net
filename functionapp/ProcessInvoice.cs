using System.Net.Http.Headers;
using System.Web;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InvoiceProcessor;

public class ProcessInvoice
{
	private readonly ILogger<ProcessInvoice> _log;
	private readonly IConfiguration _config;

	public ProcessInvoice(ILogger<ProcessInvoice> log, IConfiguration config)
	{
		_log = log;
		_config = config;
	}

	// Service Bus queue trigger: "invoice-queue"
	// Uses app setting named "ServiceBusConnection"
	[Function("ProcessInvoice")]
	public async Task Run(
		[ServiceBusTrigger("invoice-queue", Connection = "ServiceBusConnection")]
		ServiceBusReceivedMessage message)
	{
		var payload = message.Body.ToObjectFromJson<InvoiceMessage>();
		_log.LogInformation("Received invoice {InvoiceId} (corr {CorrelationId})",
			payload.InvoiceId, payload.CorrelationId);

		// 1) Download the blob
		var bytes = await DownloadBlobAsync(payload.BlobUrl);

		// 2) Get partner token (local override -> Key Vault)
		var token = await GetPartnerTokenAsync();

		// 3) Call external API (demo endpoint)
		using var http = new HttpClient();
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		using var content = new ByteArrayContent(bytes);
		if (!string.IsNullOrWhiteSpace(payload.ContentType))
			content.Headers.ContentType = new MediaTypeHeaderValue(payload.ContentType);

		// Replace with real partner endpoint later
		var resp = await http.PostAsync("https://httpbin.org/post", content);
		resp.EnsureSuccessStatusCode();

		_log.LogInformation("Processed invoice {InvoiceId}", payload.InvoiceId);
	}

	private async Task<byte[]> DownloadBlobAsync(string blobUrl)
	{
		// Prefer storage connection string locally (easy dev)
		var conn = _config["Azure__Storage__ConnectionString"] ?? _config["Values:Azure__Storage__ConnectionString"];
		if (!string.IsNullOrWhiteSpace(conn))
		{
			// Parse container & blob name from URL
			var uri = new Uri(blobUrl);
			var path = uri.AbsolutePath.Trim('/'); // container/blob/path
			var firstSlash = path.IndexOf('/');
			if (firstSlash < 0) throw new InvalidOperationException("Invalid blob URL format");

			var container = path[..firstSlash];
			var blobName = HttpUtility.UrlDecode(path[(firstSlash + 1)..]);

			var blobSvc = new BlobServiceClient(conn);
			var blob = blobSvc.GetBlobContainerClient(container).GetBlobClient(blobName);
			using var ms = new MemoryStream();
			await blob.DownloadToAsync(ms);
			return ms.ToArray();
		}

		// Fallback to token-based auth (used in Azure with MSI/az login)
		var client = new Azure.Storage.Blobs.BlobClient(new Uri(blobUrl), new DefaultAzureCredential());
		using var mem = new MemoryStream();
		await client.DownloadToAsync(mem);
		return mem.ToArray();
	}

	private async Task<string> GetPartnerTokenAsync()
	{
		// Local override (so you can run without Key Vault)
		var local = _config["PartnerApiToken"] ?? _config["Values:PartnerApiToken"];
		if (!string.IsNullOrWhiteSpace(local)) return local;

		// Key Vault (for Azure)
		var kvName = _config["KeyVaultName"] ?? _config["AppSettings:KeyVaultName"];
		if (string.IsNullOrWhiteSpace(kvName))
			throw new InvalidOperationException("PartnerApiToken not set and KeyVaultName missing.");

		var client = new SecretClient(new Uri($"https://{kvName}.vault.azure.net/"), new DefaultAzureCredential());
		var secret = await client.GetSecretAsync("PartnerApiToken");
		return secret.Value.Value;
	}

	private sealed record InvoiceMessage(
		string InvoiceId,
		string BlobUrl,
		string? ContentType,
		string? OriginalFileName,
		string? UploadedBy,
		string CorrelationId,
		DateTime ReceivedUtc);
}
