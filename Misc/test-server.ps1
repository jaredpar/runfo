param ([switch]$start, [switch]$stop)

if ($start) {
  & docker pull jaredpar/runfo:v0.1.0
  & docker run --rm -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=password@0" -p 1433:1433 --name sql-runfo-test -h sql1 -d jaredpar/runfo:v0.1.0 
}

if ($stop) {
  & docker kill sql-runfo-test
}