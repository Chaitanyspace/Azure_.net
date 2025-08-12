using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using InvoiceGateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly BlobContainerClient _container;
    private readonly ServiceBusSender _sender;

    public InvoicesController(BlobContainerClient container, ServiceBusSender sender)
    {
        _container = container;
        _sender = sender;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] UploadInvoiceRequest request)
    {
        var file = request.File;
        if (file == null || file.Length == 0)
            return BadRequest("file is required (multipart/form-data with a 'file' field)");

        await _container.CreateIfNotExistsAsync();

        var invoiceId = Guid.NewGuid().ToString("N");
        var ext = Path.GetExtension(file.FileName);
        var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{invoiceId}{ext}";
        var blob = _container.GetBlobClient(blobName);

        await using (var stream = file.OpenReadStream())
        {
            Console.WriteLine($"Uploading to: {blob.Uri} (using connection string)");
            await blob.UploadAsync(stream, overwrite: false);
        }

        var correlationId = Guid.NewGuid().ToString();

        var payload = new
        {
            invoiceId,
            blobUrl = blob.Uri.ToString(),
            originalFileName = file.FileName,
            contentType = file.ContentType,
            uploadedBy = "local-dev",
            correlationId,
            receivedUtc = DateTime.UtcNow
        };

        var msg = new ServiceBusMessage(BinaryData.FromObjectAsJson(payload))
        {
            MessageId = invoiceId,
            CorrelationId = correlationId
        };

        await _sender.SendMessageAsync(msg);

        return Created($"/api/invoices/{invoiceId}", new { invoiceId, blobUrl = blob.Uri, correlationId });
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id) => Ok(new { invoiceId = id, status = "accepted" });
}
