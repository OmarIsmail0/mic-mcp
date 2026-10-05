# Company.McpServer

A small ASP.NET Core [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server that exposes
tools for LLM clients and forwards them to internal/external REST APIs. It is intentionally lightweight:
one web project, no database, no CQRS — just MCP tools → service interfaces → typed `HttpClient`s.

- .NET 10, official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK (2.2.0)
- Streamable HTTP transport at `/mcp` (stateless — no session affinity needed)
- `X-API-Key` authentication for MCP clients
- `GET /health` without authentication

The tools shipped here (`get_customer`, `search_customers`, `create_customer`, `get_product`, `search_products`,
`get_order`, `get_customer_orders`, `create_order`) are demonstrations against a placeholder API
(`https://api.example.com/`). Point `ExternalApi:BaseUrl` at a real API and adjust the services/models to match.

## Architecture

```
MCP Client / MCP Inspector
        │  X-API-Key  (key for THIS server)
        ▼
API Key Middleware  ─ only guards /mcp; /health stays open
        ▼
MCP Tools            Mcp/Tools/*          thin: validate args, call a service, no HttpClient here
        ▼
Service Interfaces   Services/Interfaces/*
        ▼
API Services         Services/Api/*       typed HttpClient + GetFromJsonAsync / PostAsJsonAsync
        ▼
External/Internal REST API  (separate credentials: ExternalApi:ApiKey)
```

Two independent credentials are involved and are never reused for one another:

| Credential | Who sends it | Who checks it | Setting |
|---|---|---|---|
| MCP server key | MCP client | this server (`ApiKeyMiddleware`) | `McpAuthentication:ApiKey` |
| External API key | this server | the REST API | `ExternalApi:ApiKey` |

**Error handling.** `Mcp/ToolGuard.cs` wraps every tool call: it logs start/finish, validates arguments, and converts
upstream failures (connection errors, timeouts, non-success status codes, unreadable payloads) into `McpException`s with
generic, safe messages. A 404 from the API becomes a "not found" result (not an error log). For tools that change data
(`create_*`), a timeout or 5xx tells the client the outcome is **unknown** and to check before retrying, so an LLM does
not create duplicates. The MCP SDK only forwards `McpException` messages to clients, so exception details, URLs and
secrets never leave the server. Client cancellation is propagated, not reported as an error.

## Folder structure

```
.
├── Company.McpServer.slnx
├── README.md
├── .gitignore
├── Company.McpServer/
│   ├── Company.McpServer.csproj
│   ├── Program.cs                         small: AddMcpApplication, middleware, /health, MapMcp
│   ├── appsettings.json                   safe defaults, no secrets
│   ├── appsettings.Development.json       dev-only placeholder key
│   ├── Authentication/                    ApiKeyMiddleware, ApiKeyOptions, ApiKeyConstants
│   ├── Configuration/                     ApiSettings (ExternalApi), McpSettings (Mcp)
│   ├── Extensions/ServiceCollectionExtensions.cs   all DI / MCP / HttpClient registration
│   ├── Mcp/
│   │   ├── ToolGuard.cs                   logging, argument checks, safe error translation
│   │   └── Tools/                         CustomerTools, ProductTools, OrderTools
│   ├── Models/{Requests,Responses}/       strongly typed DTOs
│   └── Services/{Interfaces,Api}/         ICustomerApiService → CustomerApiService, ...
└── tests/Company.McpServer.Tests/         xUnit: auth/endpoint tests, service tests, tool tests
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` → `10.x`)
- Node.js, only if you want to use MCP Inspector
- Optional, for the HTTPS URL: `dotnet dev-certs https --trust`

## Restore, build, test, run

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project Company.McpServer
```

`dotnet run` uses the `http` launch profile (Development environment):

| Profile | URLs |
|---|---|
| `http` (default) | `http://localhost:5006` |
| `https` | `https://localhost:7094` and `http://localhost:5006` |

```bash
dotnet run --project Company.McpServer --launch-profile https
```

In Development the HTTP-to-HTTPS redirect is disabled so the plain HTTP URL works directly. In every other environment
`UseHttpsRedirection` is enabled so keys are not sent in clear text; behind a TLS-terminating proxy, make sure it forwards
the original scheme.

## Configure the MCP API key

The key MCP clients must send in the `X-API-Key` header is read from `McpAuthentication:ApiKey`.

- `appsettings.json` ships it **empty**. An empty key makes the server reject every `/mcp` request (fail closed), and
  outside the Development environment the app refuses to start without it.
- `appsettings.Development.json` contains `development-only-key` so local runs work. It is not a secret; never reuse it.
  **Never run a deployed instance with `ASPNETCORE_ENVIRONMENT=Development`** — it would accept that public key.
- Anywhere else, supply the key outside source control. ASP.NET Core maps `__` in environment variable names to `:`:

```bash
# macOS / Linux
export McpAuthentication__ApiKey="a-long-random-value"
```

```powershell
# Windows PowerShell
$env:McpAuthentication__ApiKey = "a-long-random-value"
```

For local development you can alternatively use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(stored outside the repo):

```bash
dotnet user-secrets init --project Company.McpServer
dotnet user-secrets set "McpAuthentication:ApiKey" "a-long-random-value" --project Company.McpServer
```

Generate a strong key with `openssl rand -base64 32`. The key is never logged.

## Configure the external API

```json
"ExternalApi": {
  "BaseUrl": "https://api.example.com/",
  "ApiKey": "",
  "ApiKeyHeaderName": "X-API-Key",
  "TimeoutSeconds": 30
}
```

- `BaseUrl` — absolute URL of the REST API. Empty in `appsettings.json` on purpose (Development uses the
  `https://api.example.com/` placeholder). Outside Development it must be set and must be `https`, otherwise the app
  stops at startup with a clear message, so the external key can never be sent to a placeholder host or in clear text.
  `ApiKeyHeaderName` and `TimeoutSeconds` are validated at startup too.
- `ApiKey` — credential sent **to the external API** (as the header named by `ApiKeyHeaderName`; omitted when empty).
  Set it via environment variable, never in a committed file:
  ```bash
  export ExternalApi__ApiKey="external-api-secret"
  export ExternalApi__BaseUrl="https://real-api.internal.example/"
  ```
- Need `Authorization: Bearer ...` instead of a custom header? Change the header setup in
  `AddExternalApiClient` in `Extensions/ServiceCollectionExtensions.cs`.

## Test `/health`

```bash
curl -i http://localhost:5006/health
```

```json
{ "status": "Healthy" }
```

No API key is needed for `/health`.

## Test the MCP endpoint with curl

```bash
# No key → 401
curl -i -X POST http://localhost:5006/mcp \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# With key → 200, tool list returned as a server-sent event
curl -i -X POST http://localhost:5006/mcp \
  -H "X-API-Key: development-only-key" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

The server is stateless, so `POST /mcp` is the only supported method (`GET`/`DELETE` return 405).

## Connect with MCP Inspector

Start the server, then:

```bash
npx -y @modelcontextprotocol/inspector
```

In the Inspector UI:

```
Transport Type:   Streamable HTTP
URL:              https://localhost:7094/mcp      (or http://localhost:5006/mcp)
Authentication →  Custom Headers:
                  X-API-Key: development-only-key   (or your configured key)
```

Click **Connect**, open the **Tools** tab, and **List Tools**. For the HTTPS URL the ASP.NET Core dev certificate must be
trusted (`dotnet dev-certs https --trust`); otherwise use the HTTP URL locally.

Headless alternative (also handy in scripts):

```bash
npx -y @modelcontextprotocol/inspector --cli http://localhost:5006/mcp \
  --transport http --header "X-API-Key: development-only-key" --method tools/list
```

Calling a tool succeeds end to end but returns an error result such as *"The external API could not be reached"* until
`ExternalApi:BaseUrl` points at a real API — `api.example.com` is only a placeholder.

## Add a new MCP tool

1. If it needs a new REST call, add a method to the relevant interface in `Services/Interfaces/` and implement it in
   `Services/Api/` (use `GetFromJsonAsync` / `PostAsJsonAsync`, pass the `CancellationToken`).
2. Add response/request records under `Models/`.
3. Add a method to a tool class in `Mcp/Tools/`. Tools are discovered automatically (`WithToolsFromAssembly`):

   ```csharp
   [McpServerTool(Name = "get_invoice", ReadOnly = true, Idempotent = true)]
   [Description("Retrieves an invoice by id. Use when the invoice id is known.")]
   public Task<InvoiceResponse> GetInvoice(
       [Description("Numeric id of the invoice.")] int invoiceId,
       CancellationToken cancellationToken) =>
       ToolGuard.RunAsync(logger, "get_invoice", async () =>
       {
           ToolGuard.RequireId(invoiceId, nameof(invoiceId));
           return await invoiceApi.GetInvoiceAsync(invoiceId, cancellationToken)
               ?? throw new McpException($"Invoice {invoiceId} was not found.");
       }, cancellationToken);
   ```

   Write descriptions for the LLM: what the tool does, when to use it, and what each parameter means. Mark tools that
   change data with `ReadOnly = false`.
4. Add a test under `tests/`.

## Add another external API service

1. Create `IShippingApiService` in `Services/Interfaces/` and `ShippingApiService` in `Services/Api/`
   (constructor takes `HttpClient` and `ILogger<>`).
2. Register it in `AddExternalApiClients` in `Extensions/ServiceCollectionExtensions.cs`:

   ```csharp
   services.AddExternalApiClient<IShippingApiService, ShippingApiService>();
   ```

3. If it talks to a **different** API (other base URL or credentials), add another settings class (for example
   `ShippingApiSettings`, section `ShippingApi`), bind and validate it like `ApiSettings`, and write a small variant of
   `AddExternalApiClient` that reads those settings. Keep each API's credentials in its own configuration section.
4. Inject the interface into a tool class — tools must never use `HttpClient` directly.

## Security notes

- `/mcp` is protected by `X-API-Key`; keys are compared in constant time and are never logged. Missing, wrong, or
  unconfigured keys all return `401`.
- Do not log request/response bodies from the external API; the services log only method and path templates, and search
  text is redacted.
- Use HTTPS in any shared environment, and keep real secrets in environment variables or a secret manager.
- If this server is ever exposed to browsers or the public internet, consider adding rate limiting and an allow-list of
  `Origin`/`Host` values.
