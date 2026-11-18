# DP World Algeria Access Management

## Development setup

The application targets .NET 10 and uses ASP.NET Core Identity with EF Core and SQL Server LocalDB. Install SQL Server LocalDB, then configure the first Super Admin credentials before starting the application. Do not commit credentials to source control.

In PowerShell, set the bootstrap values for the current process and start the project:

```powershell
$env:BootstrapAdmin__UserName = "your-super-admin"
$env:BootstrapAdmin__Password = "Use-a-unique-12-character-password!"
dotnet run --project Endy\Endy.csproj
```

The password must be at least 12 characters and include uppercase, lowercase, a digit, and a non-alphanumeric character. The application applies EF Core migrations at startup, seeds the four roles and the initial department list, and creates the configured Super Admin if it does not exist. Override `ConnectionStrings__DefaultConnection` to use another SQL Server instance. Configure both bootstrap settings or neither; startup fails with a clear error if there is no active Super Admin.

## Roles and workflow

- **Super Admin** creates and manages accounts, assigns one of the four roles and departments, suspends accounts, and resets passwords.
- **Admin** reviews requests created by employees, cannot approve their own, and can print permits only for approved requests. Admins can monitor customer tracking but cannot register check-in or check-out.
- **User** creates and manages only their own requests. The request department is taken from the employee account.
- **Security User** records check-in and check-out for approved visitors. The visit timer starts at check-in and ends at check-out.

Request events and snapshots, notifications, accounts, visits, and departments are persisted in SQL Server. Reports can be printed or exported as CSV for Excel.
