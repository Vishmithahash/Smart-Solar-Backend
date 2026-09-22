# SunGrid API — 5-Minute Viva Demonstration Runbook

## Overview
This runbook provides a concise, step-by-step demonstration sequence designed for a **5-minute live viva presentation**.

---

## Pre-Demonstration Setup
1. Start the backend API: `dotnet run --project backend/SunGrid.Api/SunGrid.Api.csproj`.
2. Open Swagger UI at `http://localhost:5261/swagger` (or open Postman).
3. Ensure seed admin credentials (`admin@sungrid.com` / `AdminPassword123!`) and test prosumer (`prosumer1@sungrid.com` / `ProsumerPassword123!`) are ready.

---

## 5-Minute Live Demonstration Flow

### **Step 1: System & Database Health Check (30 Seconds)**
- Execute `GET /api/health` $\rightarrow$ Explain: API is up and running (`Healthy`).
- Execute `GET /api/health/database` $\rightarrow$ Explain: API sends a live MongoDB `ping` command (`Healthy`).

### **Step 2: Backoffice Login & Station Management (1 Minute)**
- Execute `POST /api/auth/login` as `admin@sungrid.com`. Copy returned JWT.
- Authorize Swagger / Postman with Backoffice JWT.
- Execute `GET /api/stations` $\rightarrow$ Show active solar microgrid stations (`ST-COL-001` - Colombo Central Solar Hub).

### **Step 3: Prosumer Booking & 7-Day Rule Verification (1 Minute)**
- Authorize as `prosumer1@sungrid.com`.
- Execute `POST /api/reservations` for a slot 2 days in the future $\rightarrow$ Status: `Pending`.
- Explain: Slot `AvailableCapacity` decremented by 1 automatically.
- Show 7-Day Rule: Attempt booking a slot 8 days in the future $\rightarrow$ HTTP 400 Bad Request rejection (`Booking slot must be scheduled within 7 days`).

### **Step 4: Grid Operator Approval (1 Minute)**
- Authorize as `operator@sungrid.com` (Grid Operator).
- Execute `GET /api/reservations/pending` $\rightarrow$ Show the pending reservation.
- Execute `PATCH /api/reservations/{id}/approve` $\rightarrow$ Status transitions to `Approved`. Explain: Capacity remains held.

### **Step 5: Prosumer QR Generation & Grid Operator Completion (1.5 Minutes)**
- Authorize as `prosumer1@sungrid.com`.
- Execute `POST /api/reservations/{id}/qr` $\rightarrow$ Explain: Returns `SUNGRID:<token>`. Contains zero PII. Raw token hash is saved in MongoDB.
- Authorize as `operator@sungrid.com`.
- Execute `POST /api/qr/verify` with payload $\rightarrow$ Show `isValid: true`, `canComplete: true`.
- Execute `POST /api/qr/complete` with `actualEnergyAmountKwh: 25.5` $\rightarrow$ Status transitions to `Completed`.
- **Idempotency Test**: Re-submit `POST /api/qr/complete` with the exact same payload $\rightarrow$ Returns HTTP 200 OK with `alreadyCompleted: true` and unchanged completion timestamp.
- Execute `GET /api/reservations/dashboard` $\rightarrow$ Show `completedReservationsCount` incremented in live MongoDB operations dashboard.

---

## Report Screenshot Checklist

When taking screenshots for academic reports or defense slides, ensure the following guidelines are followed:

> [!IMPORTANT]
> **Privacy Rule**: Secrets, raw JWTs, MongoDB passwords, and raw QR tokens must be masked or blurred in screenshots.

- [ ] **Swagger UI Endpoint Overview**: Screenshot showing full endpoint catalog grouped by controllers.
- [ ] **Health & Database Health Responses**: Screenshot of `GET /api/health` and `GET /api/health/database` 200 OK responses.
- [ ] **Authentication Response**: Screenshot of `POST /api/auth/login` response showing JWT token format (mask full token string).
- [ ] **User Administration**: Screenshot of `GET /api/users/prosumers/pending` showing Prosumer review list.
- [ ] **Station & GPS Search**: Screenshot of `GET /api/stations/nearby` showing Haversine distance calculations.
- [ ] **Reservation Creation**: Screenshot of `POST /api/reservations` showing status `Pending`.
- [ ] **7-Day Rule Validation**: Screenshot of HTTP 400 Bad Request response for booking beyond 7 days.
- [ ] **12-Hour Rule Validation**: Screenshot of HTTP 400 Bad Request response for updating within 12 hours.
- [ ] **Reservation Approval**: Screenshot of `PATCH /api/reservations/{id}/approve` showing status `Approved`.
- [ ] **QR Token Payload**: Screenshot of `POST /api/reservations/{id}/qr` showing `SUNGRID:<token>` (mask raw token).
- [ ] **QR Verification**: Screenshot of `POST /api/qr/verify` showing `isValid: true` and `canComplete: true`.
- [ ] **Energy Transfer Completion**: Screenshot of `POST /api/qr/complete` showing status `Completed` and `alreadyCompleted: false`.
- [ ] **Idempotent Completion Re-submission**: Screenshot of second `POST /api/qr/complete` showing `alreadyCompleted: true`.
- [ ] **Operational Dashboard Metrics**: Screenshot of `GET /api/reservations/dashboard` showing live MongoDB counts.
- [ ] **MongoDB Atlas Collections**: Screenshot of MongoDB Atlas UI showing `UserDetails`, `SolarStationInfo`, `EnergyBookingSlots`, `EnergyReservations`.
- [ ] **IIS Production Health Check**: Screenshot of IIS-hosted API health check response.
