Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

try {
  $migrationName = "GitHubIssueQuery"
  $targetProject = "..\DevOps.Util.Triage\DevOps.Util.Triage.csproj"
  $env:RUNFO_DEV = 1

  [string]$output = & dotnet ef migrations list
  if ($output.Contains($migrationName)) {
    Write-Host "Removing old version of migration"
    & dotnet ef migrations remove --project $targetProject
  }

  Write-Host "Creating Migration"
  & dotnet ef migrations add $migrationName --project $targetProject

  Write-Host "Updating Database"
  & dotnet ef database update --project $targetProject 
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
}