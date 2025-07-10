# PowerShell script to clean secrets from appsettings.json and commit changes

# Get current date for commit message
$currentDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

# Path to appsettings.json
$appsettingsPath = "backend\AIMcpAssistant.Api\appsettings.json"

if (Test-Path $appsettingsPath) {
    Write-Host "Cleaning secrets from $appsettingsPath..."
    
    # Read the file content
    $content = Get-Content $appsettingsPath -Raw
    
    # Replace secrets with blank values
    $content = $content -replace '"Key": "[^"]*"', '"Key": ""'
    $content = $content -replace '"ClientSecret": "[^"]*"', '"ClientSecret": ""'
    $content = $content -replace '"ApiKey": "[^"]*"', '"ApiKey": ""'
    
    # Write back to file
    $content | Set-Content $appsettingsPath -NoNewline
    
    Write-Host "Secrets cleaned successfully."
    
    # Git operations
    Write-Host "Adding files to git..."
    git add .
    
    Write-Host "Committing changes..."
    git commit -m "Clean secrets and update configuration - $currentDate"
    
    Write-Host "Pushing to remote repository..."
    git push --set-upstream origin main
    
    Write-Host "Git operations completed successfully."
} else {
    Write-Host "Error: $appsettingsPath not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Script completed successfully!" -ForegroundColor Green