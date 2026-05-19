# Car Budget Manager - Startup Script
param(
	[switch]$Tray
)

Write-Host "Starting Car Budget Manager..." -ForegroundColor Green
Write-Host ""

# Database provider mode (default: SQLite)
$dbProvider = if ([string]::IsNullOrWhiteSpace($env:CARBUDGET_DB_PROVIDER)) { "Sqlite" } else { $env:CARBUDGET_DB_PROVIDER }
Write-Host "Database provider: $dbProvider" -ForegroundColor White

if ($dbProvider -ieq "PostgreSql" -or $dbProvider -ieq "Postgres") {
	# Start local PostgreSQL (Docker)
	$pgContainerName = "carbudget-postgres"
	$pgUser = "postgres"
	$pgPassword = "postgres"
	$pgDatabase = "carbudget"
	$pgPort = 5432

	Write-Host "Starting PostgreSQL..." -ForegroundColor Cyan
	$dockerCommand = Get-Command docker -ErrorAction SilentlyContinue

	if ($null -eq $dockerCommand) {
		Write-Warning "Docker was not found. Start PostgreSQL manually on localhost:5432."
	}
	else {
		docker info | Out-Null 2>&1
		if ($LASTEXITCODE -ne 0) {
			Write-Warning "Docker is installed but not running. Start Docker Desktop and run the script again."
		}
		else {
			$existingContainer = docker ps -a --filter "name=^/$pgContainerName$" --format "{{.Names}}"

			if ([string]::IsNullOrWhiteSpace($existingContainer)) {
				Write-Host "Creating PostgreSQL container..." -ForegroundColor Yellow
				docker run --name $pgContainerName -e POSTGRES_USER=$pgUser -e POSTGRES_PASSWORD=$pgPassword -e POSTGRES_DB=$pgDatabase -p "$pgPort:5432" -d postgres:16-alpine | Out-Null
			}
			else {
				$runningContainer = docker ps --filter "name=^/$pgContainerName$" --format "{{.Names}}"
				if ([string]::IsNullOrWhiteSpace($runningContainer)) {
					Write-Host "Starting existing PostgreSQL container..." -ForegroundColor Yellow
					docker start $pgContainerName | Out-Null
				}
			}

			Write-Host "Waiting for PostgreSQL to become ready..." -ForegroundColor Yellow
			$ready = $false
			for ($i = 1; $i -le 30; $i++) {
				docker exec $pgContainerName pg_isready -U $pgUser -d $pgDatabase | Out-Null 2>&1
				if ($LASTEXITCODE -eq 0) {
					$ready = $true
					break
				}
				Start-Sleep -Seconds 1
			}

			if ($ready) {
				Write-Host "PostgreSQL is ready." -ForegroundColor Green
			}
			else {
				Write-Warning "PostgreSQL did not become ready in time. API startup may fail."
			}
		}
	}
}
else {
	Write-Host "Using embedded SQLite file database for self-hosted mode." -ForegroundColor Green
}

# Build frontend assets into API wwwroot for single-port mode
Write-Host "Building Frontend for single-port mode..." -ForegroundColor Cyan
$npmCommand = Get-Command npm -ErrorAction SilentlyContinue

if ($null -eq $npmCommand) {
	Write-Error "npm was not found in PATH. Install Node.js or add npm to PATH, then run this script again."
	return
}

Push-Location "$PSScriptRoot\frontend"
& npm run build
$buildExitCode = $LASTEXITCODE
Pop-Location

if ($buildExitCode -ne 0) {
	Write-Error "Frontend build failed. Backend was not started."
	return
}

# Start only the backend API (serves both UI and API on port 2233)
Write-Host "Starting Backend (UI + API on one port)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\src\CarBudget.Api'; Write-Host 'Backend Starting (single-port mode)...' -ForegroundColor Yellow; dotnet run"

if ($Tray) {
	Write-Host "Starting Tray App..." -ForegroundColor Cyan
	Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\frontend'; Write-Host 'Tray App Starting...' -ForegroundColor Yellow; npm run electron-tray"
}

Write-Host ""
Write-Host "Single-port service is starting!" -ForegroundColor Green
Write-Host ""
if ($dbProvider -ieq "PostgreSql" -or $dbProvider -ieq "Postgres") {
	Write-Host "PostgreSQL is expected at: localhost:5432 (database: carbudget)" -ForegroundColor White
}
else {
	Write-Host "SQLite database file: src/CarBudget.Api/carbudget.db" -ForegroundColor White
}
Write-Host "Web UI will be available at: http://localhost:2233" -ForegroundColor White
Write-Host "Backend API will be available at: http://localhost:2233/api" -ForegroundColor White
Write-Host "OpenAPI document will be available at: http://localhost:2233/openapi/v1.json" -ForegroundColor White
if ($Tray) {
	Write-Host "Tray app started. Use system tray icon to open/hide/quit." -ForegroundColor White
}
Write-Host ""
Write-Host "Keeping window open. Close when finished." -ForegroundColor Gray
