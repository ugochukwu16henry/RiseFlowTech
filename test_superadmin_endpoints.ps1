param(
    [string]$Base = 'http://localhost:5221'
)

$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

Write-Host "=== Testing Super Admin Endpoints @ $Base ===" -ForegroundColor Cyan

# Login
$login = Invoke-RestMethod -Uri "$Base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{email='ugochukwuhenry16@gmail.com'; password='RiseFlow@2026!Secure'} | ConvertTo-Json) -WebSession $session
Write-Host ('Login OK: {0}, Role: {1}' -f $login.success, $login.primaryRole) -ForegroundColor Green

# Dashboard
$dash = Invoke-RestMethod -Uri "$Base/api/superadmin/dashboard" -WebSession $session
Write-Host ''
Write-Host 'Dashboard OK:' -ForegroundColor Green
Write-Host "  Total Schools: $($dash.totalSchools)" -ForegroundColor White
Write-Host "  Active Schools: $($dash.activeSchools)" -ForegroundColor White
Write-Host "  Compliance Pending: $($dash.compliancePending.Count)" -ForegroundColor White
if ($dash.compliancePending.Count -gt 0) {
    Write-Host '  Schools Pending Compliance:' -ForegroundColor Yellow
    $dash.compliancePending | ForEach-Object { Write-Host "    - $($_.name)" }
}

# Schools List
$schools = @(Invoke-RestMethod -Uri "$Base/api/superadmin/schools" -WebSession $session)
Write-Host ''
Write-Host 'Schools Endpoint OK:' -ForegroundColor Green
Write-Host "  Retrieved Count: $($schools.Count)" -ForegroundColor White
if ($schools.Count -gt 0) {
    Write-Host '  School Names:' -ForegroundColor Yellow
    $schools | ForEach-Object { Write-Host ('    - {0} (Active: {1})' -f $_.name, $_.isActive) }
}

# Revenue
$revenue = Invoke-RestMethod -Uri "$Base/api/superadmin/revenue" -WebSession $session
Write-Host ''
Write-Host 'Revenue Endpoint OK:' -ForegroundColor Green
Write-Host "  Total Schools: $($revenue.totalSchools)" -ForegroundColor White
Write-Host "  Billable Students: $($revenue.totalBillableStudents)" -ForegroundColor White
Write-Host "  Total One-Time Fees: $($revenue.totalOneTimeFees)" -ForegroundColor White
Write-Host "  Total Monthly Subscriptions: $($revenue.totalMonthlySubscriptions)" -ForegroundColor White

Write-Host ''
Write-Host '=== Super Admin verification completed successfully ===' -ForegroundColor Cyan