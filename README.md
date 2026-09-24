# JobFree Backend Platform

Hệ thống Backend cho nền tảng **JobFree** được xây dựng trên nền tảng **.NET 8** sử dụng kiến trúc **Clean Architecture**, tích hợp hạ tầng hiện đại gồm PostgreSQL (PostGIS), Redis, RabbitMQ, S3/MinIO Object Storage, và hệ thống giám sát OpenTelemetry (Prometheus + Grafana).

---

## Yêu Cầu Tiền Đề (Prerequisites)

Trước khi bắt đầu, hãy đảm bảo máy tính của bạn đã cài đặt các công cụ sau:

- **[Docker Desktop](https://www.docker.com/products/docker-desktop/)** đang chạy Linux containers, hoặc Docker Engine + Docker Compose hỗ trợ [`configs.content`](https://docs.docker.com/reference/compose-file/configs/) (Compose 2.23.1 trở lên).
- **[Git](https://git-scm.com/)**

Chạy bằng Docker không cần cài .NET SDK trên máy: Dockerfile tự restore, build và publish backend .NET 8. Chỉ cần SDK theo `global.json` khi phát triển hoặc chạy test ngoài container.

---

## Hướng Dẫn Khởi Chạy Dự Án (Quickstart Guide)

Sau khi **clone** dự án về máy cục bộ, bạn hãy thực hiện lần lượt các bước sau:

### Bước 1: Di chuyển vào thư mục dự án

```bash
cd JobFree
```

---

### Bước 2: Tạo và cấu hình file môi trường `.env`

Tạo file `.env` từ file mẫu `.env.example`:

- **Trên Windows (PowerShell):**
  ```powershell
  Copy-Item .env.example .env
  ```
- **Trên Linux / macOS (Bash):**
  ```bash
  cp .env.example .env
  ```

File mẫu có đủ giá trị để chạy thử local. Có thể thay tài khoản và mật khẩu trước lần khởi động đầu tiên (ví dụ):

```env
POSTGRES_DB=jobfree_db
POSTGRES_USER=jobfree_user
POSTGRES_PASSWORD=YourSecurePassword123!
REDIS_PASSWORD=YourRedisPassword123!
RABBITMQ_USER=jobfree_rabbit
RABBITMQ_PASSWORD=YourRabbitPassword123!
MINIO_ROOT_USER=minio_admin
MINIO_ROOT_PASSWORD=YourMinioPassword123!
S3_REGION=us-east-1
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=YourGrafanaPassword123!
PGADMIN_DEFAULT_EMAIL=admin@jobfree.com
PGADMIN_DEFAULT_PASSWORD=YourPgAdminPassword123!
```

---

### Bước 3: Build và chạy backend cùng toàn bộ hạ tầng

Chạy tại thư mục chứa `docker-compose.yml`:

```bash
docker compose up -d --build --wait --wait-timeout 180
```

> **Danh sách các dịch vụ & cổng truy cập cục bộ (Local Ports):**
> - **Backend API / Swagger**: `http://127.0.0.1:8080/swagger`
> - **PostgreSQL (PostGIS)**: `127.0.0.1:15432`
> - **Redis Cache**: `127.0.0.1:6379`
> - **pgAdmin**: `http://127.0.0.1:5050` — [Hướng dẫn đăng nhập và xem dữ liệu](docs/pgadmin.md)
> - **RabbitMQ Dashboard**: `http://localhost:15672` *(Đăng nhập bằng `RABBITMQ_USER` / `RABBITMQ_PASSWORD` trong `.env`)*
> - **MinIO Console (S3 UI)**: `http://localhost:9001` *(Đăng nhập bằng `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`)*
> - **Prometheus Metrics**: `http://localhost:9090`
> - **Grafana Dashboards**: `http://localhost:3000` *(Đăng nhập bằng `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD`)*

---

### Bước 4: Mở backend và kiểm tra container

Backend đã chạy trong service `api` (container `jobfree-platform`). Docker Compose build từ mã nguồn và đợi PostgreSQL, Redis, RabbitMQ, MinIO healthy trước khi khởi động API. Lần đầu cần mạng để tải image và NuGet packages; thời gian build có thể lâu hơn các lần sau.

```bash
docker compose ps
docker compose logs --tail=100 api
```

Mở `http://127.0.0.1:8080/swagger`. Nếu thay `API_PORT` trong `.env`, dùng cổng tương ứng. Không cần chạy thêm `dotnet run`. Backend hiện là scaffold, chưa có endpoint nghiệp vụ hoặc migration tạo bảng nghiệp vụ.

---

### Bước 5: Kiểm tra các Endpoints Vận Hành (Health Checks & Metrics)

Sau khi ứng dụng khởi chạy thành công, bạn có thể kiểm tra trạng thái hoạt động:

1. **Liveness Check** (Kiểm tra ứng dụng đang sống):
   ```bash
   curl http://localhost:8080/health/live
   ```
   *(Trả về HTTP `200 OK`)*

2. **Readiness Check** (Kiểm tra ứng dụng đã sẵn sàng xử lý request & kết nối hạ tầng thành công):
   ```bash
   curl http://localhost:8080/health/ready
   ```
   *(Trả về JSON trạng thái kết nối Database, Cache, Storage)*

3. **Swagger UI (Giao diện Test API)**:
   Mở trình duyệt và truy cập: `http://localhost:8080/swagger` để xem và chạy thử (Test) các API tương tác trực tiếp trên giao diện Swagger.

4. **Prometheus Metrics Endpoint**:
   ```bash
   curl http://localhost:8080/metrics
   ```

---

### Chạy Unit & Integration Tests (dành cho phát triển, cần .NET SDK)

Đảm bảo tất cả các bài kiểm thử đều vượt qua trước khi viết tính năng mới hoặc tạo Pull Request:

```bash
dotnet test
```

---

## Cập nhật mã nguồn và dừng ứng dụng

Sau khi tải thay đổi mã nguồn, build lại và chạy:

```bash
docker compose up -d --build --wait --wait-timeout 180
```

Để dừng và dọn dẹp các container:

```bash
docker compose down
```

Lệnh trên giữ các named volumes chứa dữ liệu. Cấu hình Compose này dành cho local development. Khi database hoặc pgAdmin đã khởi tạo, thay biến mật khẩu trong `.env` không tự đổi mật khẩu tài khoản đang lưu trong volume.

---

## Tài Liệu Kiến Trúc & Cấu Trúc Dự Án

Chi tiết về cấu trúc các thư mục, vai trò của từng tầng (Layer) và tác dụng của từng Class trong hệ thống được trình bày tại:
**[docs/project-structure.md](docs/project-structure.md)**

---

## Quy Định Đóng Góp (Contribution Guidelines)

1. Mọi code thay đổi cần tuân thủ theo kiến trúc **Clean Architecture**.
2. Thêm summary comment (`/// <summary>`) giải thích ngắn gọn cho các Class và Interface mới tạo.
3. Luôn chạy `dotnet test` để xác nhận không gây ảnh hưởng tới các tính năng hiện có trước khi gửi code.
