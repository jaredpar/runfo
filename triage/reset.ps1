Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

try {
  $targetProject = "..\DevOps.Util.Triage\DevOps.Util.Triage.csproj"

  Write-Host "Creating Migration"
  Remove-Item -Recurse ..\DevOps.Util.Triage\Migrations -ErrorAction SilentlyContinue
  & dotnet ef migrations add InitialCreate --project $targetProject
  
  $dbPath = "C:\Users\jaredpar\AppData\Local\runfo\triage.db"
  if (Test-Path $dbPath) {
    Remove-Item $dbPath
  }

  Write-Host "Creating Database"
  & dotnet ef database update --project $targetProject 
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