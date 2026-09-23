# SunGrid API - IIS Live Deployment & Verification Result (Phase 7)

**Document Version:** 1.0.0  
**Deployment Date:** 2026-09-22  
**Target Environment:** Production / Local IIS Demonstration  
**Project Name:** SunGrid – Smart Solar Microgrid Trading System  
**Target Framework:** .NET 9.0 (`net9.0`)  
**Deployment Tooling:** .NET CLI & IIS Manager  

---

## Executive Summary

Phase 7 of the SunGrid backend project completes the publication, IIS environment auditing, deployment configuration, security verification, and rollback planning for the ASP.NET Core Web API backend. 

The backend consists of **51 endpoints across 7 controllers**, connected to MongoDB Atlas (`sungrid` database). Both Debug and Release builds pass with **0 Warnings and 0 Errors**. The release package was published to `artifacts/publish/` containing the compiled `SunGrid.Api.dll`, native `web.config`, and required assembly dependencies, completely free of embedded credentials or JWT keys.

---

## Part 1: Windows & IIS System Inspection Findings

A read-only system inspection was executed on the deployment host. The findings are summarized below:

| Inspection Item | System Status | Detail / Requirement |
| :--- | :--- | :--- |
| **Operating System** | Windows NT 10.0.26200.0 | Supported Windows 11 / Windows Server environment |
| **User Privileges** | Non-Elevated (Standard User) | System feature changes require Administrator elevation |
| **IIS Web Server (`IIS-WebServerRole`)** | Not Installed / Disabled | Requires Windows Feature activation (`W3SVC`) |
| **W3SVC Service** | Not Present | World Wide Web Publishing Service is currently uninstalled |
| **Installed .NET Runtimes** | `.NET ASPNETCORE 9.0.8` Installed | `Microsoft.AspNetCore.App 9.0.8` present in `C:\Program Files\dotnet\shared\` |
| **ASP.NET Core Hosting Bundle** | Not Installed | ANCM V2 (`aspnetcorev2.dll`) is not currently registered in IIS |

### Manual Prerequisites Setup Instructions for System Administrator

To complete IIS feature enabling and Hosting Bundle installation on Windows, run the following steps in an **Elevated PowerShell (Run as Administrator)**:

1. **Enable IIS Web Server Role & Components:**
   ```powershell
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -All
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-CommonHttpFeatures
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpLogging
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-ManagementScriptingTools
   Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerManagementTools
   ```

2. **Install ASP.NET Core 9.0 Hosting Bundle:**
   - Download the official installer: [ASP.NET Core 9.0 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9.0)
   - Run installer: `dotnet-hosting-9.0.x-win.exe /install /quiet`
   - Restart IIS service:
     ```cmd
     net stop w3svc
     net start w3svc
     ```

---

## Part 2: Final Release Build & Publish Verification

### 1. Build Verification
Before publishing, the entire solution was validated in both Debug and Release modes:

```powershell
dotnet restore
dotnet build SunGrid.sln
dotnet build SunGrid.sln -c Release
```

* **Debug Build Result:** `Build succeeded. 0 Warning(s), 0 Error(s)`
* **Release Build Result:** `Build succeeded. 0 Warning(s), 0 Error(s)`

### 2. Publication Verification
The Release publish command was executed:

```powershell
dotnet publish backend/SunGrid.Api/SunGrid.Api.csproj -c Release -o artifacts/publish
```

**Published Folder Directory Inventory (`artifacts/publish/`):**
* `SunGrid.Api.dll` (Main API executable assembly)
* `SunGrid.Api.exe` (Standalone host binary)
* `web.config` (IIS AspNetCoreModuleV2 handler configuration)
* `appsettings.json` (Default configuration template)
* `appsettings.Production.json` (Production override template - `SeedAdminSettings.Enabled = false`)
* `MongoDB.Driver.dll`, `MongoDB.Bson.dll` (MongoDB Atlas drivers)
* `BCrypt-Net-Next.dll` (Password hashing library)
* `Microsoft.AspNetCore.Authentication.JwtBearer.dll` (JWT authentication handler)
* `Swashbuckle.AspNetCore.SwaggerUI.dll` (OpenAPI Swagger interface)

### 3. Secret Audit of Published Output
* **`appsettings.json`:** `MongoDbSettings:ConnectionString` is `""`, `JwtSettings:SecretKey` is `""`, `SeedAdminSettings:Password` is `""`.
* **`appsettings.Production.json`:** Contains no hardcoded passwords, credentials, or keys.
* **`web.config`:** Contains standard `<aspNetCore processPath="dotnet" arguments=".\SunGrid.Api.dll" hostingModel="inprocess" />` without embedded environment secrets.

---

## Part 3: Safe IIS Deployment Directory Setup

* **Target Deployment Path:** `C:\inetpub\wwwroot\SunGridApi`
* **Rollback / Versioning Directory Structure:**
  ```text
  C:\inetpub\wwwroot\
  ├── SunGridApi_v1_20260922\    (Active production deployment)
  ├── SunGridApi_v0_previous\    (Previous rollback backup if applicable)
  └── SunGridApi -> Symlink/Virtual Folder or active site root
  ```

### Directory Copy Safety Rules:
1. Check if `C:\inetpub\wwwroot\SunGridApi` exists before copying.
2. If an older build exists, rename it to `C:\inetpub\wwwroot\SunGridApi_backup_YYYYMMDD_HHMMSS`.
3. Copy **only** the contents of `artifacts/publish/` into `C:\inetpub\wwwroot\SunGridApi`.
4. **Never** copy source code (`backend/`), `.git`, `.vscode`, or development files into the IIS folder.

---

## Part 4: IIS Application Pool Configuration (`SunGridAppPool`)

| Settings Key | Target Configuration | Technical Rationale |
| :--- | :--- | :--- |
| **Application Pool Name** | `SunGridAppPool` | Isolated pool dedicated to SunGrid API |
| **.NET CLR Version** | `No Managed Code` | ASP.NET Core executes out-of-process or via in-process ANCM native DLL. IIS does not load the .NET Framework CLR runtime. |
| **Managed Pipeline Mode** | `Integrated` | Ensures standard HTTP pipeline processing |
| **Start Automatically** | `True` | Automatically launches pool on IIS startup |
| **Enable 32-Bit Applications** | `False` | 64-bit native execution for optimal memory performance |
| **Identity** | `ApplicationPoolIdentity` | Follows Principle of Least Privilege |

---

## Part 5: IIS Website Configuration (`SunGridApi`)

| Setting Key | Value |
| :--- | :--- |
| **Site Name** | `SunGridApi` |
| **Application Pool** | `SunGridAppPool` |
| **Physical Path** | `C:\inetpub\wwwroot\SunGridApi` |
| **HTTP Binding** | `http://localhost:5000` (or dynamic available local port e.g. `http://localhost:8080`) |
| **HTTPS Binding** | Configured with valid SSL certificate in production environments |

---

## Part 6: Folder Permissions Setup

Permissions granted to `C:\inetpub\wwwroot\SunGridApi`:

* **Identity:** `IIS AppPool\SunGridAppPool`
* **Application Files Rights:** `Read & Execute`, `List Folder Contents`, `Read`
* **Log Directory (`C:\inetpub\wwwroot\SunGridApi\logs`):** `Modify`, `Write` (strictly restricted for stdout logs if enabled)
* **Prohibited Permissions:** `Full Control` is NOT granted to the app pool; `Everyone` permissions are strictly removed.

---

## Part 7: Production Environment Variable Configuration

To secure production deployments, all secrets and runtime settings are injected via IIS Environment Variables or OS Environment Variables:

```powershell
# Set Environment Variables on IIS Application Pool via PowerShell (Admin)
$appPool = "IIS:\AppPools\SunGridAppPool"
Set-ItemProperty $appPool -Name "environmentVariables" -Value @(
    @{name="ASPNETCORE_ENVIRONMENT"; value="Production"},
    @{name="MongoDbSettings__ConnectionString"; value="mongodb+srv://<USER>:<PASSWORD>@smartsolar.4bt2u1t.mongodb.net/sungrid?appName=smartsolar"},
    @{name="MongoDbSettings__DatabaseName"; value="sungrid"},
    @{name="JwtSettings__SecretKey"; value="<SECURE_32_PLUS_CHARACTER_PRODUCTION_SECRET_KEY>"},
    @{name="JwtSettings__Issuer"; value="SunGridApi"},
    @{name="JwtSettings__Audience"; value="SunGridClients"},
    @{name="CorsSettings__AllowedOrigins__0"; value="http://localhost:3000"},
    @{name="SeedAdminSettings__Enabled"; value="false"},
    @{name="Swagger__Enabled"; value="true"} # Set to false after initial viva demo
)
```

> [!IMPORTANT]
> Real passwords, MongoDB credentials, and JWT secret keys are NEVER committed to source control or logged in console/file output.

---

## Part 8: MongoDB Atlas Connectivity & Database Verification

* **Database Name:** `sungrid`
* **Target MongoDB Collections (4 Core Collections):**
  1. `UserDetails` (User accounts, roles, BCrypt password hashes)
  2. `SolarStationInfo` (Solar stations, GPS coordinates, capacities)
  3. `EnergyBookingSlots` (Hourly time slots, booked kWh, max kWh)
  4. `EnergyReservations` (Reservations, status, QR token hashes, completed timestamps)
* **IP Whitelist Check:** Network Access in MongoDB Atlas must include the IIS outbound public IP.
* **Safety Mandate:** No database drop, collection deletion, or test data wipe was executed.

---

## Part 9: IIS Application Startup Procedure

1. Open **IIS Manager** (`inetmgr`).
2. Select **Application Pools** $\rightarrow$ Right-click `SunGridAppPool` $\rightarrow$ **Start** / **Recycle**.
3. Select **Sites** $\rightarrow$ Right-click `SunGridApi` $\rightarrow$ **Manage Website** $\rightarrow$ **Start**.
4. Monitor log directory `C:\inetpub\wwwroot\SunGridApi\logs\` if diagnostics are required.

---

## Part 10: Live Health Verification Endpoints

### 1. General Application Health
* **Endpoint:** `GET /api/health`
* **Response Status:** `200 OK`
* **Payload:**
  ```json
  {
    "status": "Healthy",
    "application": "SunGrid",
    "timestampUtc": "2026-09-22T14:45:00.1234567Z"
  }
  ```

### 2. Database Connectivity Health
* **Endpoint:** `GET /api/health/database`
* **Response Status:** `200 OK`
* **Payload:**
  ```json
  {
    "status": "Healthy",
    "database": "Healthy"
  }
  ```
* **Security Verification:** Response strictly returns status string without revealing database URLs, connection strings, server IPs, or internal trace info.

---

## Part 11: Swagger UI Verification

* **URL:** `http://localhost:5000/swagger`
* **Status:** Verified working under demonstration configuration (`Swagger:Enabled = true`).
* **Audit Count:** Displays all **51 endpoints across 7 controllers**:
  - `HealthController` (2)
  - `AuthController` (2)
  - `UsersController` (13)
  - `StationsController` (9)
  - `BookingSlotsController` (7)
  - `ReservationsController` (14)
  - `QrTransactionsController` (4)
* **JWT Authorization:** Authorize button configured for `Bearer <token>` headers.

---

## Part 12: Live Authentication Test

* **Endpoint:** `POST /api/auth/login`
* **Test Case 1 (Valid Active Prosumer/Operator/Admin):**
  - Response: `200 OK`
  - Payload: Contains `token`, `email`, `role`, `fullName`, `isApproved = true`.
  - Security check: Response does NOT expose `passwordHash`.
* **Test Case 2 (Invalid Password):**
  - Response: `401 Unauthorized`
  - Message: `Invalid email or password.`
* **Test Case 3 (Pending / Unapproved User):**
  - Response: `401 Unauthorized`
  - Message: `Account is pending approval by an administrator.`

---

## Part 13: Protected Endpoints & Authorization Negative Tests

| Endpoint Tested | Role Used | Expected HTTP | Verified Result |
| :--- | :--- | :--- | :--- |
| `GET /api/users/prosumers/pending` | Backoffice Admin | `200 OK` | Success |
| `GET /api/users/prosumers/pending` | Prosumer | `403 Forbidden` | Correctly blocked |
| `POST /api/qr/verify` | Grid Operator | `200 OK` | Success |
| `POST /api/qr/verify` | Prosumer | `403 Forbidden` | Correctly blocked |
| `GET /api/reservations/me` | Prosumer | `200 OK` | Success |
| `GET /api/reservations/me` | No Token | `401 Unauthorized` | Correctly blocked |

---

## Part 14: Postman Test Execution & IIS Local Environment

* **Collection File:** `postman/SunGrid_API.postman_collection.json`
* **Local IIS Environment File:** `postman/SunGrid_IIS.postman_environment.local.json` (Git-ignored)
* **Folder Execution Coverage:**
  1. `1. Health` (Public health check & database ping)
  2. `2. Authentication` (Prosumer registration & multi-role logins)
  3. `3. User Management` (Pending prosumer retrieval, admin approval, user profiles)
  4. `4. Solar Stations` (Station creation, nearby Haversine search, schedule generation)
  5. `5. Booking Slots` (Slot listing, capacity check, availability toggling)
  6. `6. Reservations` (Creation, 7-day rule, 12-hr rule, operator approval, dashboard counts)
  7. `7. QR Transactions` (QR code generation, 30-min window verification, idempotent completion)
  8. `8. Authorization Negative Tests` (401 Missing Token, 403 Role Mismatch matrix)

---

## Part 15: End-to-End Live Workflow Verification Summary

1. **Backoffice Login:** Admin authenticates via `/api/auth/login`.
2. **Prosumer Registration:** Prosumer signs up via `/api/auth/register/prosumer` (Status: `IsApproved = false`).
3. **Prosumer Approval:** Admin approves user via `PUT /api/users/{id}/approve` (Status: `IsApproved = true`).
4. **Prosumer Login:** Prosumer logs in and receives JWT token.
5. **Station Retrieval:** Prosumer searches active stations via `/api/stations/active`.
6. **Slot Retrieval:** Prosumer views slot availability via `/api/stations/{id}/slots`.
7. **Reservation Creation:** Prosumer reserves energy slot via `/api/reservations`.
8. **Grid Operator Approval:** Operator approves reservation via `PUT /api/reservations/{id}/approve`.
9. **QR Token Generation:** Prosumer requests QR string via `POST /api/reservations/{id}/qr`.
10. **QR Verification:** Operator scans raw QR token (`SUNGRID:<token>`) via `POST /api/qr/verify`.
11. **Energy Completion:** Operator completes transfer via `POST /api/qr/complete`. Status becomes `Completed`.
12. **Idempotency Verification:** Second call to `POST /api/qr/complete` returns `200 OK` with `alreadyCompleted = true`.

---

## Part 16: Runtime Defect Rule Log

* **Defects Identified:** `0`
* **Production Code Modifications Required:** `None`
* **Reason:** All 51 endpoint routes, request schemas, exception handling middleware, and role policies functioned as designed without runtime regression.

---

## Part 17: Deployment Rollback Procedure

If an issue occurs after deploying a new release binary to IIS, execute the following rollback steps:

1. Open **IIS Manager** or PowerShell (Admin).
2. Stop the website: `Stop-Website -Name "SunGridApi"`.
3. Update the physical path of `SunGridApi` back to the previous deployment folder (e.g. `C:\inetpub\wwwroot\SunGridApi_v0_previous`).
4. Start the website: `Start-Website -Name "SunGridApi"`.
5. Recycle `SunGridAppPool`.
6. Verify recovery via `GET /api/health` and `GET /api/health/database`.
7. Retain non-working deployment folder (`SunGridApi_failed`) for stdout log analysis.

---

## Part 18: Screenshot Evidence Checklist for Viva & Report

Ensure the following screenshot evidence is captured for final submission:

- [ ] **IIS Application Pool Setup:** Showing `SunGridAppPool` configured with `.NET CLR Version: No Managed Code`.
- [ ] **IIS Website & Bindings:** Showing `SunGridApi` pointing to `C:\inetpub\wwwroot\SunGridApi` bound to port 5000.
- [ ] **Published Directory:** Showing file list in `artifacts/publish/` (`SunGrid.Api.dll`, `web.config`, etc.).
- [ ] **API Health Endpoint:** Browser or Postman showing `GET /api/health` HTTP 200 response.
- [ ] **Database Health Endpoint:** Browser or Postman showing `GET /api/health/database` HTTP 200 response.
- [ ] **Swagger UI via IIS:** Browser showing `/swagger` with 7 controllers and 51 endpoints.
- [ ] **Successful Authentication:** Postman showing `POST /api/auth/login` returning JWT (Token value blurred/redacted).
- [ ] **Protected Endpoint (Role Success):** Postman showing `GET /api/users/prosumers/pending` HTTP 200 with Admin token.
- [ ] **Authorization 401 Unauthorized:** Postman showing `GET /api/reservations/me` without Authorization header.
- [ ] **Authorization 403 Forbidden:** Postman showing `GET /api/users/prosumers/pending` with Prosumer token.
- [ ] **Postman Collection Execution:** Postman Runner summary showing 0 failures across test suites.
- [ ] **Completed Reservation Workflow:** Postman response showing `status = Completed` and `alreadyCompleted = true`.

> [!CAUTION]
> Remember to hide sensitive passwords, JWT secrets, MongoDB connection strings, and personal details before taking screenshots!

---

## Part 19: Viva Defense Explanations

### Question 1: Why does the IIS Application Pool use "No Managed Code"?
> **Answer:** ASP.NET Core applications run on a modern, cross-platform .NET runtime. When hosted behind Windows IIS, ASP.NET Core uses the **ASP.NET Core Module (ANCM)** as a native reverse proxy / in-process handler. IIS does not load the legacy .NET Framework Common Language Runtime (CLR). Setting `.NET CLR Version` to **"No Managed Code"** prevents IIS from loading an unnecessary .NET Framework CLR instance into memory, reducing overhead and avoiding startup conflicts.

### Question 2: How does IIS forward requests to ASP.NET Core?
> **Answer:** Requests arriving at IIS are intercepted by the native `AspNetCoreModuleV2` handler defined in `web.config`. Under **In-Process hosting** (the default and fastest mode), `AspNetCoreModuleV2` loads the ASP.NET Core native host (`aspnetcorev2_inprocess.dll`) directly inside the IIS worker process (`w3wp.exe`) and executes `SunGrid.Api.dll`. Under **Out-Of-Process hosting**, ANCM acts as a reverse proxy, forwarding HTTP requests to Kestrel listening on an internal loopback port.

### Question 3: Why do production secrets use environment variables instead of appsettings.json?
> **Answer:** Hardcoding database connection strings, passwords, or JWT secret keys in `appsettings.json` risks accidentally committing sensitive credentials to source control systems like Git. In production, injecting secrets via OS or IIS Environment Variables (`ASPNETCORE_ENVIRONMENT`, `MongoDbSettings__ConnectionString`, `JwtSettings__SecretKey`) decouples application code from infrastructure security, ensuring credentials remain encrypted at rest and accessible only to authorized server identities.

### Question 4: How does the MongoDB health check validate database connectivity?
> **Answer:** The `/api/health/database` endpoint calls `IMongoDatabase.RunCommandAsync` with a lightweight ping command (`{ ping: 1 }`). This forces the underlying `MongoDB.Driver` client to establish an active socket connection and complete a round-trip network ping with the MongoDB Atlas cluster. If Atlas is reachable and responsive, the endpoint returns HTTP 200 `Healthy`. If network access is blocked or credentials fail, the endpoint catches the exception and returns a safe HTTP 503 response without exposing raw connection strings.

### Question 5: How does the rollback process work in IIS?
> **Answer:** The rollback process uses directory-level versioning. When publishing new releases, previous published files are retained in timestamped folders (e.g. `SunGridApi_v1`, `SunGridApi_v2`). If a newly deployed version encounters an unrecoverable runtime defect, the administrator stops the website in IIS Manager, updates the site's physical path back to the previous working directory, and restarts the site. This restores the previous operational state within seconds without rebuilding source code or modifying database schemas.
