using Microsoft.AspNetCore.Http;

namespace InvoiceGateway.Models
{
    public class UploadInvoiceRequest
    {
        public IFormFile File { get; set; } = default!;
    }
}
