$dbPath = "C:\Users\jaredpar\AppData\Local\runfo\triage.db"

if (Test-Path $dbPath) {
  Remove-Item $dbPath
}

Remove-Item -Recurse Migrations
& dotnet ef migrations add InitialCreate
& dotnet ef database update