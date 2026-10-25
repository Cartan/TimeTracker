<#
.SYNOPSIS
    Gives a person access to TimeTracker as their own tenant.

.DESCRIPTION
    1. Invites the person to the Entra tenant if they are not a member yet.
    2. Assigns them to the TimeTracker-web enterprise application (assignment is required to sign in).
    3. Adds their login to the Users table of the Azure SQL database, which creates their tenant.

    Every step is skipped when it has already been done, so the script can be run again safely.
    Requires the Azure CLI, signed in (az login) as someone who can manage users and app assignments in
    Entra and is an Entra administrator of the SQL server. Your public IP must be allowed by the SQL server firewall.

.PARAMETER Login
    The sign-in name the app sees in X-MS-CLIENT-PRINCIPAL-NAME. For most people this is their email address.

.EXAMPLE
    ./AddUser.ps1 -Login new.person@example.com

.EXAMPLE
    ./AddUser.ps1 -Login new.person@example.com -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[^@\s'']+@[^@\s'']+$')]
    [string]$Login,

    [string]$AppDisplayName = 'TimeTracker-web',
    [string]$SqlServer = 'nils-timetracker-server.database.windows.net',
    [string]$Database = 'TimeTracker',
    [string]$InviteRedirectUrl = 'https://nils-timetracker-web.azurewebsites.net',
    [switch]$SkipInvite
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Login = $Login.Trim()
$graph = 'https://graph.microsoft.com/v1.0'
# The built-in "Default Access" role, used for apps that do not define app roles.
$defaultAppRoleId = '00000000-0000-0000-0000-000000000000'

function Invoke-Az {
    $output = & az @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($args -join ' ') failed: $output"
    }
    $output
}

function Invoke-Graph {
    param([string]$Method = 'GET', [string]$Path, [string]$Filter, $Body)

    $azArgs = @('rest', '--method', $Method, '--url', "$graph$Path", '-o', 'json')
    if ($Filter) {
        $azArgs += @('--url-parameters', "`$filter=$Filter")
    }

    $bodyFile = $null
    try {
        if ($Body) {
            $bodyFile = New-TemporaryFile
            Set-Content -Path $bodyFile -Value ($Body | ConvertTo-Json -Compress)
            $azArgs += @('--headers', 'Content-Type=application/json', '--body', "@$bodyFile")
        }
        $json = Invoke-Az @azArgs
        if ($json) { $json | ConvertFrom-Json }
    }
    finally {
        if ($bodyFile) { Remove-Item $bodyFile -ErrorAction SilentlyContinue }
    }
}

$escapedLogin = $Login.Replace("'", "''")

Write-Host "Checking Azure CLI sign-in..."
$account = Invoke-Az account show --query '{user:user.name, tenant:tenantId}' -o json | ConvertFrom-Json
Write-Host "Signed in as $($account.user)"

# 1. Entra user
Write-Host "`n[1/3] Entra user"
$found = Invoke-Graph -Path '/users' -Filter "mail eq '$escapedLogin' or userPrincipalName eq '$escapedLogin'"
$entraUser = $found.value | Select-Object -First 1

if ($entraUser) {
    Write-Host "  Already in the tenant: $($entraUser.userPrincipalName)"
}
elseif ($SkipInvite) {
    throw "$Login is not in the tenant and -SkipInvite was given."
}
elseif ($PSCmdlet.ShouldProcess($Login, 'Invite to the Entra tenant')) {
    $invitation = Invoke-Graph -Method POST -Path '/invitations' -Body @{
        invitedUserEmailAddress = $Login
        inviteRedirectUrl       = $InviteRedirectUrl
        sendInvitationMessage   = $true
    }
    $entraUser = $invitation.invitedUser
    Write-Host "  Invitation sent to $Login"
}

# 2. Enterprise application assignment
Write-Host "`n[2/3] Access to '$AppDisplayName'"
$escapedApp = $AppDisplayName.Replace("'", "''")
$servicePrincipal = (Invoke-Graph -Path '/servicePrincipals' -Filter "displayName eq '$escapedApp'").value | Select-Object -First 1
if (-not $servicePrincipal) {
    throw "Enterprise application '$AppDisplayName' was not found."
}

if (-not $entraUser) {
    Write-Host "  Skipped (the user was not invited in this run)."
}
else {
    $assignments = Invoke-Graph -Path "/servicePrincipals/$($servicePrincipal.id)/appRoleAssignedTo"
    $alreadyAssigned = $assignments.value | Where-Object { $_.principalId -eq $entraUser.id }

    if ($alreadyAssigned) {
        Write-Host "  Already assigned."
    }
    elseif ($PSCmdlet.ShouldProcess($Login, "Assign to '$AppDisplayName'")) {
        $null = Invoke-Graph -Method POST -Path "/servicePrincipals/$($servicePrincipal.id)/appRoleAssignedTo" -Body @{
            principalId = $entraUser.id
            resourceId  = $servicePrincipal.id
            appRoleId   = $defaultAppRoleId
        }
        Write-Host "  Assigned."
    }
}

# 3. Users table
Write-Host "`n[3/3] Users table in $Database"
if ($PSCmdlet.ShouldProcess("$SqlServer/$Database", "Add '$Login' to Users")) {
    $token = (Invoke-Az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv) | Out-String
    $connection = New-Object System.Data.SqlClient.SqlConnection "Server=tcp:$SqlServer,1433;Initial Catalog=$Database;Encrypt=True;Connect Timeout=120;"
    $connection.AccessToken = $token.Trim()

    try {
        # A paused serverless database resumes on the first connection, which can take about a minute.
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'INSERT INTO Users (Login) SELECT @login WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Login = @login)'
        $null = $command.Parameters.AddWithValue('@login', $Login)
        $added = $command.ExecuteNonQuery()
        Write-Host ($(if ($added -gt 0) { "  Added." } else { "  Already present." }))
    }
    catch {
        if ($_.Exception.Message -match 'firewall|40615|Client with IP address') {
            Write-Warning "The SQL server firewall is blocking this machine. Add your IP with: az sql server firewall-rule create -g <resource-group> -s $($SqlServer.Split('.')[0]) -n <name> --start-ip-address <ip> --end-ip-address <ip>"
        }
        throw
    }
    finally {
        $connection.Dispose()
    }
}

if ($WhatIfPreference) {
    Write-Host "`nNothing was changed (-WhatIf)."
}
else {
    Write-Host "`nDone. $Login can sign in to the app and works in their own tenant."
}
