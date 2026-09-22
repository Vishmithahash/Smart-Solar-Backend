# SunGrid – Smart Solar Microgrid Trading System Backend

Complete ASP.NET Core 9.0 Web API backend connected to MongoDB for the **SunGrid Smart Solar Microgrid Trading System**.

The backend serves as a single unified REST API (51 Endpoints across 7 Controllers) for both:
1. **React Web Application** (Backoffice administration, station management, reservation review & monitoring)
2. **Native Android Mobile Application** (Prosumer profile, station search, slot booking, QR display, Grid Operator QR scanning & transfer completion)

---

## Quick Reference & Project Structure

- **Backend Web API Project**: [`backend/SunGrid.Api`](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/backend/SunGrid.Api/README.md)
- **Solution File**: `SunGrid.sln`

### **Documentation Links**
- [**API Endpoint Catalog**](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/docs/API_ENDPOINT_CATALOG.md) — Complete 51-endpoint specification catalog.
- [**Client Integration Guide**](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/docs/CLIENT_INTEGRATION.md) — Integration guide for React and Android clients.
- [**Postman Testing Guide**](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/docs/POSTMAN_TESTING.md) — Postman collection setup, dynamic scripts, and negative test matrix.
- [**Demonstration Runbook**](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/docs/DEMO_RUNBOOK.md) — 5-minute viva demonstration guide and screenshot checklist.
- [**IIS Deployment Guide**](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/docs/IIS_DEPLOYMENT.md) — Deployment, environment variable setup, and IIS Hosting Bundle configuration.
- [**Backend Viva Defense Guide**](file:///c:/Users/Vishmitha%20Hashendra/OneDrive%20-%20Sri%20Lanka%20Institute%20of%20Information%20Technology/Desktop/Smart%20Solar/Solar%20Backend/Smart-Solar-Backend/docs/BACKEND_VIVA_GUIDE.md) — Student viva Q&A guide explaining architectural decisions.

---

## Core Capabilities Implemented

- **Authentication & Security**: BCrypt password hashing, JWT Bearer tokens, role-based access control (`Backoffice`, `GridOperator`, `Prosumer`).
- **User Management**: Prosumer NIC self-registration, Backoffice approval workflow, staff user management, soft deactivation & reactivation.
- **Microgrid Station Management**: Station CRUD, weekly operating schedule, GPS coordinates, Haversine nearby station search, soft deactivation protections.
- **Energy Booking Slots**: Slot timing, total and available capacity management, overlap prevention, soft closure protections.
- **Energy Reservations**: Prosumer booking, staff on-behalf creation, 7-day rule, 12-hour rule, slot capacity consistency, approval/rejection/cancellation workflows, live operation dashboards.
- **Secure QR & Transfer Completion**: 32-byte random cryptographic QR tokens (`SUNGRID:<token>`), SHA-256 token hashing, 30-minute completion window, atomic idempotent energy transfer completion (`ReservationStatus.Completed`).
- **Health Monitoring & IIS Production Setup**: Public API health check (`/api/health`), MongoDB ping check (`/api/health/database`), configurable CORS policy, startup validation, `appsettings.Production.json`, and Release publish pipeline.

---

## Verification Commands

- **Development Build**: `dotnet build SunGrid.sln` (0 Warnings, 0 Errors)
- **Release Build**: `dotnet build SunGrid.sln -c Release` (0 Warnings, 0 Errors)
- **Release Publish**: `dotnet publish backend/SunGrid.Api/SunGrid.Api.csproj -c Release -o artifacts/publish`