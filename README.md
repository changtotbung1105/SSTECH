# Partner Integration BFF

A .NET 8 Web API that validates partner transactions, verifies and enriches them through a Partner Verification API, and publishes them to RabbitMQ. It returns `202 Accepted` only after the broker confirms the message.

The scope ends at queue delivery: a legacy consumer and downstream business processing are not included. Docker Compose provides the API and a local RabbitMQ instance; the API can also run directly on Windows.

- [Run with Docker](#run-with-docker)
- [Run on Windows](#run-directly-on-windows-without-docker)
- [Architecture](#clean-architecture-and-solid)
- [Testing and verification](#testing-and-verification)

## Run with Docker

Requires Docker Engine with Docker Compose v2, or Docker Desktop configured for Linux containers. No host .NET SDK is needed for the container build. Clone the repository and run the commands from its root:

```sh
git clone https://github.com/changtotbung1105/SSTECH.git
cd SSTECH
```

PowerShell:

```powershell
Copy-Item .env.example .env
# Update PARTNER_API_KEY in .env if needed.
docker compose up --build -d
docker compose ps
```

On Bash, use `cp .env.example .env` instead of `Copy-Item`; the Docker commands are the same. Create `.env` only on the first run so existing configuration is preserved. Wait for the API startup message in `docker compose logs api` before sending requests.

API: http://localhost:8080. RabbitMQ Management: http://localhost:15672, with demo credentials `partner` / `local-demo-password`. Published ports are bound to loopback. These credentials are for local development only.

Send a transaction from PowerShell:

```powershell
$headers = @{ 'X-Api-Key' = 'local-demo-change-this-key' }
$body = @{
    partnerId = 'P-1001'
    transactionReference = 'TXN-99823'
    amount = 250.00
    currency = 'USD'
    timestamp = '2024-05-10T14:30:00Z'
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri 'http://localhost:8080/api/v1/partner/transactions' `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body $body
```

If you changed the API key in `.env`, use the same value in the request header.

Expected response: **HTTP 202 Accepted** (the generated ID varies).

```json
{
  "messageId": "93a03eab-a782-4729-a50a-90a77bcc4e17",
  "transactionReference": "TXN-99823",
  "status": "queued"
}
```

The queued JSON preserves the request fields and adds `partnerName`, `messageId`, `schemaVersion` (1), and `receivedAt`. Acceptance means queued for later processing, not that the transaction has completed.

In RabbitMQ Management, open **Queues and Streams → partner.transactions** to inspect messages. The queue is created on the first publish. No consumer is included because legacy processing is outside the exercise scope.

To check persistence, run `docker compose restart rabbitmq` and verify that unconsumed messages remain. `docker compose down` preserves the volume; adding `-v` deletes its data.

## Run directly on Windows without Docker

The API and RabbitMQ run directly on Windows without Docker, WSL, or BIOS virtualization.

### 1. Install prerequisites

- Install the .NET 8 SDK. A newer SDK can build through the roll-forward policy in `global.json`, but running the application still requires the .NET 8 runtime.
- Install 64-bit Erlang/OTP first, then RabbitMQ Server using the Windows installer. Follow the [official installation guide](https://www.rabbitmq.com/docs/install-windows) and select compatible versions using the [Erlang compatibility matrix](https://www.rabbitmq.com/docs/which-erlang).
- Open `services.msc`, find **RabbitMQ**, and check that its status is **Running**. Select **Start** if it is stopped.

### 2. Enable RabbitMQ Management

Open **RabbitMQ Command Prompt (sbin dir)** from the Start Menu using **Run as administrator**, then run:

```cmd
rabbitmq-plugins.bat enable rabbitmq_management
rabbitmq-diagnostics.bat ping
```

The `ping` command should succeed. Open http://localhost:15672 and sign in with `guest` / `guest` for a fresh default installation. This account is restricted to local connections. Docker Compose uses different demo credentials: `partner` / `local-demo-password`.

### 3. Start the API

**Quick start using the included local profile:** once RabbitMQ is running, open PowerShell at the repository root:

```powershell
cd src/PartnerIntegration.Api
dotnet run
```

The API listens on `https://localhost:64882` and `http://localhost:64883`. Submit transactions to **POST https://localhost:64882/api/v1/partner/transactions** with the header `X-Api-Key: local-demo-change-this-key`.

The profile connects to local RabbitMQ using `guest` / `guest` and calls the mock Partner API over HTTP on port 64883. Demo settings are defined in `src/PartnerIntegration.Api/Properties/launchSettings.json` for local use only.

Alternatively, run `dotnet run --project src/PartnerIntegration.Api` from the solution directory. In Visual Studio, set the API as the startup project, select the `PartnerIntegration.Api` profile, and press **F5**. Visual Studio opens `/health/live`; `dotnet run` does not automatically open a browser.

Opening the transactions URL in a browser sends GET and returns 405. Use POST through Postman or the PowerShell example below to submit a transaction.

If the local HTTPS certificate is not trusted, run `dotnet dev-certs https --trust` once, accept the Windows confirmation, and restart the API.

**Custom configuration on port 8080:** open PowerShell at the repository root and run these commands in the same window:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:8080'
$env:Security__ApiKey = 'local-demo-change-this-key'
$env:PartnerApi__BaseUrl = 'http://localhost:8080/'
$env:RabbitMq__Uri = 'amqp://guest:guest@localhost:5672/'

dotnet run --project src/PartnerIntegration.Api --no-launch-profile
```

Keep this window open. `Now listening on: http://localhost:8080` indicates that the API has started. These environment variables apply only to the current PowerShell session. This approach does not require `.env`; `dotnet run` does not automatically load that file.

### 4. Send a transaction

Open a second PowerShell window. This example uses the default HTTPS profile. For the custom configuration, change `$baseUrl` to `http://localhost:8080`:

```powershell
$baseUrl = 'https://localhost:64882'
Invoke-RestMethod -Uri "$baseUrl/health/live"

$headers = @{ 'X-Api-Key' = 'local-demo-change-this-key' }
$body = @{
    partnerId = 'P-1001'
    transactionReference = 'TXN-99823'
    amount = 250.00
    currency = 'USD'
    timestamp = '2024-05-10T14:30:00Z'
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri "$baseUrl/api/v1/partner/transactions" `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body $body
```

Success returns HTTP `202` with `messageId`, `transactionReference`, and `status: queued`. In RabbitMQ Management, open **Queues and Streams → partner.transactions**. When inspecting payloads with **Get messages**, select a requeue mode if you want to keep the messages in the queue.

The mock may time out on each verification attempt. If retries are exhausted, the API returns `503`. For repeated failures, inspect the API logs to distinguish Partner API failures from RabbitMQ connection failures.

### 5. Stop the application

Stop the API with `Ctrl+C` in its terminal. Use **Stop** or **Restart** in `services.msc` to manage RabbitMQ. To check persistence, publish a message, restart the service, and verify that unconsumed messages remain. See [Testing and verification](#testing-and-verification) for automated tests.

## Clean Architecture and SOLID

The solution separates responsibilities into four production projects. Dependencies point toward the business rules; Domain and Application have no ASP.NET Core, RabbitMQ, Polly, or external NuGet dependencies.

```mermaid
flowchart LR
    API[API / composition root] --> Application
    API --> Infrastructure
    API -->|shared validation rules| Domain
    Infrastructure --> Application
    Application --> Domain
```

Arrows show code dependencies, not the runtime request flow. API uses Domain through the transitive project reference for shared validation rules; its direct project references are Application and Infrastructure.

| Project | Responsibility |
| --- | --- |
| `PartnerIntegration.Domain` | Immutable `PartnerTransaction` and shared `TransactionRules`; rejects invalid business data regardless of the caller |
| `PartnerIntegration.Application` | `SubmitTransaction` use case, commands/receipts, message contract, and narrow `IPartnerVerifier` / `ITransactionPublisher` ports |
| `PartnerIntegration.Infrastructure` | HTTP partner verification with resilience, RabbitMQ publishing, and registration of these adapters |
| `PartnerIntegration.Api` | HTTP DTOs and validation, controller, API-key authentication, exception-to-HTTP mapping, Development mock, and dependency wiring |

The composition root is `Program.cs` in the API project. It references Infrastructure to register concrete adapters. Controllers depend on `ISubmitTransaction`; they do not call RabbitMQ or HTTP clients. Application references Domain, and Infrastructure implements Application interfaces. Assembly dependency tests guard these boundaries.

- **Single responsibility:** the controller handles HTTP, the use case orchestrates acceptance, Domain enforces transaction invariants, and adapters handle external protocols.
- **Open/closed:** a new partner verifier or message destination can implement an existing port and be registered without changing the use case.
- **Liskov substitution:** implementations must honor the port contracts: verifier rejection returns null, failures propagate, and publishing completes only after destination confirmation. Tests exercise replacement adapters and failure behavior.
- **Interface segregation:** verification, publishing, and submission each have a focused interface; callers do not depend on unrelated operations.
- **Dependency inversion:** business orchestration depends on ports owned by Application, not on RabbitMQ, `HttpClient`, or ASP.NET Core. `TimeProvider` makes receipt timestamps deterministic in tests.

Amounts use `decimal`, timestamps use `DateTimeOffset`, and nullable HTTP DTO properties distinguish omitted values. DataAnnotations provide HTTP validation responses using shared Domain rules; the Domain constructor also protects callers that invoke the use case directly. No database, generic repository, or mediator is introduced because this exercise does not require them.

The request flow is: authenticate → validate → verify partner over HTTP → enrich the transaction → publish to RabbitMQ → await broker confirmation → return 202. Legacy consumption and business processing happen later and are not implemented here.

**Validation assumptions:** all five request fields are required. Partner IDs and transaction references must be nonblank and at most 100 characters; amounts must be positive. Currency validation accepts USD, EUR, GBP, VND, JPY, SGD, AUD, CAD, CHF, CNY, and THB, in uppercase. This implementation interprets valid currency as a supported currency: it is an explicit allow-list, not complete ISO 4217 validation. Valid ISO codes outside this list, such as NZD, are currently rejected. Timestamps must be parseable and non-default, but need not be recent because the specification does not impose that restriction.

## Partner verification and resilience

The mock `GET /mock/partners/{partnerId}` endpoint is enabled only in Development, including the Compose demo. Each call independently has a 30% chance of throwing `TimeoutException` and a 70% chance of returning a verified partner. The mock accepts any supplied partner ID on its success branch; it has no partner registry. Rejected partners are covered through test doubles. The global exception handler converts the timeout into HTTP 504. The BFF calls the mock over HTTP rather than invoking its method directly.

`Microsoft.Extensions.Http.Resilience` provides the standard handler with at most two retries (three attempts total), exponential backoff starting at 200 ms with jitter, a two-second attempt timeout, and an eight-second total request timeout. It retries network errors, HTTP 408, 429, and 5xx, but not other 4xx responses. The handler also provides circuit breaking and concurrency limiting. This pipeline is used for the partner verification GET request, and caller cancellation is propagated.

Do not expect an exact 30/70 split in a small sample. Assuming three independent attempts reach the mock, the chance that all three time out is 2.7%, so an occasional 503 is expected. The circuit breaker can reject requests early during sustained failures. Production must point `PartnerApi__BaseUrl` at a compatible external service because the local mock is disabled outside Development.

## Messaging reliability

The publisher reuses a connection and creates a separate channel per request. It uses a durable queue, persistent messages, mandatory routing, and publisher confirms, with a five-second publish deadline. The API does not return 202 when publishing fails. Publishing is not automatically retried because the broker might have received a message even if its acknowledgement was lost.

**Delivery limitations:** this implementation does not guarantee exactly-once processing. If the broker receives a message but the HTTP response is lost, a client retry can create a duplicate. A legacy consumer should deduplicate by `(partnerId, transactionReference)` in the same database transaction as its business operation, then acknowledge after commit. `MessageId` identifies an individual acceptance attempt; it is not an idempotency key across client retries.

Persistent idempotency tracking could deduplicate client submissions. Accepting transactions while the broker is offline would additionally require durable local storage, such as a transactional outbox with a background publisher. Neither is implemented here: broker failures return 503. A single RabbitMQ node with local storage does not provide high availability.

## HTTP responses

| HTTP status | Meaning |
| --- | --- |
| 202 | The broker confirmed the message; legacy processing is still pending |
| 400 | Invalid JSON or request data |
| 401 | Missing or invalid API key |
| 422 | Partner not found or not verified |
| 503 | Partner API or message broker unavailable |
| 500 | Unexpected server error |

Errors use `application/problem+json` without exposing stack traces. Server logs retain exceptions and trace IDs for investigation. A mock timeout returns 504; exhausted verification retries result in 503 from the transaction endpoint. `/health/live` checks whether the API process is alive, not whether the broker is ready.

## Security

The transaction endpoint requires `X-Api-Key`. The key is read from configuration or environment variables, and its hash is compared in constant time. `.env` is excluded from Git. The local launch profile contains demo credentials only.

This shared-key example does not bind the authenticated client to a specific `partnerId`. Production should use OAuth2 client credentials/JWT or mTLS, authorize access to each partner ID, rotate secrets through a secret manager, terminate HTTPS at a reverse proxy, and apply per-partner rate limits. The API container runs as a non-root user, and the mock endpoint is available only in Development.

## Testing and verification

```powershell
dotnet test PartnerIntegration.sln -c Release --collect:"XPlat Code Coverage"
```

RabbitMQ is not required for these tests. Cobertura reports are written to `tests/PartnerIntegration.Tests/TestResults/<run-id>/coverage.cobertura.xml`; CI uploads them as artifacts.

Recorded local verification (2026-09-10): **78/78 tests passed**, **95.95% line coverage (190/198)**, and **82.95% branch coverage** across all four production assemblies. Tests ran on .NET 8 using SDK 10.0.302. Release publishing and Compose configuration validation also passed.

**Verification boundary:** Docker Engine was unavailable locally. Container startup, delivery to a real broker, and restart persistence remain unverified here. The CI workflow defines .NET 8 build/tests and a Docker smoke test; their results should be checked in the repository's Actions tab. Coverage measures the automated tests, not end-to-end broker guarantees.

- Validation tests cover required fields, amounts, supported currencies, and timestamps.
- Resilience tests exercise the real HTTP resilience pipeline with a stub handler producing deterministic failures. The timeout test waits for an actual attempt deadline.
- Endpoint tests use `WebApplicationFactory` with replacement verifier/publisher implementations to check validation, authentication, enrichment, accepted responses, and dependency failures. The mock failure sampler is replaceable so both outcomes can be tested without random failures.
- Publisher tests use Moq at the RabbitMQ client boundary to check durable queues, persistent messages, mandatory routing, publisher confirmation settings, waiting for confirmations, connection reuse/replacement, connection/publish errors, and cancellation.
- Use-case tests check Domain validation without HTTP, partner rejection, enrichment, cancellation, and waiting for confirmation before returning a receipt. Architecture tests enforce assembly dependency boundaries.

Mock-based publisher tests verify how the application uses the client contract; they do not prove broker persistence or network recovery. Those require a real RabbitMQ instance, using either installation method above. Infrastructure is included in coverage measurements.

### Docker smoke test

The Dockerfile restores all four projects before publishing the API. The [`docker-smoke` CI job](.github/workflows/ci.yml) builds and starts Compose, submits a transaction over HTTP, and checks the enriched payload through RabbitMQ Management. It uses a fresh broker and cleans up its own volumes afterward.

To run the same smoke check locally after starting Compose:

```powershell
./scripts/Smoke-Test.ps1 -ApiKey 'local-demo-change-this-key'
```

Use the key configured in `.env`. The script expects the Compose credentials and reads up to 100 queued messages with requeue enabled; use a fresh test queue for a reliable isolated check. It retries HTTP 503 responses to accommodate the random mock failures. It verifies delivery, not restart persistence or exactly-once processing.
