Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

try {
  $targetProject = "..\DevOps.Util.Triage\DevOps.Util.Triage.csproj"

  if ((-not (Test-Path "env:\RUNFO_USE_SQLITE")) -or ($env:RUNFO_USE_SQLITE -eq "")) {
    Write-Host "Must be setup for SQLITE to run this script"
    exit 1
  }

  Write-Host "Creating Migration"
  Remove-Item -Recurse ..\DevOps.Util.Triage\Migrations\Sqlite -ErrorAction SilentlyContinue
  & dotnet ef migrations add ExpandQuery --project $targetProject -o "Migrations\Sqlite"
  
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