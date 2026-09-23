# SunGrid – Smart Solar Microgrid Trading System Backend (Phase 1, Phase 2, Phase 3 & Phase 4)

## Project Purpose
SunGrid is a Smart Solar Microgrid Trading System backend API designed to manage user authentication, prosumer onboarding, solar microgrid station management, energy booking slots, energy reservation lifecycle management, secure QR transaction verification, and live operation dashboards.

The API serves as a unified backend for both:
1. **React Web Application** (Backoffice operations, station management, reservation approvals, and monitoring)
2. **Native Android Mobile Application** (Prosumer profile, station search, slot booking, QR display, and Grid Operator QR scanning/completion)

---

## Architecture & Design
The system adheres to a simple, highly maintainable **FAT Service Pattern**:
`Controllers` → `Services` → `MongoDB Context` → `MongoDB Collections`

- **Controllers**: Handle HTTP routing, role authorization attributes, input validation, and HTTP response formatting.
- **Services**: Contain all domain business rules, credential hashing, Haversine GPS calculations, slot overlap checks, 7-day rule, 12-hour rule, QR cryptography/hashing, completion windows, and status state transitions.
- **MongoDB Context**: Provides access to `UserDetails`, `SolarStationInfo`, `EnergyBookingSlots`, and `EnergyReservations` collections and initializes database indexes.
- **Models**: BSON-attributed document representations for MongoDB.
- **DTOs**: Enforce strict data transfer boundaries and prevent leaking sensitive internal data (e.g. `QrTokenHash` is never exposed).

---

## Technology Stack
- **Language**: C# (.NET 9.0 SDK)
- **Framework**: ASP.NET Core Web API with Controllers
- **Database**: MongoDB (Official `MongoDB.Driver`)
- **Authentication**: JWT Bearer Tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Password & QR Security**: BCrypt password hashing (`BCrypt.Net-Next`) and built-in .NET Cryptography (`RandomNumberGenerator`, `SHA256`)
- **Documentation**: Swagger / OpenAPI (`Swashbuckle.AspNetCore`)

---

## MongoDB Collections & Schema Overview

### 1. `UserDetails`
Stores user account documents (`Backoffice`, `GridOperator`, `Prosumer`). Includes `PasswordHash`, `Role`, `AccountStatus`, `Nic`, and UTC timestamps.

### 2. `SolarStationInfo`
Stores solar microgrid station records (`StationCode`, `Name`, `Address`, `Latitude`, `Longitude`, `CapacityKwh`, `TotalBatteryStorageSlots`, `OperatingSchedule`, `Status`).

### 3. `EnergyBookingSlots`
Stores time-based energy booking slots (`StationId`, `StartTimeUtc`, `EndTimeUtc`, `TotalCapacity`, `AvailableCapacity`, `Status`).

### 4. `EnergyReservations`
Stores energy reservation documents directly extending existing documents with QR & completion fields:
- `ReservationReference` (Unique reference e.g., `RES-20260921-A1B2C3`)
- `ProsumerId`, `StationId`, `BookingSlotId`
- `TransferType` (`EnergyDropOff`, `Charging`)
- `EnergyAmountKwh` (> 0)
- `Status` (`Pending`, `Approved`, `Rejected`, `Cancelled`, `Completed`)
- `Notes`, `RejectionReason`, `CancellationReason`
- `QrTokenHash` (Nullable SHA-256 hash string of raw token; unique sparse index `UX_Reservation_QrTokenHash_Sparse`)
- `QrIssuedAtUtc`, `QrExpiresAtUtc`, `QrUsedAtUtc`, `QrRevokedAtUtc` (Nullable UTC timestamps)
- `CompletedByUserId`, `ActualEnergyAmountKwh`, `CompletionNotes`, `CompletedAtUtc` (Nullable completion audit fields)
- Audit user IDs and UTC timestamps (`CreatedAtUtc`, `UpdatedAtUtc`, `ApprovedAtUtc`, `RejectedAtUtc`, `CancelledAtUtc`)

---

## Enums & Status Explanations

### `ReservationStatus`
- `Pending`: Newly created reservation awaiting staff review. Decrements 1 capacity unit from slot.
- `Approved`: Staff-approved reservation. Capacity remains held. Eligible for Prosumer QR token generation.
- `Rejected`: Staff-rejected reservation. Requires reason. Releases 1 capacity unit back to slot and revokes active QR.
- `Cancelled`: Soft-cancelled reservation by Prosumer or Staff. Releases 1 capacity unit back to slot and revokes active QR.
- `Completed`: Final state set when Grid Operator scans valid QR inside completion window and completes energy transfer. Slot capacity is NOT released.

### `EnergyTransferType`
- `EnergyDropOff`: Prosumer depositing solar energy into the microgrid station battery storage.
- `Charging`: Prosumer drawing energy from the microgrid station to charge a vehicle/battery.

---

## Phase 4 QR Transaction & Completion Design

### 1. QR Payload Format
The backend generates a 32-byte cryptographically secure random token using `RandomNumberGenerator.GetBytes(32)` and returns string text formatted as:
```text
SUNGRID:<64-character-hex-random-token>
```
The QR payload contains **zero PII** (no name, NIC, email, reservation ID, station ID, or JWT).

### 2. Token Hashing & Storage
- Only the **SHA-256 hash** of the raw token (`ComputeSha256Hash`) is stored in `QrTokenHash`.
- The raw token is returned **only once** in the `GenerateQrResponse` to the authorized Prosumer.
- Raw QR payloads and token hashes are **never logged** in application logs.
- A sparse unique MongoDB index (`UX_Reservation_QrTokenHash_Sparse`) prevents duplicate token hashes while allowing documents without QR tokens.

### 3. QR Regeneration Rule
When a Prosumer requests a new QR token for the same reservation, a completely new token is generated, replacing the previous `QrTokenHash`. The old QR becomes invalid immediately.

### 4. Automatic Revocation Hooks
Active QR tokens (`QrRevokedAtUtc = nowUtc`) are automatically revoked when:
- An `Approved` reservation is edited and returns to `Pending`.
- A reservation is cancelled.
- A reservation is rejected.
- Slot migration occurs.
- The reservation reaches `Completed` status (`QrUsedAtUtc = nowUtc`).

### 5. Completion Window Rule
Grid Operators can verify and complete an energy transfer starting **30 minutes before Slot StartTimeUtc** until **Slot EndTimeUtc**:
$$\text{WindowStartsUtc} = \text{Slot.StartTimeUtc} - 30\text{ minutes}$$
$$\text{WindowEndsUtc} = \text{Slot.EndTimeUtc}$$

### 6. Atomic Idempotent Transfer Completion
Completion uses an atomic MongoDB conditional update (`FindOneAndUpdateAsync`):
- Checks matching `QrTokenHash`, `Status == Approved`, `QrUsedAtUtc == null`, and `QrRevokedAtUtc == null`.
- Sets `Status = Completed`, `ActualEnergyAmountKwh`, `CompletedByUserId`, `CompletedAtUtc`, and `QrUsedAtUtc`.
- If the same QR payload is scanned again after successful completion, the API returns **HTTP 200 OK** with `AlreadyCompleted = true` without modifying database state or slot capacity.

### 7. Slot Capacity Rule on Completion
Slot capacity is **NOT released** when a reservation reaches `Completed`. Capacity was reserved when `Pending` was created. Completion simply records that the reserved physical transfer took place.

---

## Implemented API Endpoints

### System Health
- `GET /api/health` — Public health status check.

### Authentication & Profile (`/api/auth`, `/api/users/me`)
- `POST /api/auth/register/prosumer` — Public Prosumer self-registration (`Pending`).
- `POST /api/auth/login` — Public login endpoint (`Active` users only). Returns JWT token.
- `GET /api/users/me` — Get current user profile.
- `PUT /api/users/me` — Update permitted profile fields.
- `PATCH /api/users/me/change-password` — Change password after verifying current password.
- `PATCH /api/users/me/request-deactivation` — Prosumer self-deactivation request.

### Backoffice Administration (`/api/users`) — *Backoffice Only*
- `POST /api/users/staff` — Create Backoffice or GridOperator staff user (`Active`).
- `GET /api/users` — List users with filters and pagination.
- `GET /api/users/prosumers/pending` — List Pending Prosumers.
- `GET /api/users/{id}` — Get user details by ID.
- `PUT /api/users/{id}` — Update user profile by ID.
- `PATCH /api/users/{id}/approve` — Approve Pending Prosumer → `Active`.
- `PATCH /api/users/{id}/reject` — Reject Pending Prosumer → `Rejected`.
- `DELETE /api/users/{id}` — Soft delete user → `Deactivated`.
- `PATCH /api/users/{id}/reactivate` — Restore Deactivated user → `Active`.

### Solar Microgrid Stations (`/api/stations`)
- `POST /api/stations` — Create solar station (`Backoffice`).
- `GET /api/stations` — List stations (Backoffice & GridOperator).
- `GET /api/stations/active` — Get all `Active` stations.
- `GET /api/stations/nearby` — Search `Active` stations using Haversine GPS formula.
- `GET /api/stations/{id}` — Get station by ID.
- `PUT /api/stations/{id}` — Update station information (`Backoffice`).
- `PUT /api/stations/{id}/schedule` — Update weekly operating schedule (`Backoffice`).
- `DELETE /api/stations/{id}` — Soft deactivate station (`Backoffice`). Blocked if active reservations exist.
- `PATCH /api/stations/{id}/reactivate` — Reactivate station (`Backoffice`).

### Energy Booking Slots (`/api/booking-slots`, `/api/stations/{stationId}/slots`)
- `POST /api/stations/{stationId}/slots` — Create booking slot (`Backoffice`).
- `GET /api/stations/{stationId}/slots` — Get slots for station.
- `GET /api/booking-slots/{id}` — Get slot by ID.
- `PUT /api/booking-slots/{id}` — Update slot timing & total capacity (`Backoffice`).
- `PATCH /api/booking-slots/{id}/availability` — Update available capacity (`Backoffice`, `GridOperator`).
- `DELETE /api/booking-slots/{id}` — Soft close slot (`Backoffice`). Blocked if active reservations exist.
- `PATCH /api/booking-slots/{id}/reopen` — Reopen `Closed` slot (`Backoffice`).

### Energy Reservations (`/api/reservations`)
- `POST /api/reservations` — Create reservation for authenticated Prosumer (`Prosumer`).
- `POST /api/reservations/for-prosumer/{prosumerId}` — Create reservation on behalf of Active Prosumer (`Backoffice`, `GridOperator`).
- `GET /api/reservations/me` — List Prosumer's reservations with filters (`Prosumer`).
- `GET /api/reservations/me/current` — Get Prosumer's future Pending/Approved reservations (`Prosumer`).
- `GET /api/reservations/me/history` — Get Prosumer's historical reservations (`Prosumer`).
- `GET /api/reservations/me/dashboard` — Get live Prosumer reservation dashboard counts (`Prosumer`).
- `GET /api/reservations` — Staff query across all reservations with filters (`Backoffice`, `GridOperator`).
- `GET /api/reservations/pending` — Get Pending reservations ordered oldest first (`Backoffice`, `GridOperator`).
- `GET /api/reservations/dashboard` — Get live operational staff dashboard counts (`Backoffice`, `GridOperator`).
- `GET /api/reservations/{id}` — Get reservation by ID (Prosumers restricted to own).
- `PUT /api/reservations/{id}` — Update reservation (12-hr & 7-day rules apply; revokes active QR).
- `PATCH /api/reservations/{id}/approve` — Approve Pending reservation → `Approved`.
- `PATCH /api/reservations/{id}/reject` — Reject Pending reservation → `Rejected` (revokes QR & releases 1 capacity unit).
- `PATCH /api/reservations/{id}/cancel` — Cancel reservation → `Cancelled` (revokes QR & releases 1 capacity unit).

### Phase 4 QR Transactions (`/api/reservations/{id}/qr`, `/api/qr`)
| Method | Endpoint | Allowed Roles | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/reservations/{id}/qr` | `Prosumer` | Generate secure QR payload (`SUNGRID:<token>`) for an Approved reservation. |
| `GET` | `/api/reservations/{id}/qr/status` | `Prosumer` | Get safe QR status metadata without raw token or hash. |
| `POST` | `/api/qr/verify` | `GridOperator` | Verify scanned QR payload string and calculate completion window eligibility. |
| `POST` | `/api/qr/complete` | `GridOperator` | Idempotently complete energy transfer → `Completed`. |

---

## Role Permissions Matrix

| Feature / Action | Backoffice | GridOperator | Prosumer |
| :--- | :---: | :---: | :---: |
| Self-Service Reservation Creation | ❌ | ❌ | ✅ |
| Create Reservation On Behalf | ✅ | ✅ | ❌ |
| View Own Reservations & Dashboard | ✅ | ✅ | ✅ |
| View All Reservations & Ops Dashboard | ✅ | ✅ | ❌ |
| Approve / Reject Reservations | ✅ | ✅ | ❌ |
| Update / Cancel Own Reservation | ✅ | ✅ | ✅ (Own Only) |
| Generate QR Payload | ❌ | ❌ | ✅ (Own Approved Only) |
| Verify Scanned QR Payload | ❌ | ✅ | ❌ |
| Complete Energy Transfer | ❌ | ✅ | ❌ |

---

## Android Integration & Backend Separation
The backend **does not generate QR images** (PNG, SVG, Base64 images).

- **Backend Role**: Generates, stores SHA-256 hash, and returns string payload `SUNGRID:<token>`.
- **Android Mobile Role**:
  1. Calls `POST /api/reservations/{id}/qr` to receive `QrPayload`.
  2. Uses a client library (e.g. ZXing Android Embedded or ML Kit) to render the string into a visual QR image on screen.
  3. Displays QR image to Prosumer.
  4. In Grid Operator mode, uses camera scanner to decode string payload and POSTs text to `/api/qr/verify` and `/api/qr/complete`.

---

## Manual Swagger Testing Order (36-Step Complete Sequence)

1. **Start API**: Run `dotnet run --project backend/SunGrid.Api/SunGrid.Api.csproj`.
2. **Health Check**: Call `GET /api/health` → 200 OK.
3. **Log in as Backoffice**: `POST /api/auth/login` (`admin@sungrid.com`).
4. **Confirm Active Station**: `GET /api/stations` as Backoffice. Create station `ST-COL-001` if needed.
5. **Create Future Booking Slot**: Create a booking slot starting ~15–20 minutes in the future.
6. **Log in as Prosumer**: `POST /api/auth/login` as `prosumer1@sungrid.com`.
7. **Create Reservation**: Call `POST /api/reservations` for Prosumer. Status is `Pending`.
8. **Test QR Generation while Pending**: Call `POST /api/reservations/{id}/qr`. Confirm HTTP 400 Bad Request rejection.
9. **Log in as GridOperator**: `POST /api/auth/login` (`operator@sungrid.com`).
10. **Approve Reservation**: Call `PATCH /api/reservations/{id}/approve`. Status becomes `Approved`.
11. **Log in as Owner Prosumer**: Authorize Swagger with Prosumer JWT.
12. **Generate QR Data**: Call `POST /api/reservations/{id}/qr`. Confirm 200 OK response with `SUNGRID:<token>`.
13. **Verify Payload Format**: Confirm payload starts with `SUNGRID:` and contains zero personal data.
14. **Check QR Status**: Call `GET /api/reservations/{id}/qr/status`. Verify `HasQr = true`, `IsExpired = false`.
15. **Regenerate QR**: Re-call `POST /api/reservations/{id}/qr`.
16. **Confirm Old Token Replaced**: Verify new payload string is returned.
17. **Log in as GridOperator**: Authorize Swagger with GridOperator JWT.
18. **Verify QR Payload**: Call `POST /api/qr/verify` with latest payload.
19. **Confirm Verification Response**: Verify `IsValid = true`, `CanComplete = true`, and details match.
20. **Test Prosumer Verification Protection**: Call `POST /api/qr/verify` with Prosumer JWT. Confirm HTTP 403 Forbidden.
21. **Complete Energy Transfer**: Call `POST /api/qr/complete` as GridOperator inside 30-minute window.
22. **Confirm Completed Status**: Response shows `Status = Completed` and `AlreadyCompleted = false`.
23. **Verify Stored Fields**: Verify `ActualEnergyAmountKwh` and `CompletedByUserId` are populated.
24. **Confirm Slot Capacity Not Released**: Check booking slot capacity. Verify capacity was not incremented.
25. **Submit Same QR Again**: Re-send `POST /api/qr/complete` with exact same payload.
26. **Confirm Idempotent 200 OK**: Response returns `AlreadyCompleted = true` with original completion timestamp.
27. **Confirm No Duplicate Effect**: Verify database state remains unchanged.
28. **Check Prosumer History**: Call `GET /api/reservations/me/history`. Verify reservation appears in history.
29. **Check Operations Dashboard**: Call `GET /api/reservations/dashboard`. Verify `CompletedReservationsCount` incremented.
30. **Attempt Updating Completed Reservation**: Call `PUT /api/reservations/{id}`. Confirm HTTP 400 Bad Request rejection.
31. **Attempt Cancelling Completed Reservation**: Call `PATCH /api/reservations/{id}/cancel`. Confirm HTTP 400 Bad Request rejection.
32. **Create & Approve Future Reservation**: Create slot >12 hours away, create reservation, approve it.
33. **Generate QR & Update Reservation**: Generate QR, then call `PUT /api/reservations/{id}`.
34. **Confirm QR Revoked & Pending**: Verify reservation returned to `Pending` and old QR token revoked (`QrRevokedAtUtc` set).
35. **Test Malformed QR Payload**: Call `POST /api/qr/verify` with `SUNGRID:invalid_token`. Confirm general message `"Invalid or expired QR code."`.
36. **Verify End-to-End System Integrity**: Confirm all Phase 1, Phase 2, Phase 3, and Phase 4 endpoints operate cleanly without errors.

---

## Requirements Audit Coverage Table

| Category | Requirement Feature | Endpoint | Allowed Roles | Status |
| :--- | :--- | :--- | :--- | :---: |
| **User** | Staff User Registration | `POST /api/users/staff` | `Backoffice` | ✅ Complete |
| **User** | Prosumer Self-Registration | `POST /api/auth/register/prosumer` | Public | ✅ Complete |
| **User** | Password Login & JWT Issuance | `POST /api/auth/login` | Public (`Active` Users) | ✅ Complete |
| **User** | Password Changing | `PATCH /api/users/me/change-password` | All Authenticated | ✅ Complete |
| **User** | Pending Prosumer Review & Approval | `PATCH /api/users/{id}/approve` | `Backoffice` | ✅ Complete |
| **User** | Account Deactivation & Reactivation | `DELETE /api/users/{id}`, `PATCH /api/users/{id}/reactivate` | `Backoffice` | ✅ Complete |
| **Station** | Microgrid Station CRUD & Schedule | `POST`, `GET`, `PUT`, `DELETE /api/stations` | `Backoffice`, `GridOperator` | ✅ Complete |
| **Station** | Station Deactivation Protection | `DELETE /api/stations/{id}` | `Backoffice` | ✅ Complete (Blocked if active reservations exist) |
| **Station** | Haversine GPS Nearby Search | `GET /api/stations/nearby` | Public / All Authenticated | ✅ Complete |
| **Slot** | Energy Booking Slot Management | `POST`, `GET`, `PUT`, `DELETE /api/booking-slots` | `Backoffice`, `GridOperator` | ✅ Complete |
| **Slot** | Slot Overlap & Capacity Protection | `POST /api/stations/{id}/slots` | `Backoffice` | ✅ Complete |
| **Reservation** | Prosumer Self-Service Booking | `POST /api/reservations` | `Prosumer` | ✅ Complete |
| **Reservation** | On-Behalf Reservation Creation | `POST /api/reservations/for-prosumer/{id}` | `Backoffice`, `GridOperator` | ✅ Complete |
| **Reservation** | 7-Day & 12-Hour Rules | `POST`, `PUT`, `PATCH /api/reservations` | `Prosumer`, Staff | ✅ Complete |
| **Reservation** | Approval, Rejection & Cancellation | `PATCH /api/reservations/{id}/approve`, `reject`, `cancel` | Staff, Owner Prosumer | ✅ Complete |
| **Reservation** | Dashboard Counts & Filters | `GET /api/reservations/me/dashboard`, `/dashboard` | `Prosumer`, Staff | ✅ Complete |
| **QR & Transfer** | Secure QR Payload Generation | `POST /api/reservations/{id}/qr` | `Prosumer` (Approved Only) | ✅ Complete |
| **QR & Transfer** | QR Verification & Expiry Check | `POST /api/qr/verify` | `GridOperator` | ✅ Complete |
| **QR & Transfer** | Idempotent Transfer Completion | `POST /api/qr/complete` | `GridOperator` | ✅ Complete |
| **Health** | System & Database Health Check | `GET /api/health`, `GET /api/health/database` | Public | ✅ Complete |

---

## Production Environment Variables

| Variable Name | Description | Example / Required |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Host environment setting | `Production` |
| `MongoDbSettings__ConnectionString` | MongoDB Atlas SRV URI | `mongodb+srv://user:pass@cluster.mongodb.net/sungrid` |
| `MongoDbSettings__DatabaseName` | Target database name | `sungrid` |
| `JwtSettings__SecretKey` | Cryptographic JWT signing key | `<secure-32+character-key>` |
| `JwtSettings__Issuer` | Valid JWT Issuer claim | `SunGridApi` |
| `JwtSettings__Audience` | Valid JWT Audience claim | `SunGridClients` |
| `CorsSettings__AllowedOrigins__0` | Allowed React app URL | `https://sungrid.yourdomain.com` |
| `SeedAdminSettings__Enabled` | Enable startup admin seeder | `false` (Production recommendation) |
| `Swagger__Enabled` | Enable Swagger in Production | `false` (Set `true` temporarily for demo) |

---

## Final API Verification Checklist

- [x] Authentication & BCrypt hashing operational
- [x] JWT role-based access control enforced across all endpoints
- [x] MongoDB Atlas connection verified (`smartsolar.4bt2u1t.mongodb.net`)
- [x] User management CRUD & status workflows active
- [x] Station management & Haversine GPS search functional
- [x] Booking slot overlap prevention & capacity management active
- [x] 7-Day reservation rule enforced
- [x] 12-Hour update & cancellation rule enforced
- [x] Secure QR token generation & SHA-256 hash storage operational
- [x] 30-Minute QR completion window enforced
- [x] Idempotent energy transfer completion active (`FindOneAndUpdateAsync`)
- [x] Real-time Prosumer & Operations dashboards operational
- [x] Configurable CORS policy implemented
- [x] Zero hardcoded secrets in tracked repository files
- [x] Development build (`dotnet build`) succeeds with 0 warnings & 0 errors
- [x] Release build (`dotnet build -c Release`) succeeds with 0 warnings & 0 errors
- [x] Release publish (`dotnet publish -c Release`) succeeds with valid `web.config`
- [x] Public health endpoints (`/api/health`, `/api/health/database`) operational

---

## Manual Final Smoke Test Sequence (20 Steps)

1. Call `GET /api/health` $\rightarrow$ 200 OK.
2. Call `GET /api/health/database` $\rightarrow$ 200 OK (MongoDB Ping success).
3. Log in as Backoffice (`POST /api/auth/login`).
4. Confirm Backoffice can query users (`GET /api/users`) and stations (`GET /api/stations`).
5. Attempt calling `POST /api/users/staff` as GridOperator. Confirm 403 Forbidden.
6. Log in as Prosumer (`prosumer1@sungrid.com`).
7. Search active stations (`GET /api/stations/active`) and available slots (`GET /api/stations/{id}/slots`).
8. Create a reservation for a slot 2 days in future. Status becomes `Pending`.
9. Verify 7-day rule: Attempt booking a slot 8 days in future. Confirm 400 Bad Request.
10. Log in as GridOperator (`operator@sungrid.com`).
11. Approve the pending reservation (`PATCH /api/reservations/{id}/approve`).
12. Log in as owner Prosumer and generate QR (`POST /api/reservations/{id}/qr`).
13. Verify payload format starts with `SUNGRID:` and contains no personal data.
14. Log in as GridOperator and verify QR (`POST /api/qr/verify`).
15. Complete energy transfer (`POST /api/qr/complete`). Status becomes `Completed`.
16. Resubmit the exact same QR payload to completion endpoint.
17. Confirm HTTP 200 OK response with `alreadyCompleted: true` and unchanged timestamp.
18. Call `GET /api/reservations/me/history` as Prosumer. Confirm completed reservation is listed.
19. Call `GET /api/reservations/dashboard` as Staff. Confirm `completedReservationsCount` incremented.
20. Confirm an unauthenticated request to `/api/users/me` returns 401 Unauthorized.

