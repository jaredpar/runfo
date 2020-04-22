Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Push-Location ..\DevOps.Util.Triage
try {

  Write-Host "Creating Migration"
  Remove-Item -Recurse Migrations -ErrorAction SilentlyContinue
  & dotnet ef migrations add InitialCreate
  
  $dbPath = "C:\Users\jaredpar\AppData\Local\runfo\triage.db"
  if (Test-Path $dbPath) {
    Remove-Item $dbPath
  }

  Write-Host "Creating Database"
  & dotnet ef database update
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}
finally {
  Pop-Location
}