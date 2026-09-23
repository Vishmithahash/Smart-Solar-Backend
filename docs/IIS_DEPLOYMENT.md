# SunGrid API — Windows Server IIS Deployment Guide

## Overview
This document outlines the step-by-step instructions for publishing and deploying the **SunGrid ASP.NET Core Web API** to **Internet Information Services (IIS)** on Windows Server.

---

## 1. Prerequisites on Windows Server
1. **Windows Server OS** with IIS Role installed.
2. **ASP.NET Core Hosting Bundle**: Install .NET 9.0 Hosting Bundle from Microsoft.
3. Verify IIS registration: Run `net stop was /y` and `net start w3svc` in command prompt.

---

## 2. Release Publish Procedure
Execute the publish command from the root solution folder:
```bash
dotnet publish backend/SunGrid.Api/SunGrid.Api.csproj -c Release -o artifacts/publish
```
Confirm the output directory (`artifacts/publish/`) contains:
- `SunGrid.Api.dll`
- `web.config`
- `appsettings.json` and `appsettings.Production.json`
- Dependent library binaries (`MongoDB.Driver.dll`, `BCrypt.Net-Next.dll`, etc.)

---

## 3. IIS Site & Application Pool Configuration
1. **Copy Published Files**: Copy contents of `artifacts/publish/` to `C:\inetpub\wwwroot\SunGridApi`.
2. **Create Application Pool**:
   - Open IIS Manager $\rightarrow$ **Application Pools** $\rightarrow$ **Add Application Pool**.
   - **Name**: `SunGridAppPool`
   - **.NET CLR version**: `No Managed Code` (Required for ASP.NET Core out-of-process/in-process hosting handler).
   - **Managed pipeline mode**: `Integrated`.
3. **Create IIS Website**:
   - IIS Manager $\rightarrow$ **Sites** $\rightarrow$ **Add Website**.
   - **Site name**: `SunGridApi`
   - **Application Pool**: `SunGridAppPool`
   - **Physical path**: `C:\inetpub\wwwroot\SunGridApi`
   - **Binding**: HTTPS (Port 443 with SSL Certificate).

---

## 4. Secure Environment Variables Configuration
Production configuration secrets must **never** be written to `appsettings.json` or `web.config`. Set system environment variables or configure Application Pool environment variables in IIS:

### **Required Production Environment Variables**
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `MongoDbSettings__ConnectionString` = `mongodb+srv://<dbuser>:<dbpass>@<atlas-cluster>.mongodb.net/sungrid?appName=smartsolar`
- `MongoDbSettings__DatabaseName` = `sungrid`
- `JwtSettings__SecretKey` = `<your-production-secure-32+byte-secret-key>`
- `JwtSettings__Issuer` = `SunGridApi`
- `JwtSettings__Audience` = `SunGridClients`
- `CorsSettings__AllowedOrigins__0` = `https://sungrid.yourdomain.com`
- `SeedAdminSettings__Enabled` = `false`
- `Swagger__Enabled` = `false`

*To set variables in IIS Manager:*
Select `SunGridAppPool` $\rightarrow$ **Advanced Settings** $\rightarrow$ **Environment Variables** $\rightarrow$ Add variables.

---

## 5. MongoDB Atlas Network Access Configuration
1. Log in to **MongoDB Atlas**.
2. Navigate to **Network Access** $\rightarrow$ **Add IP Address**.
3. Add the **Static Public IP Address** of your Windows Server IIS host.
4. Do not use `0.0.0.0/0` in production.

---

## 6. Verification & Health Monitoring
After starting the IIS site:
1. Call public health endpoint: `GET https://sungrid-api.yourdomain.com/api/health` $\rightarrow$ `200 OK`.
2. Call database health endpoint: `GET https://sungrid-api.yourdomain.com/api/health/database` $\rightarrow$ `200 OK` (Pings live MongoDB cluster).
3. Test authentication: `POST https://sungrid-api.yourdomain.com/api/auth/login`.

---

## 7. Supervised Demonstration Swagger Activation
In Production, Swagger UI is disabled by default. To temporarily enable Swagger for a supervised viva or demonstration:
1. Set Environment Variable `Swagger__Enabled` = `true`.
2. Recycle `SunGridAppPool` in IIS Manager.
3. Access `https://sungrid-api.yourdomain.com/swagger`.
4. **Disable afterwards**: Change `Swagger__Enabled` back to `false` and recycle the Application Pool.

---

## 8. Updating Deployed Application Safely
1. Stop `SunGridAppPool` in IIS Manager.
2. Replace files in `C:\inetpub\wwwroot\SunGridApi` with new publish files.
3. Start `SunGridAppPool`.
4. Check IIS stdout logs in `C:\inetpub\wwwroot\SunGridApi\logs\` if startup issues occur.
