# Partner Integration BFF

.NET 8 Web API nhận giao dịch từ đối tác, xác thực dữ liệu, gọi Partner Verification API để bổ sung tên đối tác, rồi publish vào RabbitMQ. Trả `202 Accepted` chỉ sau khi broker xác nhận message.

## Chạy bằng Docker

Cần Docker Desktop với Linux containers / Docker Compose v2.

```powershell
Copy-Item .env.example .env
# Thay PARTNER_API_KEY trong .env nếu cần.
docker compose up --build -d
docker compose ps
```
API: http://localhost:8080. RabbitMQ Management: http://localhost:15672, tài khoản demo `partner` / `local-demo-password`. Queue `partner.transactions` được tạo ở lần publish đầu tiên. Đây là thông tin demo local, không dùng cho production. Các port chỉ bind vào loopback.

```powershell
$headers = @{ 'X-Api-Key' = 'local-demo-change-this-key' }
$body = @{
    partnerId = 'P-1001'
    transactionReference = 'TXN-99823'
    amount = 250.00
    currency = 'USD'
    timestamp = '2024-05-10T14:30:00Z'
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/v1/partner/transactions -Headers $headers -ContentType 'application/json' -Body $body
```

Trong RabbitMQ Management, mở **Queues and Streams → partner.transactions** để xem số message và payload. Chưa có consumer vì legacy processing nằm ngoài phạm vi bài tập. Sau khi restart broker bằng `docker compose restart rabbitmq`, message chưa consume phải còn trong queue. `docker compose down` giữ volume; thêm `-v` sẽ xóa dữ liệu.

Mock `GET /mock/partners/{partnerId}` chỉ được bật trong Development: mỗi lần gọi độc lập có xác suất 30% ném `TimeoutException`, 70% trả đối tác hợp lệ. Global exception handler chuyển exception thành HTTP 504. Không kỳ vọng đúng 30/70 trên một mẫu nhỏ. BFF gọi mock qua HTTP thật, không gọi trực tiếp method. Với ba lần thử độc lập, xác suất cả ba timeout là 2,7%, nên thỉnh thoảng nhận 503 là hành vi mong đợi. Circuit breaker cũng có thể từ chối sớm khi nhiều lỗi.

## Chạy hoàn toàn trên Windows, không cần Docker

API và RabbitMQ chạy trực tiếp trên Windows; không cần Docker, WSL hoặc bật ảo hóa trong BIOS.

### 1. Cài phần mềm

- Cài .NET 8 SDK. SDK mới hơn có thể build nhờ `global.json`, nhưng chạy vẫn cần .NET 8 runtime.
- Cài Erlang/OTP 64-bit trước, sau đó cài RabbitMQ Server bằng Windows installer theo [hướng dẫn chính thức](https://www.rabbitmq.com/docs/install-windows). Chọn phiên bản Erlang tương thích với RabbitMQ theo [bảng tương thích](https://www.rabbitmq.com/docs/which-erlang).
- Mở `services.msc`, tìm dịch vụ **RabbitMQ**, kiểm tra trạng thái **Running**; chọn **Start** nếu dịch vụ đang dừng.

### 2. Bật giao diện quản lý RabbitMQ

Mở **RabbitMQ Command Prompt (sbin dir)** từ Start Menu bằng **Run as administrator**, chạy:

```cmd
rabbitmq-plugins.bat enable rabbitmq_management
rabbitmq-diagnostics.bat ping
```

Lệnh `ping` phải báo thành công. Mở http://localhost:15672 và đăng nhập `guest` / `guest` cho bản cài mới mặc định. Tài khoản này chỉ dùng kết nối local; bản Docker Compose dùng tài khoản demo khác là `partner` / `local-demo-password`.

### 3. Chạy API

**Cách nhanh: dùng profile local đã cấu hình sẵn.** Sau khi RabbitMQ chạy nền, mở PowerShell tại thư mục repository:

```powershell
cd src/PartnerIntegration.Api
dotnet run
```

API lắng nghe tại `https://localhost:64882` và `http://localhost:64883`. Endpoint giao dịch là **POST https://localhost:64882/api/v1/partner/transactions**, dùng header `X-Api-Key: local-demo-change-this-key`. Profile dùng RabbitMQ local với tài khoản demo `guest` / `guest`; mock Partner API được gọi qua HTTP nội bộ port 64883.

Nếu chạy từ thư mục solution, dùng `dotnet run --project src/PartnerIntegration.Api`. Trong Visual Studio, chọn API làm Startup Project, chọn profile `PartnerIntegration.Api` rồi F5. Visual Studio mở `/health/live` để hiển thị trạng thái API; `dotnet run` không tự mở trình duyệt. Mở URL transactions bằng trình duyệt gửi GET nên trả 405; cần gửi POST bằng Postman hoặc đoạn PowerShell ở bước 4 (đổi URL sang `https://localhost:64882`).

Nếu máy chưa tin cậy chứng chỉ HTTPS local, chạy một lần `dotnet dev-certs https --trust` và chấp nhận hộp thoại Windows, sau đó chạy lại API.

Các giá trị demo được đặt trong `src/PartnerIntegration.Api/Properties/launchSettings.json`, chỉ dùng khi chạy local. Không dùng key hoặc tài khoản demo cho production.

**Cách tùy chỉnh: tự đặt biến môi trường và chạy port 8080.**

Mở PowerShell tại thư mục repository, nơi có file `PartnerIntegration.sln`, rồi chạy toàn bộ đoạn sau trong cùng cửa sổ:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:8080'
$env:Security__ApiKey = 'local-demo-change-this-key'
$env:PartnerApi__BaseUrl = 'http://localhost:8080/'
$env:RabbitMq__Uri = 'amqp://guest:guest@localhost:5672/'

dotnet run --project src/PartnerIntegration.Api --no-launch-profile
```

Giữ cửa sổ này mở. Khi thấy `Now listening on: http://localhost:8080`, API đã khởi động. Các biến môi trường trên chỉ áp dụng cho cửa sổ PowerShell hiện tại; mở cửa sổ mới để chạy API thì cần đặt lại. Cách chạy này không cần file `.env`; `dotnet run` không tự đọc file đó.

### 4. Gửi giao dịch thử

Mở PowerShell thứ hai và chạy. Đoạn dưới dùng port 8080 của cách tùy chỉnh; nếu chạy profile local mặc định, thay `http://localhost:8080` bằng `https://localhost:64882` ở cả hai URL:

```powershell
Invoke-RestMethod -Uri 'http://localhost:8080/health/live'

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

Thành công trả HTTP `202` với `messageId`, `transactionReference` và `status: queued`. Trong RabbitMQ Management, mở **Queues and Streams → partner.transactions** để xem message. Queue được tạo ở lần publish đầu tiên. Nếu dùng **Get messages** để xem payload, chọn chế độ requeue nếu muốn giữ message trong queue.

Mock vẫn có xác suất timeout 30% mỗi lần gọi; nếu hết lượt retry, API trả `503`. Khi gặp `503` liên tục, xem log ở cửa sổ chạy API để phân biệt lỗi Partner API với lỗi kết nối RabbitMQ.

### 5. Chạy test và dừng ứng dụng

Tại thư mục repository:

```powershell
dotnet test PartnerIntegration.sln -c Release --collect:"XPlat Code Coverage"
```

Unit/in-process integration tests không cần API hoặc RabbitMQ đang chạy. Dừng API bằng `Ctrl+C` ở cửa sổ chạy `dotnet run`. Để dừng hoặc khởi động lại RabbitMQ, dùng **Stop** hoặc **Restart** trong `services.msc`. Có thể kiểm tra persistence bằng cách gửi message, restart dịch vụ rồi kiểm tra message chưa consume vẫn còn trong queue.

### Xử lý lỗi thường gặp

| Triệu chứng | Cách kiểm tra |
| --- | --- |
| API báo thiếu `Security:ApiKey` | Đặt các biến `$env:...` trong cùng cửa sổ trước khi chạy `dotnet run` |
| HTTP `401` | Header `X-Api-Key` phải trùng với `Security__ApiKey` |
| HTTP `503` và log báo lỗi broker | Kiểm tra dịch vụ RabbitMQ đang chạy, port 5672 và tài khoản trong `RabbitMq__Uri` |
| Không mở được trang quản lý | Kiểm tra dịch vụ RabbitMQ và đã bật plugin `rabbitmq_management`; trang quản lý dùng port 15672 |
| Port 8080 đang bị dùng | Đổi cả `ASPNETCORE_URLS`, `PartnerApi__BaseUrl` và URL gửi request sang cùng port mới |

Production phải trỏ `PartnerApi__BaseUrl` tới Partner API thật; mock bị tắt. `/health/live` chỉ kiểm tra process còn sống, không khẳng định broker sẵn sàng.

## Kiến trúc và OOP

Một project API tổ chức theo trách nhiệm, một project test; không thêm repository/database vì bài toán chưa cần persistence riêng.

- `Transactions`: DTO có DataAnnotations, contract message và controller điều phối. Amount dùng `decimal`, timestamp dùng `DateTimeOffset`; nullable giúp phân biệt trường bị bỏ sót. `[ApiController]` tự trả 400 trước khi gọi dependency.
- `Partners`: `IPartnerVerifier` và typed `HttpClient`; kiểm tra ID trả về, trạng thái verified và tên đối tác trước khi enrich.
- `Infrastructure`: `ITransactionPublisher` có implementation RabbitMQ, API-key authentication, global exception handler.
- Constructor injection giúp controller phụ thuộc abstraction (DIP); mỗi thành phần có một trách nhiệm (SRP). Không thêm lớp kế thừa hoặc generic abstraction khi chưa có nhu cầu.

Currency được hiểu là **currency được hệ thống hỗ trợ**: USD, EUR, GBP, VND, JPY, SGD, AUD, CAD, CHF, CNY, THB. Đây là allow-list mã ISO 4217 viết hoa, không phải toàn bộ danh sách ISO. Mã ISO ngoài danh sách vẫn bị từ chối; cần thống nhất với nghiệp vụ khi mở rộng. Không áp đặt timestamp phải gần hiện tại, vì đề bài không yêu cầu và payload mẫu là ngày quá khứ.

## Retry và tính tin cậy

`Microsoft.Extensions.Http.Resilience` dùng standard handler: retry tối đa 2 lần (3 attempts tổng cộng), exponential backoff bắt đầu 200 ms với jitter, attempt timeout 2 giây và tổng thời gian 8 giây. Retry lỗi network, 408, 429 và 5xx; không retry lỗi 4xx khác. Có circuit breaker và giới hạn concurrency của standard handler. Retry chỉ dùng cho GET kiểm tra partner. Cancellation của caller được truyền xuống.

RabbitMQ dùng connection dùng lại, channel riêng theo request, durable queue, persistent message, `mandatory` routing và publisher confirms. Publish có deadline 5 giây. Không trả 202 khi publish lỗi. Không tự retry publish vì broker có thể đã nhận message nhưng acknowledgement bị mất.

**Giới hạn delivery:** không đảm bảo exactly-once. Nếu broker nhận message nhưng HTTP response bị mất, client retry có thể tạo bản sao. Legacy consumer nên deduplicate bằng cặp `(partnerId, transactionReference)` trong cùng database transaction với nghiệp vụ và manual ack sau commit. `MessageId` nhận diện một lần tiếp nhận, không phải idempotency key xuyên các lần client retry. Production có thể thêm persistent idempotency store / transactional outbox nếu cần nhận giao dịch trong lúc broker offline. Bài này trả 503 để caller xử lý retry. Single-node RabbitMQ và volume local chưa cung cấp HA.

| HTTP | Ý nghĩa |
| --- | --- |
| 202 | Broker đã xác nhận message |
| 400 | JSON hoặc dữ liệu không hợp lệ |
| 401 | Thiếu/sai API key |
| 422 | Partner không tồn tại hoặc chưa được xác thực |
| 503 | Partner API hoặc broker không khả dụng |
| 500 | Lỗi ngoài dự kiến |

Lỗi dùng `application/problem+json`; lỗi không lộ stack trace cho client. Server log giữ exception và trace ID để điều tra. Mock timeout trả 504; khi BFF dùng hết retries, endpoint giao dịch trả 503.

## Bảo mật

Endpoint yêu cầu `X-Api-Key`; key đọc từ configuration/environment và so sánh hash bằng constant-time comparison. `.env` không commit. Đây là ví dụ shared key cho bài test, chưa ràng buộc danh tính với `partnerId`. Production nên dùng OAuth2 client credentials/JWT hoặc mTLS, kiểm tra quyền trên từng partnerId, rotate secrets qua secret manager, HTTPS tại reverse proxy và rate limit theo đối tác. Container API chạy bằng user không phải root. Mock chỉ dành cho môi trường Development.

## Tests

```powershell
dotnet test PartnerIntegration.sln -c Release --collect:"XPlat Code Coverage"
```

Không cần RabbitMQ để chạy unit/in-process integration tests. Coverage Cobertura nằm dưới `tests/PartnerIntegration.Tests/TestResults/<run-id>/coverage.cobertura.xml`; CI upload thành artifact.

Kết quả kiểm chứng tại workspace: **63/63 tests passed**, line coverage **95,36% (144/151)**, branch coverage **79,68%**. Build/test target `net8.0`, dùng SDK 10.0.302 và runtime .NET 8 có sẵn. Docker Desktop không khởi động được trên máy kiểm thử nên chưa chạy Compose; cũng chưa xác nhận end-to-end với broker thật cài trực tiếp trên Windows.

Tests kiểm tra từng field required, amount, supported currency, timestamp; resilience dùng **pipeline thật** và HTTP handler giả để ép chuỗi lỗi deterministic; test timeout dùng request thực sự chờ đến deadline. Endpoint tests dùng `WebApplicationFactory`, thay verifier/publisher để kiểm tra validation, authentication, enrichment, accepted response và dependency errors. Mock sampler có thể thay thế để kiểm tra cả nhánh thành công/timeout mà không có flaky random tests.

Publisher tests dùng Moq ở boundary RabbitMQ client: kiểm tra durable queue, persistent message, mandatory routing, bật publisher confirms, chờ confirmation, tái sử dụng/thay connection, lỗi connection/publish và cancellation. Các test này kiểm tra code gọi client đúng contract; chúng không thay thế bài test tích hợp với broker thật.

Publisher thật và broker confirms cần kiểm chứng bằng RabbitMQ chạy qua Docker hoặc cài trực tiếp trên Windows theo các bước ở trên; in-process tests không chứng minh RabbitMQ persistence hoặc network recovery. Không loại bỏ infrastructure khỏi coverage để nâng số liệu.

## Nộp bài

Tạo public repository trên GitHub/GitLab, sau đó ở thư mục này chạy:

```powershell
git init
git add .
git commit -m "Implement partner integration BFF"
git branch -M main
git remote add origin https://github.com/changtotbung1105/SSTECH
git push -u origin main
```

## Tài liệu tham khảo

- [Microsoft: HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [RabbitMQ: reliable publishing with confirms](https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet)
- [RabbitMQ: .NET client guide](https://www.rabbitmq.com/client-libraries/dotnet-api-guide)
