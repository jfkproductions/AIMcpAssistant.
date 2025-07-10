# Check user subscriptions and modules in database
Write-Host "Checking database for user 112766686483169100008..."

# Check if sqlite3 is available
try {
    sqlite3 -version
    Write-Host "SQLite3 is available"
} catch {
    Write-Host "SQLite3 not found, trying with dotnet ef"
}

# Query modules
Write-Host "\n=== MODULES ==="
sqlite3 aimcp.db "SELECT ModuleId, Name, IsEnabled, IsRegistered FROM Modules;"

# Query user subscriptions
Write-Host "\n=== USER SUBSCRIPTIONS ==="
sqlite3 aimcp.db "SELECT UserId, ModuleId, IsSubscribed, CreatedAt, UpdatedAt FROM UserModuleSubscriptions WHERE UserId = '112766686483169100008';"

# Query all user subscriptions
Write-Host "\n=== ALL USER SUBSCRIPTIONS ==="
sqlite3 aimcp.db "SELECT UserId, ModuleId, IsSubscribed FROM UserModuleSubscriptions;"

# Query users
Write-Host "\n=== USERS ==="
sqlite3 aimcp.db "SELECT UserId, Email, Name FROM Users;"

Write-Host "\nDone."