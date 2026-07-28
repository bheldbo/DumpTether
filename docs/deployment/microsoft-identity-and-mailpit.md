# Microsoft Identity And Local Mail

## Microsoft Entra Registration

DumpTether uses one backend-mediated Microsoft OpenID Connect flow. The web,
desktop, and future mobile clients do not contain the Microsoft client secret.

In the Microsoft Entra admin center:

1. Open **Identity > Applications > App registrations**.
2. Select the DumpTether registration.
3. Copy **Application (client) ID** from **Overview**.
4. Use `common` for organizational and personal Microsoft accounts, or copy
   **Directory (tenant) ID** for a tenant-specific deployment.
5. Open **Certificates & secrets > Client secrets > New client secret**.
6. Copy the secret **Value** immediately. The secret ID is not the credential.
7. Open **Authentication > Add a platform > Web**.
8. Add the exact callback URI for each deployed API.

Local callback:

```text
http://localhost:55868/api/auth/oauth/microsoft/callback
```

Production callback:

```text
https://your-dumptether-domain.example/api/auth/oauth/microsoft/callback
```

Local uncommitted `.env`:

```text
DUMPTETHER_OAUTH_MICROSOFT_ENABLED=true
DUMPTETHER_OAUTH_MICROSOFT_CLIENT_ID=<Application client ID>
DUMPTETHER_OAUTH_MICROSOFT_CLIENT_SECRET=<client secret Value>
DUMPTETHER_OAUTH_MICROSOFT_TENANT_ID=common
```

The development script maps these names to ASP.NET configuration:

```text
OAuth__Microsoft__Enabled
OAuth__Microsoft__ClientId
OAuth__Microsoft__ClientSecret
OAuth__Microsoft__TenantId
```

For production, place the values in the server's real `.env.prod` or secret
store. Do not duplicate real values in committed `appsettings` files.

Microsoft login is useful for ordinary personal sign-in as well as Entra/AD.
Importing Entra groups or application roles requires Microsoft Graph
permissions, admin consent, and a separate authorization design; it is not part
of the current sign-in flow.

## Mailpit

Mailpit captures local SMTP messages and displays them in a browser. It does not
deliver public production mail.

Start it:

```powershell
.\scripts\dev.ps1 -Target Mail
```

Configure a locally running API:

```text
DUMPTETHER_EMAIL_CONFIRMATION_ENABLED=true
DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL=http://localhost:55868
DUMPTETHER_EMAIL_PROVIDER=Smtp
DUMPTETHER_EMAIL_FROM=noreply@dumptether.local
DUMPTETHER_EMAIL_SMTP_HOST=localhost
DUMPTETHER_EMAIL_SMTP_PORT=1025
DUMPTETHER_EMAIL_SMTP_USE_AUTHENTICATION=false
DUMPTETHER_EMAIL_SMTP_ENABLE_SSL=false
```

Open the inbox at `http://127.0.0.1:8025`. When the API itself runs in
`docker-compose.local.yml`, set the SMTP host to `mailpit`.

Production uses `Email:Provider=Smtp` with a real SMTP relay, or
`Email:Provider=BrevoApi`. Mailpit is deliberately absent from production
Compose.
