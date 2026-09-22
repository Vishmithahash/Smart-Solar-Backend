# SunGrid API — Postman Testing & Verification Guide

## Overview
This guide explains how to import, configure, and execute the **SunGrid API Postman Collection** for automated and manual testing of all 51 REST API endpoints across 7 controllers.

---

## 1. Importing Collection and Environment
1. Open **Postman** (v10+).
2. Click **Import** $\rightarrow$ Select `postman/SunGrid_API.postman_collection.json`.
3. Click **Import** $\rightarrow$ Select `postman/SunGrid_Local.postman_environment.example.json`.
4. Duplicate the imported environment and rename it to `SunGrid Local`.

---

## 2. Environment Configuration
Populate your private `SunGrid Local` environment with local test credentials:
- `baseUrl`: `http://localhost:5261`
- `backofficeEmail`: `admin@sungrid.com`
- `backofficePassword`: `AdminPassword123!`
- `gridOperatorEmail`: `operator@sungrid.com`
- `gridOperatorPassword`: `OperatorPassword123!`
- `prosumerEmail`: `prosumer1@sungrid.com`
- `prosumerPassword`: `ProsumerPassword123!`

*Never commit your local environment file containing active passwords to Git.*

---

## 3. Dynamic Variables & Pre-Request Scripts
The collection uses a top-level pre-request script to automatically manage test isolation:
- **`runId`**: Generates a random 4-digit code per run (e.g. `1042`), appending it to `nic`, `email`, and `stationCode` so repeated collection runs never conflict.
- **Dynamic UTC Dates**: Automatically calculates ISO 8601 UTC date strings for slot creation & rule verification:
  - `slotStartTwoDaysUtc`, `slotEndTwoDaysUtc`: Slot 2 days in the future.
  - `slotStartSixDaysUtc`, `slotEndSixDaysUtc`: Slot 6 days in the future.
  - `slotStartEightDaysUtc`, `slotEndEightDaysUtc`: Slot 8 days in the future (used for 7-day rule failure test).
  - `slotStartNearTermUtc`, `slotEndNearTermUtc`: Slot 20 minutes in the future (used for 30-min QR completion window test).

---

## 4. Automatic JWT & Entity ID Storage
Postman test scripts automatically capture and store tokens and IDs upon request completion:
- **Login requests**: Save `backofficeToken`, `gridOperatorToken`, `prosumerToken`.
- **Entity creation**: Saves `newProsumerId`, `stationId`, `bookingSlotId`, `secondBookingSlotId`, `reservationId`, `reservationReference`, `qrPayload`.

---

## 5. Recommended Execution Order (Folder Sequence)

1. `1. Health` — Verify system & database connectivity.
2. `2. Authentication` — Log in as Backoffice, Grid Operator, and Prosumer.
3. `3. User Management` — Verify profile endpoints and Prosumer approval.
4. `4. Solar Stations` — Create microgrid station and test Haversine GPS search.
5. `5. Booking Slots` — Create booking slots for station.
6. `6. Reservations` — Create Prosumer reservation, approve as Grid Operator.
7. `7. QR Transactions` — Generate QR payload, verify as Grid Operator, complete transfer, and test idempotent duplicate completion.
8. `8. Authorization Negative Tests` — Verify role enforcement (403), missing JWT (401), and 7-day rule violation (400).

---

## 6. Negative Test Matrix

| # | Endpoint | Test Condition | Target Role | Expected Status | Expected Outcome |
| :-: | :--- | :--- | :---: | :-: | :--- |
| 1 | `POST /api/auth/login` | Invalid password | Anonymous | `401 Unauthorized` | Login fails, no JWT issued. |
| 2 | `POST /api/auth/login` | Pending account login | Anonymous | `401 Unauthorized` | Login blocked for unapproved Prosumers. |
| 3 | `POST /api/auth/register/prosumer` | Duplicate email / NIC | Anonymous | `409 Conflict` | Rejects registration with duplicate conflict error. |
| 4 | `POST /api/users/staff` | Call by GridOperator | `GridOperator` | `403 Forbidden` | Restricted to Backoffice role. |
| 5 | `POST /api/stations` | Call by Prosumer | `Prosumer` | `403 Forbidden` | Restricted to Backoffice role. |
| 6 | `GET /api/users/me` | Missing JWT Header | Unauthenticated | `401 Unauthorized` | Request rejected for missing JWT. |
| 7 | `GET /api/users/{id}` | Invalid ObjectId (`123`) | `Backoffice` | `400 Bad Request` | Fails ObjectId format validation. |
| 8 | `DELETE /api/stations/{id}` | Active reservations exist | `Backoffice` | `409 Conflict` | Deactivation blocked to prevent orphan bookings. |
| 9 | `POST /api/stations/{id}/slots` | Overlapping slot times | `Backoffice` | `409 Conflict` | Slot creation blocked due to time overlap. |
| 10 | `POST /api/reservations` | Slot > 7 days ahead | `Prosumer` | `400 Bad Request` | Rejects booking violating 7-day rule. |
| 11 | `PUT /api/reservations/{id}` | Update < 12 hrs before start | `Prosumer` | `400 Bad Request` | Rejects modification violating 12-hr rule. |
| 12 | `PATCH /api/reservations/{id}/cancel` | Cancel < 12 hrs before start | `Prosumer` | `400 Bad Request` | Rejects cancellation violating 12-hr rule. |
| 13 | `GET /api/reservations/{id}` | Prosumer viewing another's | `Prosumer` | `403 Forbidden` | Prosumer ownership protection enforced. |
| 14 | `POST /api/reservations/{id}/qr` | Reservation is Pending | `Prosumer` | `400 Bad Request` | Rejects QR generation for unapproved booking. |
| 15 | `POST /api/qr/verify` | Malformed QR text | `GridOperator` | `200 OK` (`isValid:false`)| Returns safe invalid QR message. |
| 16 | `POST /api/qr/verify` | Call by Prosumer | `Prosumer` | `403 Forbidden` | Restricted to GridOperator role. |
| 17 | `POST /api/qr/complete` | Transfer amount > Capacity | `GridOperator` | `400 Bad Request` | Rejects energy amount exceeding station capacity. |
| 18 | `POST /api/qr/complete` | Outside 30-min window | `GridOperator` | `400 Bad Request` | Rejects completion prior to 30 mins before start. |
| 19 | `POST /api/qr/complete` | Duplicate scan after completion | `GridOperator` | `200 OK` (`alreadyCompleted:true`) | Idempotent response returned without double-completing. |
