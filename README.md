# Azure Invoice Processing Project — Full Cloud Setup Recap

This document summarizes all the steps we took to set up the **Invoice Processing Pipeline** in Azure, from scratch to running both the Web API and Function App.

---
![Azure Resource Group Diagram](<a href="https://ibb.co/1JjzxMq1"><img src="https://i.ibb.co/XkMYTDs1/Untitled-diagram-Mermaid-Chart-2025-08-12-062917.png" alt="Untitled-diagram-Mermaid-Chart-2025-08-12-062917" border="0"></a>)

## **1. Resource Group**
- **Name:** `rg-invoice-dev`
- **Purpose:** Container for all Azure resources related to the project.
- **Region:** `UK South` (or closest).

---

## **2. Storage Account**
- **Name:** `stinvoice<unique>`
- **Purpose:** Store uploaded invoice files in Blob Storage.
- **Setup:**
  - Create **Blob container**: `invoices`
  - Access level: Private

---

## **3. Service Bus Namespace & Queue**
- **Namespace:** `sbinvoice-dev`
- **Queue:** `invoice-queue`
- **Purpose:** Event messaging — API sends a message when an invoice is uploaded, triggering the Function App.

---

## **4. Application Insights**
- **Name:** `appi-invoice-dev`
- **Purpose:** Logging, monitoring, and telemetry for API & Function App.

---

## **5. Key Vault**
- **Name:** `kv-invoice-dev`
- **Purpose:** Securely store sensitive data (e.g., API tokens, secrets).
- **Example Secret:** `PartnerApiToken` (placeholder).

---

## **6. App Service (Web API)**
- **Name:** `app-invoice-gateway-dev`
- **Runtime:** `.NET 8 (Windows)`
- **Plan:** Free (F1)
- **Purpose:** Host the Invoice Upload API.

---

## **7. Managed Identity Setup**
### API Identity
- **Enabled** system-assigned Managed Identity.
- Gave API the following access:
  - **Storage Blob Data Contributor** (to write files to Blob Storage).
  - **Azure Service Bus Data Sender** (to send messages to queue).
  - **Key Vault Secrets User** (if secrets needed).

### Function Identity
- **Enabled** system-assigned Managed Identity.
- Gave Function the following access:
  - **Storage Blob Data Reader** (to read files).
  - **Azure Service Bus Data Receiver** (to receive messages from queue).
  - **Key Vault Secrets User**.

---

## **8. Function App**
- **Name:** `func-invoice-processor-dev`
- **Runtime:** `.NET 8 (Windows, Consumption Plan)`
- **Purpose:** Triggered by Service Bus when a new invoice message arrives, reads blob file, processes it.

---

## **9. Configuration Settings**
**App Service (API)**  
```
Azure__Storage__AccountUrl = https://stinvoice<unique>.blob.core.windows.net
Azure__Storage__Container = invoices
Azure__ServiceBus__Namespace = sbinvoice-dev.servicebus.windows.net
```

**Function App**  
```
Azure__Storage__AccountUrl = https://stinvoice<unique>.blob.core.windows.net
KeyVaultName = kv-invoice-dev
ServiceBusConnection = <Service Bus connection string for trigger>
```

---

## **10. Local Development**
- Built `.NET Web API` for file uploads.
- Built `.NET Isolated Azure Function` for Service Bus processing.
- Local testing with `func start` and `dotnet run`.

---

## **11. Deployment Steps**
**API Deployment**
1. In Visual Studio or VS Code, publish API to `app-invoice-gateway-dev`.
2. Test via Swagger/Postman — file upload should create blob + queue message.

**Function Deployment**
1. Publish Function to `func-invoice-processor-dev`.
2. Test by uploading an invoice — Function should trigger.

---

## **12. Flow Overview**
**Sequence:**
1. User uploads invoice via API.
2. API stores file in Blob Storage.
3. API sends message to Service Bus Queue.
4. Function App triggers on queue message.
5. Function retrieves file from Blob.
6. Function processes invoice data.
7. Logs and metrics sent to Application Insights.

---

## **13. Diagram**
```mermaid
flowchart LR
    A[User Uploads Invoice via API] --> B[Blob Storage: Save File]
    B --> C[Service Bus: Send Message]
    C --> D[Azure Function: Trigger on Message]
    D --> E[Read File from Blob]
    E --> F[Process Invoice Data]
    F --> G[Log to Application Insights]
```

---

**Status:** ✅ Local testing complete. Ready for Azure deployment.
