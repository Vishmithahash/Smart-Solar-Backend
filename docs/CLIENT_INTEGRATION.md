# SunGrid API — Client Integration Guide (React Web & Native Android)

## Overview
The **SunGrid Web API** serves as the single unified backend for both:
1. **React Web Application** (Backoffice management, station management, reservation approvals, and operational dashboards)
2. **Native Android Mobile Application** (Prosumer profile, nearby station search, slot booking, QR code display, and Grid Operator QR scanning/completion)

Clients **must never connect directly to MongoDB**. All data access, authentication, validation, and business logic are enforced exclusively by the ASP.NET Core Web API.

---

## 1. Base API URL & Configuration
- **Development Environment**:
  - React Web App: `http://localhost:5261/api`
  - Android Emulator: Use `http://10.0.2.2:5261/api` (In Android Emulator, `10.0.2.2` bridges directly to the host machine's `localhost`).
  - Physical Android Device: `http://<host-machine-ip>:5261/api`
- **Production Environment**:
  - Both React and Android clients connect to the HTTPS IIS URL: `https://sungrid-api.yourdomain.com/api`

---

## 2. Authentication & JWT Token Usage
1. Public registration: `POST /api/auth/register/prosumer`
2. Login: `POST /api/auth/login` with `Email` and `Password`.
3. Returns JSON response:
   ```json
   {
     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
     "expiryMinutes": 120,
     "userId": "673f1a2b3c4d5e6f7a8b9c0d",
     "fullName": "Sunil Perera",
     "role": "Prosumer"
   }
   ```
4. Authenticated Requests: Pass token in header:
   ```http
   Authorization: Bearer <token>
   ```
5. **Security Rule**: Never store raw user passwords in client local storage, SharedPreferences, or SQLite. Store only the JWT securely.

---

## 3. Data Formats & Conventions
- **Property Naming**: camelCase (e.g. `reservationReference`, `actualEnergyAmountKwh`).
- **Enums**: Human-readable string representations (e.g. `"Prosumer"`, `"Active"`, `"Approved"`, `"EnergyDropOff"`).
- **Date & Time**: ISO 8601 UTC format (`YYYY-MM-DDTHH:mm:ssZ`).
- **Null Handling**: Omitted or `null` for non-populated optional fields.

---

## 4. User Roles & Account Status Values

### **User Roles**
- `Backoffice`: System administrator. Manages staff, stations, slots, prosumer approvals, and global reservations.
- `GridOperator`: Microgrid station staff. Approves/rejects reservations, verifies QR codes, and completes energy transfers.
- `Prosumer`: Solar owner/consumer. Manages profile, searches stations, books slots, requests QR tokens.

### **Account Status Values**
- `Pending`: Newly registered Prosumer awaiting Backoffice approval.
- `Active`: Verified active user. Can log in and perform role-allowed actions.
- `DeactivationRequested`: Prosumer requested self-deactivation.
- `Deactivated`: Account soft-deleted/deactivated.
- `Rejected`: Prosumer registration application rejected by Backoffice.

---

## 5. Domain Enums

### **Reservation Status**
- `Pending`: Reservation created, slot capacity held (1 unit). Awaiting staff review.
- `Approved`: Approved by staff. Capacity held. Eligible for QR generation by Prosumer.
- `Rejected`: Rejected by staff. Slot capacity released (+1 unit). Active QR revoked.
- `Cancelled`: Cancelled by user/staff. Slot capacity released (+1 unit). Active QR revoked.
- `Completed`: Energy transfer physically completed at station by Grid Operator. Capacity NOT released.

### **Station & Slot Status**
- **Station Status**: `Active`, `Inactive`.
- **Booking Slot Status**: `Available`, `Full`, `Closed`.
- **Energy Transfer Type**: `EnergyDropOff`, `Charging`.

---

## 6. Phase 4 QR Integration Flow

### **Prosumer Flow (Android / Web)**
1. Prosumer selects an `Approved` reservation and calls:
   ```http
   POST /api/reservations/{id}/qr
   ```
2. API returns payload string:
   ```json
   {
     "reservationId": "673f1a2b3c4d5e6f7a8b9c0d",
     "reservationReference": "RES-20260922-A1B2C3",
     "qrPayload": "SUNGRID:4A8F9C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B",
     "issuedAtUtc": "2026-09-22T10:00:00Z",
     "expiresAtUtc": "2026-09-23T11:00:00Z"
   }
   ```
3. Android app uses a client renderer (e.g. ZXing) to display `qrPayload` as a visual QR barcode image.

### **Grid Operator Flow (Android Camera Scanning)**
1. Operator scans the physical QR image displayed on Prosumer's phone.
2. Operator calls verification endpoint with decoded text:
   ```http
   POST /api/qr/verify
   Body: { "qrPayload": "SUNGRID:4A8F9C1D..." }
   ```
3. API returns server-verified details and `canComplete: true/false`.
4. Operator confirms actual energy amount and calls:
   ```http
   POST /api/qr/complete
   Body: { "qrPayload": "SUNGRID:4A8F9C1D...", "actualEnergyAmountKwh": 25.5, "completionNotes": "Completed successfully" }
   ```
5. API performs atomic completion $\rightarrow$ `Completed`. Repeated calls return 200 OK with `alreadyCompleted: true`.

---

## 7. HTTP Status Code Conventions
- `200 OK`: Request succeeded.
- `400 Bad Request`: Input validation failed, 7-day rule violation, 12-hour rule violation, or invalid/expired QR payload.
- `401 Unauthorized`: Missing or expired JWT `Authorization` header.
- `403 Forbidden`: Insufficient role permissions or accessing another user's private data.
- `404 Not Found`: Requested record does not exist.
- `409 Conflict`: Business state conflict (e.g. duplicate reservation, station deactivation blocked).
- `503 Service Unavailable`: Database health check ping failed.
- `500 Internal Server Error`: Generic safe server error response in production.
