# Roles and account setup

## Roles

- **Leadership** is the administrator role. Leadership users can access reporting/dashboard functions and create volunteer accounts.
- **Volunteer** is for approved staff/volunteers who capture donors and donations and perform the operational tasks permitted by the application.
- New public self-registration is intentionally not enabled. This application handles donor personal information and financial records, so allowing any visitor to create an account would make it easier for unauthorized users to gain access. Leadership should create an account only after confirming the volunteer's identity/authorization.

## First Leadership sign-in

For local development only, when no Leadership account exists and no bootstrap credentials are configured, "IdentitySeed" creates:

- Email: `leader@peniel.local`
- Password: `Dev-Only-Change-Me-123!`

This is a development convenience, not a production credential. Do not deploy with the documented development password. For a controlled first account, configure these environment variables before starting the app:

```powershell
$env:BootstrapAdmin__Email = "your-approved-admin-email@example.org"
$env:BootstrapAdmin__Password = "Use-A-Unique-Strong-Password-Here!"
dotnet run --project Backend/PenielPlaceMinistries.csproj
```

The password must satisfy the configured Identity policy: at least 12 characters with uppercase, lowercase, digit, and non-alphanumeric character. Environment variables are examples; use your own unique credentials. After setup, remove the environment variables from the shell or secret store when appropriate. Do not put real credentials in "appsettings.json", source control, screenshots, or documentation.

## Create a volunteer

1. Sign in as Leadership using the configured administrator email/password.
2. Complete authenticator-app setup on the first sign-in; two-factor authentication is required before access is granted.
3. Open **Create Volunteer** from the leadership navigation or browse to "/Account/CreateVolunteer".
4. Enter the volunteer's display name, email and a unique temporary password that meets the password policy.
5. Share the temporary password with the volunteer through a separate, secure channel. Do not send it in a public group chat or include it in source control.
6. The volunteer signs in with that email and temporary password, then enrolls their authenticator app before accessing the application.
