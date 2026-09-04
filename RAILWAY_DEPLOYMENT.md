# Railway deployment checklist

## API service

1. Push the `MeetingScheduler.API` folder to the GitHub repository connected to the Railway API service.
2. In Railway, open the API service, then **Variables**, and add these values. Use the double underscore exactly as shown.

| Variable | Value |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Your Railway MySQL connection string |
| `Jwt__Key` | A new random secret, at least 32 characters |
| `Jwt__Issuer` | `SyncUpAPI` |
| `Jwt__Audience` | `SyncUpClient` |
| `Email__SmtpHost` | `smtp.gmail.com` |
| `Email__SmtpPort` | `587` |
| `Email__SenderEmail` |  guptavaidehi06@gmail.com|
| `Email__SenderPassword` | lfvx owbb diuy obex |
| `Email__SenderName` | `SyncUp` |
| `Client__BaseUrl` | The public URL of the deployed Angular app, without a trailing slash |

3. Deploy. Railway will supply `PORT` automatically; the API listens on it.
4. Open `https://YOUR-API-DOMAIN/health`. The expected response is `{ "status": "ok" }`.
5. If you have new EF migrations, run `dotnet ef database update` against the production connection string once before testing sign-up.

## Client service

1. Deploy the `meeting-scheduler-client` repository/folder as a separate Railway static site or web service.
2. Build command: `npm ci && npm run build`
3. Publish directory: `dist/meeting-scheduler-client/browser`
4. Confirm the API URL in `src/app/services/*.ts` matches the public Railway API domain.
5. Return to the API service and set `Client__BaseUrl` to this client URL, then redeploy the API. Invitation emails will then contain the public link instead of `localhost`.

## Email check

Create a test account, then use **Resend code**. If it fails, Railway deployment logs now contain the SMTP error. The common causes are a revoked Gmail app password, 2-Step Verification disabled on the Gmail account, or a typo in `Email__SenderEmail` / `Email__SenderPassword`.
