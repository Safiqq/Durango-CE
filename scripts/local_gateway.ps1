# Durango: Community Edition Local Gateway Server for Windows PowerShell (Zero Dependencies)
# Run inside Windows PowerShell: .\local_gateway.ps1

$port = 8290
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://127.0.0.1:$port/")

try {
    $listener.Start()
} catch {
    Write-Host "[!] Failed to start listener on port $port : $_" -ForegroundColor Red
    Exit 1
}

Write-Host "=====================================================" -ForegroundColor Green
Write-Host " Durango Local Gateway Server (PowerShell Edition)" -ForegroundColor Green
Write-Host " Listening on http://127.0.0.1:$port/" -ForegroundColor Green
Write-Host " Press Ctrl+C to stop." -ForegroundColor Yellow
Write-Host "=====================================================" -ForegroundColor Green

while ($listener.IsListening) {
    try {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response

        $method = $request.HttpMethod
        $rawUrl = $request.Url.LocalPath
        Write-Host "[Gateway] $method $rawUrl" -ForegroundColor Cyan

        $body = "{}"
        $statusCode = 200

        if ($method -eq "GET") {
            if ($rawUrl -eq "/status") {
                $body = '{"ok": true, "online": 1}'
            } elseif ($rawUrl -eq "/knock") {
                $body = '{"server_version": "3.3.6", "compatible": true, "assetbundle_index_url": "http://127.0.0.1:8290/Info.5.2.1.json", "assetbundle_url_root": "http://127.0.0.1:8290/AssetBundles/"}'
            } elseif ($rawUrl -eq "/Info.5.2.1.json") {
                $body = '{"PreloadHash": "c4408d73cff6e7ad670c0c93a2b04385", "PreloadCrc": "c4408d73cff6e7ad670c0c93a2b04385", "ItemList": [], "FileList": []}'
            } elseif ($rawUrl -eq "/notice") {
                $body = '{}'
            } elseif ($rawUrl -eq "/admission") {
                $body = '{"admitted": true}'
            } elseif ($rawUrl -eq "/entry") {
                $body = '{"frontend_addresses": ["127.0.0.1:8191"], "cluster_mode": "Normal"}'
            } elseif ($rawUrl.StartsWith("/assets/")) {
                $statusCode = 404
                $body = '{"error": "use embedded offline yaml"}'
            } else {
                $statusCode = 404
                $body = '{"error": "not found"}'
            }
        } elseif ($method -eq "POST") {
            if ($rawUrl -eq "/accounts") {
                $body = '{"players": [], "max_player_slot_count": 4, "player_slot_count": 1, "account_id": "local_dev", "discord_name": "Dev"}'
            } elseif ($rawUrl -eq "/sessions") {
                $body = '{"user_id": "1", "session_token": "local-session-01"}'
            } elseif ($rawUrl -eq "/players") {
                $body = '{"entity_id": "1001"}'
            } else {
                $statusCode = 404
                $body = '{"error": "not found"}'
            }
        }

        $buffer = [System.Text.Encoding]::UTF8.GetBytes($body)
        $response.StatusCode = $statusCode
        $response.ContentType = "application/json; charset=utf-8"
        $response.ContentLength64 = $buffer.Length
        $response.OutputStream.Write($buffer, 0, $buffer.Length)
        $response.OutputStream.Close()
    } catch [System.Net.HttpListenerException] {
        if (-not $listener.IsListening) { break }
        Write-Host "[!] Listener warning: $_" -ForegroundColor DarkYellow
    } catch {
        if (-not $listener.IsListening) { break }
        Write-Host "[!] Request error: $_" -ForegroundColor DarkYellow
    }
}

$listener.Stop()
Write-Host "[Gateway] Server stopped." -ForegroundColor Yellow
