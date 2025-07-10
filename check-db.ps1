# Navigate to the API project directory
Set-Location "backend\AIMcpAssistant.Api"

# Check if database file exists
if (Test-Path "aimcp.db") {
    Write-Host "Database file exists" -ForegroundColor Green
    
    # Get file size
    $dbFile = Get-Item "aimcp.db"
    Write-Host "Database size: $($dbFile.Length) bytes" -ForegroundColor Yellow
} else {
    Write-Host "Database file does not exist!" -ForegroundColor Red
}

# Try to run a simple EF command to check database
Write-Host "\nChecking database with EF Core..." -ForegroundColor Cyan
try {
    dotnet ef database update --verbose
} catch {
    Write-Host "Error with EF database update: $_" -ForegroundColor Red
}

# Go back to root
Set-Location "..\.."