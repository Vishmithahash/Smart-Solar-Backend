# SunGrid API — Complete Endpoint Catalog

## Overview
This document provides the definitive source-code-audited catalog of all **51 REST API endpoints** exposed across **7 Controllers** in the SunGrid ASP.NET Core Web API backend.

- **Total Controllers**: 7
- **Total Endpoints**: 51

---

## Endpoint Count Summary

| Controller Class | Route Prefix | Action Count | Description |
| :--- | :--- | :---: | :--- |
| [`HealthController`](#1-healthcontroller-2-endpoints) | `/api/health` | **2** | System & MongoDB database ping health check endpoints. |
| [`AuthController`](#2-authcontroller-2-endpoints) | `/api/auth` | **2** | Prosumer self-registration and JWT password authentication. |
| [`UsersController`](#3-userscontroller-13-endpoints) | `/api/users` | **13** | Profile management (`/me`) and Backoffice user administration. |
| [`StationsController`](#4-stationscontroller-9-endpoints) | `/api/stations` | **9** | Microgrid station CRUD, operating schedule, and Haversine GPS search. |
| [`BookingSlotsController`](#5-bookingslotscontroller-7-endpoints) | `/api/booking-slots` / `/api/stations/{id}/slots` | **7** | Station booking slot creation, capacity updates, and soft closure. |
| [`ReservationsController`](#6-reservationscontroller-14-endpoints) | `/api/reservations` | **14** | Reservation lifecycle management, rules (7-day/12-hr), & dashboards. |
| [`QrTransactionsController`](#7-qrtransactionscontroller-4-endpoints) | `/api/qr` / `/api/reservations/{id}/qr` | **4** | Secure QR payload generation, verification, and idempotent completion. |
| **TOTAL** | | **51** | |

---

## Detailed Endpoint Specifications

### 1. `HealthController` (2 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `GET` | `/api/health` | Public | Anonymous | None | `HealthResponse` (200) | Public system readiness check. |
| `GET` | `/api/health/database` | Public | Anonymous | None | `DatabaseHealthResponse` (200) | Sends MongoDB ping command. Returns 503 if database unreachable. |

---

### 2. `AuthController` (2 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `POST` | `/api/auth/register/prosumer` | Public | Anonymous | `RegisterProsumerRequest` | `UserResponse` (201) | Self-registration creates Prosumer in `Pending` status. Rejects duplicate Email/NIC (409 Conflict). |
| `POST` | `/api/auth/login` | Public | Anonymous | `LoginRequest` | `LoginResponse` (200) | Validates BCrypt hash. Blocks inactive/pending accounts (401 Unauthorized). Returns signed JWT. |

---

### 3. `UsersController` (13 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `GET` | `/api/users/me` | JWT | All Authenticated | None | `UserResponse` (200) | Returns current user's profile details. |
| `PUT` | `/api/users/me` | JWT | All Authenticated | `UpdateUserRequest` | `UserResponse` (200) | Updates profile info (`FullName`, `PhoneNumber`, `Address`) for current user. |
| `PATCH` | `/api/users/me/change-password` | JWT | All Authenticated | `ChangePasswordRequest` | `{ message }` (200) | Verifies current password before updating BCrypt hash (400 if invalid). |
| `PATCH` | `/api/users/me/request-deactivation` | JWT | `Prosumer` | None | `UserResponse` (200) | Transitions Prosumer status to `DeactivationRequested`. |
| `POST` | `/api/users/staff` | JWT | `Backoffice` | `CreateStaffUserRequest` | `UserResponse` (201) | Creates `Backoffice` or `GridOperator` staff user with `Active` status. Rejects duplicate email (409). |
| `GET` | `/api/users` | JWT | `Backoffice` | Query Params | `UserListResponse` (200) | Returns paginated users with optional `role`, `status`, and `search` filters. |
| `GET` | `/api/users/prosumers/pending` | JWT | `Backoffice` | None | `List<UserResponse>` (200) | Returns all Pending Prosumer applications awaiting administrative review. |
| `GET` | `/api/users/{id}` | JWT | `Backoffice` | None | `UserResponse` (200) | Gets single user details by MongoDB ObjectId string (404 if missing). |
| `PUT` | `/api/users/{id}` | JWT | `Backoffice` | `UpdateUserRequest` | `UserResponse` (200) | Updates specified user's profile fields by ID. |
| `PATCH` | `/api/users/{id}/approve` | JWT | `Backoffice` | None | `UserResponse` (200) | Approves Pending Prosumer $\rightarrow$ `Active`. |
| `PATCH` | `/api/users/{id}/reject` | JWT | `Backoffice` | None | `UserResponse` (200) | Rejects Pending Prosumer $\rightarrow$ `Rejected`. |
| `DELETE` | `/api/users/{id}` | JWT | `Backoffice` | None | `UserResponse` (200) | Soft-deletes user $\rightarrow$ `Deactivated`. |
| `PATCH` | `/api/users/{id}/reactivate` | JWT | `Backoffice` | None | `UserResponse` (200) | Restores Deactivated user $\rightarrow$ `Active`. |

---

### 4. `StationsController` (9 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `POST` | `/api/stations` | JWT | `Backoffice` | `CreateStationRequest` | `StationResponse` (201) | Creates active solar station with operating schedule. Unique `StationCode` enforced (409). |
| `GET` | `/api/stations` | JWT | `Backoffice`, `GridOperator` | Query Params | `StationListResponse` (200) | Returns paginated stations. GridOperators receive only Active stations. |
| `GET` | `/api/stations/active` | JWT | All Authenticated | None | `List<StationResponse>` (200) | Returns all `Active` microgrid stations for web/mobile client display. |
| `GET` | `/api/stations/nearby` | JWT | All Authenticated | Query Params | `List<NearbyStationResponse>` (200) | Haversine GPS search returning Active stations within specified radius (`radiusKm`). |
| `GET` | `/api/stations/{id}` | JWT | All Authenticated | None | `StationResponse` (200) | Gets station by ID. Non-Backoffice users blocked if station is Inactive (404). |
| `PUT` | `/api/stations/{id}` | JWT | `Backoffice` | `UpdateStationRequest` | `StationResponse` (200) | Updates station name, address, coordinates, and capacity details. |
| `PUT` | `/api/stations/{id}/schedule` | JWT | `Backoffice` | `UpdateStationScheduleRequest` | `StationResponse` (200) | Replaces 7-day operating schedule timing and closed day flags. |
| `DELETE` | `/api/stations/{id}` | JWT | `Backoffice` | None | `StationResponse` (200) | Soft deactivates station $\rightarrow$ `Inactive`. Blocked if active reservations exist (409). |
| `PATCH` | `/api/stations/{id}/reactivate` | JWT | `Backoffice` | None | `StationResponse` (200) | Reactivates `Inactive` station $\rightarrow$ `Active`. |

---

### 5. `BookingSlotsController` (7 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `POST` | `/api/stations/{stationId}/slots` | JWT | `Backoffice` | `CreateBookingSlotRequest` | `BookingSlotResponse` (201) | Creates booking slot for station. Rejects overlapping time windows for same station (409). |
| `GET` | `/api/stations/{stationId}/slots` | JWT | All Authenticated | Query Params | `List<BookingSlotResponse>` (200) | Gets slots for station. Prosumers see future `Available` slots only. |
| `GET` | `/api/booking-slots/{id}` | JWT | All Authenticated | None | `BookingSlotResponse` (200) | Gets single booking slot by ID. |
| `PUT` | `/api/booking-slots/{id}` | JWT | `Backoffice` | `UpdateBookingSlotRequest` | `BookingSlotResponse` (200) | Updates slot timing & capacity. Rejects overlaps or capacity reduction below active bookings (409). |
| `PATCH` | `/api/booking-slots/{id}/availability` | JWT | `Backoffice`, `GridOperator` | `UpdateSlotAvailabilityRequest` | `BookingSlotResponse` (200) | Manually updates available capacity and auto-adjusts slot status (`Available`/`Full`). |
| `DELETE` | `/api/booking-slots/{id}` | JWT | `Backoffice` | None | `BookingSlotResponse` (200) | Soft closes slot $\rightarrow$ `Closed`. Blocked if active reservations exist (409). |
| `PATCH` | `/api/booking-slots/{id}/reopen` | JWT | `Backoffice` | None | `BookingSlotResponse` (200) | Reopens future `Closed` slot $\rightarrow$ `Available`. |

---

### 6. `ReservationsController` (14 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `POST` | `/api/reservations` | JWT | `Prosumer` | `CreateReservationRequest` | `ReservationResponse` (201) | Creates `Pending` reservation for owner Prosumer. Enforces 7-day rule and slot capacity (-1). |
| `POST` | `/api/reservations/for-prosumer/{prosumerId}` | JWT | `Backoffice`, `GridOperator` | `CreateReservationRequest` | `ReservationResponse` (201) | Staff operation creating reservation on behalf of Active Prosumer. |
| `GET` | `/api/reservations/me` | JWT | `Prosumer` | Query Params | `ReservationListResponse` (200) | Paginated list of Prosumer's own reservations with status, date, and search filters. |
| `GET` | `/api/reservations/me/current` | JWT | `Prosumer` | None | `List<ReservationResponse>` (200) | Retrieves owner Prosumer's active future `Pending` and `Approved` reservations. |
| `GET` | `/api/reservations/me/history` | JWT | `Prosumer` | None | `List<ReservationResponse>` (200) | Retrieves owner Prosumer's historical (`Completed`, `Cancelled`, `Rejected`, past slot) reservations. |
| `GET` | `/api/reservations/me/dashboard` | JWT | `Prosumer` | None | `ProsumerReservationDashboardResponse` (200) | Returns live metric counts for owner Prosumer dashboard. |
| `GET` | `/api/reservations` | JWT | `Backoffice`, `GridOperator` | Query Params | `ReservationListResponse` (200) | Staff query across all reservations with status, station, slot, prosumer, and date filters. |
| `GET` | `/api/reservations/pending` | JWT | `Backoffice`, `GridOperator` | None | `List<ReservationResponse>` (200) | Returns Pending reservations ordered oldest first for administrative review. |
| `GET` | `/api/reservations/dashboard` | JWT | `Backoffice`, `GridOperator` | None | `OperationsDashboardResponse` (200) | Returns live metric counts for operational staff dashboard. |
| `GET` | `/api/reservations/{id}` | JWT | All Authenticated | None | `ReservationResponse` (200) | Gets reservation by ID. Prosumers restricted to viewing their own reservations (403). |
| `PUT` | `/api/reservations/{id}` | JWT | All Authenticated | `UpdateReservationRequest` | `ReservationResponse` (200) | Updates reservation. Enforces 12-hour rule & 7-day rule. Resets `Approved` $\rightarrow$ `Pending` and revokes active QR. |
| `PATCH` | `/api/reservations/{id}/approve` | JWT | `Backoffice`, `GridOperator` | None | `ReservationResponse` (200) | Approves Pending reservation $\rightarrow$ `Approved`. Slot capacity remains held. |
| `PATCH` | `/api/reservations/{id}/reject` | JWT | `Backoffice`, `GridOperator` | `RejectReservationRequest` | `ReservationResponse` (200) | Rejects Pending reservation $\rightarrow$ `Rejected`. Restores slot capacity (+1 unit) & revokes QR. |
| `PATCH` | `/api/reservations/{id}/cancel` | JWT | All Authenticated | `CancelReservationRequest` | `ReservationResponse` (200) | Soft-cancels reservation $\rightarrow$ `Cancelled`. Enforces 12-hour rule. Restores slot capacity (+1) & revokes QR. |

---

### 7. `QrTransactionsController` (4 Endpoints)

| Method | Complete Route | Auth | Allowed Roles | Request DTO | Success Response | Main Business Rule / Error Codes |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| `POST` | `/api/reservations/{reservationId}/qr` | JWT | `Prosumer` | None | `GenerateQrResponse` (200) | Generates `SUNGRID:<token>` for owner's `Approved` reservation. Saves SHA-256 hash in DB. Replaces old token. |
| `GET` | `/api/reservations/{reservationId}/qr/status` | JWT | `Prosumer` | None | `QrStatusResponse` (200) | Returns safe QR metadata (`HasQr`, `IsExpired`, `IsUsed`, `IsRevoked`) without exposing raw token or hash. |
| `POST` | `/api/qr/verify` | JWT | `GridOperator` | `VerifyQrRequest` | `VerifyQrResponse` (200) | Hashes scanned token, matches DB, verifies unrevoked/unexpired, checks 30-min window (`CanComplete`). |
| `POST` | `/api/qr/complete` | JWT | `GridOperator` | `CompleteEnergyTransferRequest` | `CompleteEnergyTransferResponse` (200) | Atomic conditional update $\rightarrow$ `Completed`. Enforces 30-min window. Repeated calls return 200 with `AlreadyCompleted = true`. |
