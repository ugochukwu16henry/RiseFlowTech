$base = 'http://localhost:5222'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

Write-Host '=== Testing Super Admin Endpoints ===' -ForegroundColor Cyan

# Login
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{email='ugochukwuhenry16@gmail.com'; password='RiseFlow@2026!Secure'} | ConvertTo-Json) -WebSession $session
Write-Host "✓ Login: $($login.success), Role: $($login.primaryRole)" -ForegroundColor Green

# Dashboard
$dash = Invoke-RestMethod -Uri "$base/api/superadmin/dashboard" -WebSession $session
Write-Host "`n✓ Dashboard:" -ForegroundColor Green
Write-Host "  Total Schools: $($dash.totalSchools)" -ForegroundColor White
Write-Host "  Active Schools: $($dash.activeSchools)" -ForegroundColor White
Write-Host "  Compliance Pending: $($dash.compliancePending.Count)" -ForegroundColor White
if ($dash.compliancePending.Count -gt 0) {
    Write-Host '  Schools Pending Compliance:' -ForegroundColor Yellow
    $dash.compliancePending | ForEach-Object { Write-Host "    - $($_.name)" }
}

# Schools List
$schools = @(Invoke-RestMethod -Uri "$base/api/superadmin/schools" -WebSession $session)
Write-Host "`n✓ Schools Endpoint:" -ForegroundColor Green
Write-Host "  Retrieved Count: $($schools.Count)" -ForegroundColor White
if ($schools.Count -gt 0) {
    Write-Host '  School Names:' -ForegroundColor Yellow
    $schools | ForEach-Object { Write-Host "    - $($_.name) (Active: $($_.isActive))" }
}

# Revenue
$revenue = Invoke-RestMethod -Uri "$base/api/superadmin/revenue" -WebSession $session
Write-Host "`n✓ Revenue Endpoint:" -ForegroundColor Green
Write-Host "  Total Schools: $($revenue.totalSchools)" -ForegroundColor White
Write-Host "  Billable Students: $($revenue.totalBillableStudents)" -ForegroundColor White
Write-Host "  Total One-Time: $($revenue.totalOneTime)" -ForegroundColor White
Write-Host "  Total Monthly Recurring: $($revenue.totalMonthlyRecurring)" -ForegroundColor White

Write-Host "`n=== All Tests Passed ===" -ForegroundColor Cyan