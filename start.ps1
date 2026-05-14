# Car Budget Manager - Startup Script

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

# Start the backend API
Write-Host "Starting Backend API..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\src\CarBudget.Api'; Write-Host 'Backend API Starting...' -ForegroundColor Yellow; dotnet run"

# Wait for the API to start (database initialization takes time on first run)
Start-Sleep -Seconds 8

# Start the frontend
Write-Host "Starting Frontend..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\frontend'; Write-Host 'Frontend Starting...' -ForegroundColor Yellow; & 'C:\Program Files\nodejs\npm.cmd' start"

Write-Host ""
Write-Host "Both services are starting!" -ForegroundColor Green
Write-Host ""
if ($dbProvider -ieq "PostgreSql" -or $dbProvider -ieq "Postgres") {
	Write-Host "PostgreSQL is expected at: localhost:5432 (database: carbudget)" -ForegroundColor White
}
else {
	Write-Host "SQLite database file: src/CarBudget.Api/carbudget.db" -ForegroundColor White
}
Write-Host "Backend API will be available at: http://localhost:5000" -ForegroundColor White
Write-Host "OpenAPI document will be available at: http://localhost:5000/openapi/v1.json" -ForegroundColor White
Write-Host "Frontend will open automatically at: http://localhost:2233" -ForegroundColor White
Write-Host ""
Write-Host "Keeping window open. Close when finished." -ForegroundColor Gray
