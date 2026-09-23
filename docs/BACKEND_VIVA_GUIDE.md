# SunGrid API — Viva Defense Quick Guide

## Architectural Overview

### 1. Why does one backend serve both React Web and Android Mobile apps?
> Maintaining a single central ASP.NET Core REST API guarantees unified business rules, consistent security, atomic database updates, and data integrity. Both clients consume identical JSON endpoints.

### 2. What architecture is used in this project?
> The project follows a clean **Controller $\rightarrow$ Service $\rightarrow$ MongoDbContext** design (FAT Service pattern). It avoids over-engineering (no Clean Architecture split, no CQRS, no MediatR, no AutoMapper, no generic repositories) to remain simple, readable, and maintainable.

### 3. What are the responsibilities of each layer?
- **Controller**: Routing, HTTP status code mapping, model validation, and JWT role attributes (`[Authorize(Roles = "...")]`).
- **Service**: Business logic, 7-day rule, 12-hour rule, Haversine GPS calculations, slot capacity checks, QR token cryptography/hashing, and state transitions.
- **MongoDbContext**: Manages `MongoClient`, exposes collection handles, and initializes indexes.

---

## Key Technical Decisions & viva Answers

### 4. Why MongoDB?
> Microgrid stations, flexible operating schedules, and time-based booking slots naturally fit JSON document schemas. MongoDB offers fast queries, flexible document updates, and built-in indexing.

### 5. What are the 4 main collections?
1. `UserDetails` (User accounts, hashed passwords, roles, NICs)
2. `SolarStationInfo` (Station locations, capacity, operating schedules)
3. `EnergyBookingSlots` (Time slots, total & available capacity)
4. `EnergyReservations` (Reservation references, transfer type, QR token hash, completion audit)

### 6. How is authentication and authorization handled?
> **Authentication**: Uses JWT Bearer tokens signed with a 256-bit secret key. Passwords are hashed using BCrypt.
> **Authorization**: Role-based access control (`Backoffice`, `GridOperator`, `Prosumer`) enforced at the API level via `[Authorize(Roles = "...")]`.

### 7. How does the 7-Day Reservation Rule work?
> Reservation creation and slot migration require slot start time to be in the future and no later than 7 days from current time: $\text{currentUtc} < \text{slotStartUtc} \le \text{currentUtc} + 7\text{ days}$.

### 8. How does the 12-Hour Update & Cancellation Rule work?
> Updating or cancelling a reservation requires at least 12 hours before slot start time ($\text{slotStartUtc} - \text{currentUtc} \ge 12\text{ hours}$). Less than 12 hours returns HTTP 400.

### 9. How is slot capacity kept consistent?
> Each reservation decrements `AvailableCapacity` by 1 using an atomic MongoDB update (`TryReserveOneCapacityUnitAsync`). If rejected or cancelled, capacity is restored (+1 unit).

### 10. Why does the QR contain only a random token (`SUNGRID:<token>`)?
> Including user PII, reservation IDs, or JWTs inside a QR code creates privacy leaks and tampering risks. An opaque random token acts as a single-use secure ticket without exposing PII.

### 11. Why does MongoDB store only the token hash (`QrTokenHash`)?
> Storing raw tokens in MongoDB creates security risks if the database is read. Storing only `SHA256(rawToken)` ensures valid QR payloads cannot be derived even if database contents are inspected.

### 12. How is duplicate completion prevented (Idempotency)?
> Completion uses an **atomic conditional update** (`FindOneAndUpdateAsync`) checking `Status == Approved` and `QrUsedAtUtc == null`. The first request sets `Status = Completed`. Repeated requests fail the filter, detect that the reservation is already completed, and safely return HTTP 200 OK with `alreadyCompleted: true`.

### 13. Why is slot capacity NOT released upon transfer completion?
> Capacity was reserved when the Pending reservation was created. Completion simply records that the physical energy transaction occurred. Releasing capacity upon completion would incorrectly allow unreserved extra bookings into the slot.

### 14. How are production secrets secured?
> Local development uses `.NET User Secrets`. Production deployments use IIS or OS environment variables (`MongoDbSettings__ConnectionString`, `JwtSettings__SecretKey`). No secrets are committed to Git.

### 15. How does the database health check work?
> `GET /api/health/database` sends a MongoDB `ping` command (`RunCommandAsync`). Returns `200 OK` (Healthy) or `503 Service Unavailable` (Unhealthy) without leaking database credentials or server addresses.
