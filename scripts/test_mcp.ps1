<#
Tests the built MCP server through its stdio JSON-RPC interface.

Usage:
  pwsh ./scripts/test_mcp.ps1 -Configuration Release
  pwsh ./scripts/test_mcp.ps1 -Configuration Debug
  pwsh ./scripts/test_mcp.ps1 -Configuration Release -ProtocolVersion 2025-11-25

Build the MCP project in the selected configuration first.
Complete responses and diagnostics are saved to buildlog.mcp-smoke-<protocol>.log.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('2026-07-28', '2025-11-25')]
    [string]$ProtocolVersion = '2026-07-28'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$relativeExecutable = 'src/ScreenShotNet.Mcp/bin/{0}/net8.0-windows/ScreenShotNet.Mcp.exe' -f $Configuration
$executable = Join-Path $repoRoot $relativeExecutable
$logName = 'buildlog.mcp-smoke-{0}.log' -f $ProtocolVersion
$logPath = Join-Path $repoRoot $logName
if (!(Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw ('MCP executable not found. Build it first: {0}' -f $executable)
}
$executable = (Resolve-Path -LiteralPath $executable).Path

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executable
$startInfo.WorkingDirectory = $repoRoot
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$server = [Diagnostics.Process]::new()
$server.StartInfo = $startInfo
$transcript = [Collections.Generic.List[string]]::new()
$metadata = @{
    'io.modelcontextprotocol/protocolVersion' = $ProtocolVersion
    'io.modelcontextprotocol/clientInfo' = @{ name = 'ScreenShotNet-smoke-test'; version = '1.0' }
    'io.modelcontextprotocol/clientCapabilities' = @{}
}

function Invoke-McpRequest {
    param([hashtable]$Request)

    if ($ProtocolVersion -eq '2026-07-28') {
        if (!$Request.ContainsKey('params')) {
            $Request.params = @{}
        }
        if (!$Request.params.ContainsKey('_meta')) {
            $Request.params._meta = $metadata
        }
    }

    $json = $Request | ConvertTo-Json -Depth 10 -Compress
    $transcript.Add($json)
    $server.StandardInput.WriteLine($json)
    $server.StandardInput.Flush()
    if (!$Request.ContainsKey('id')) {
        return
    }

    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while ($deadline.ElapsedMilliseconds -lt 10000) {
        $remaining = [int](10000 - $deadline.ElapsedMilliseconds)
        $read = $server.StandardOutput.ReadLineAsync()
        if (!$read.Wait([Math]::Max(1, $remaining))) {
            throw ('MCP response timed out for {0}.' -f $Request.method)
        }
        if (!$read.Result) {
            throw 'MCP server closed stdout before responding.'
        }

        $transcript.Add($read.Result)
        $response = $read.Result | ConvertFrom-Json -AsHashtable
        if (!$response.ContainsKey('id')) {
            continue
        }
        if ($response.id -ne $Request.id) {
            throw 'MCP returned an unexpected response ID.'
        }
        if ($ProtocolVersion -eq '2026-07-28' -and $response.result -and $response.result.resultType -cne 'complete') {
            throw 'Modern MCP response is missing resultType=complete.'
        }
        return $response
    }
    throw ('MCP response timed out for {0}.' -f $Request.method)
}

if (!$server.Start()) {
    throw 'MCP server did not start.'
}
$stderr = $server.StandardError.ReadToEndAsync()
try {
    if ($ProtocolVersion -eq '2026-07-28') {
        $unsupportedMetadata = $metadata.Clone()
        $unsupportedMetadata['io.modelcontextprotocol/protocolVersion'] = '2099-01-01'
        $request = @{ jsonrpc = '2.0'; id = 0; method = 'server/discover'; params = @{ _meta = $unsupportedMetadata } }
        $reply = Invoke-McpRequest -Request $request
        if ($reply.error.code -ne -32022) {
            throw 'MCP did not reject the unsupported protocol version with -32022.'
        }
        Write-Host 'Unsupported protocol version was rejected with -32022.'

        $request = @{ jsonrpc = '2.0'; id = 1; method = 'server/discover'; params = @{} }
        $reply = Invoke-McpRequest -Request $request
        if ($reply.error -or $reply.result.supportedVersions -cnotcontains $ProtocolVersion) {
            throw 'MCP discovery did not advertise protocol 2026-07-28.'
        }
        Write-Host 'MCP protocol 2026-07-28 discovered without an initialize handshake.'
    }
    else {
        $request = @{
            jsonrpc = '2.0'; id = 1; method = 'initialize'
            params = @{
                protocolVersion = $ProtocolVersion; capabilities = @{}
                clientInfo = @{ name = 'ScreenShotNet-smoke-test'; version = '1.0' }
            }
        }
        $reply = Invoke-McpRequest -Request $request
        if ($reply.error -or !$reply.result.serverInfo -or $reply.result.protocolVersion -cne $ProtocolVersion) {
            throw 'MCP initialization failed to negotiate the requested protocol.'
        }
        Write-Host ('Initialized MCP version {0} with protocol {1}.' -f $reply.result.serverInfo.version, $ProtocolVersion)
        $request = @{ jsonrpc = '2.0'; method = 'notifications/initialized' }
        Invoke-McpRequest -Request $request
    }

    $request = @{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} }
    $reply = Invoke-McpRequest -Request $request
    $toolNames = @($reply.result.tools | ForEach-Object { $_.name })
    $expectedTools = @('capture_screenshot', 'capture_window_screenshot', 'capture_center_screenshot')
    if ($reply.error -or $toolNames.Count -ne $expectedTools.Count) {
        throw 'MCP did not advertise the three capture tools.'
    }
    foreach ($name in $expectedTools) {
        if ($toolNames -cnotcontains $name) {
            throw ('MCP tool missing: {0}' -f $name)
        }
    }
    Write-Host 'All three capture tools are available.'

    $request = @{
        jsonrpc = '2.0'; id = 3; method = 'tools/call'
        params = @{ name = 'capture_screenshot'; arguments = @{ x = 0; y = 0; width = 2; height = 2 } }
    }
    $reply = Invoke-McpRequest -Request $request
    $image = $reply.result.content | Where-Object { $_.type -eq 'image' } | Select-Object -First 1
    if ($reply.error -or $reply.result.isError -or !$image -or $image.mimeType -ne 'image/png') {
        throw 'MCP capture did not return a PNG image.'
    }
    $imageBytes = [Convert]::FromBase64String($image.data)
    if ($imageBytes.Length -eq 0) {
        throw 'MCP returned empty image data.'
    }
    Write-Host 'A real 2x2 capture returned image content.'

    $request = @{
        jsonrpc = '2.0'; id = 4; method = 'tools/call'
        params = @{ name = 'capture_screenshot'; arguments = @{ x = 0; y = 0; width = 10000; height = 10000 } }
    }
    $reply = Invoke-McpRequest -Request $request
    if (!$reply.result.isError -and !$reply.error) {
        throw 'MCP accepted an oversized capture.'
    }
    Write-Host 'Oversized capture was rejected.'
}
finally {
    $server.StandardInput.Close()
    if (!$server.WaitForExit(10000)) {
        $processFilter = 'ProcessId = {0}' -f $server.Id
        $child = Get-CimInstance Win32_Process -Filter $processFilter
        if ($child) {
            $owner = Invoke-CimMethod -InputObject $child -MethodName GetOwner
            $ownerName = '{0}\{1}' -f $owner.Domain, $owner.User
            $currentOwner = [Security.Principal.WindowsIdentity]::GetCurrent().Name
            $expectedName = [IO.Path]::GetFileName($executable)
            $commandMatches = $child.CommandLine -and $child.CommandLine.Trim().Trim('"') -eq $executable
            if ($child.Name -ne $expectedName -or !$commandMatches -or $child.ExecutablePath -ne $executable -or $child.ParentProcessId -ne $PID -or $ownerName -ne $currentOwner) {
                throw 'Cannot verify ownership of the unresponsive smoke-test server.'
            }
            try {
                Stop-Process -Id $server.Id -ErrorAction Stop
            }
            catch {
                if (!$server.HasExited) {
                    throw
                }
            }
        }
        if (!$server.WaitForExit(5000)) {
            throw 'Smoke-test server did not exit after termination.'
        }
        $transcript.Add($stderr.GetAwaiter().GetResult())
        $transcript | Set-Content -LiteralPath $logPath -Encoding utf8
        $server.Dispose()
        throw 'MCP server did not shut down when stdin closed.'
    }

    $exitCode = $server.ExitCode
    $transcript.Add($stderr.GetAwaiter().GetResult())
    $transcript | Set-Content -LiteralPath $logPath -Encoding utf8
    $server.Dispose()
    if ($exitCode -ne 0) {
        throw ('MCP server exited with code {0}.' -f $exitCode)
    }
}
Write-Host 'MCP smoke tests passed.'
