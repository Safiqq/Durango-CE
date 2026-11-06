#!/usr/bin/env python3
"""
Lightweight local gateway server for Durango: Community Edition (v1.0.0-CE).
Listens on http://127.0.0.1:8290 and satisfies the initial client handshake:
  - GET  /status
  - POST /accounts
  - GET  /knock
  - GET  /notice
  - POST /sessions
  - GET  /admission
  - GET  /entry
"""

import json
import sys
from http.server import HTTPServer, BaseHTTPRequestHandler

PORT = 8290
HOST = "127.0.0.1"

class DurangoGatewayHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Clean formatted console output
        sys.stdout.write(f"[Gateway] {self.command} {self.path} -> {args[0]} ({args[1]})\n")
        sys.stdout.flush()

    def send_json(self, data, status=200):
        body = json.dumps(data).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        url = self.path.split("?")[0]

        if url == "/status":
            # Durango UI cluster status probe
            self.send_json({"ok": True, "online": 1})
        elif url == "/knock":
            # Handshake / version compatibility check
            self.send_json({
                "server_version": "3.3.6",
                "compatible": True,
                "assetbundle_index_url": f"http://{HOST}:{PORT}/Info.5.2.1.json",
                "assetbundle_url_root": f"http://{HOST}:{PORT}/AssetBundles/"
            })
        elif url == "/Info.5.2.1.json":
            # Asset bundle index holder
            self.send_json({
                "PreloadHash": "c4408d73cff6e7ad670c0c93a2b04385",
                "PreloadCrc": "c4408d73cff6e7ad670c0c93a2b04385",
                "ItemList": [],
                "FileList": []
            })
        elif url == "/notice":
            # Maintenance / popup notice
            self.send_json({})
        elif url == "/admission":
            # Admission gate check
            self.send_json({"admitted": True})
        elif url == "/entry":
            # Points client to the TCP gameplay server port (e.g. 8191)
            self.send_json({
                "frontend_addresses": ["127.0.0.1:8191"],
                "cluster_mode": "Normal"
            })
        elif url.startswith("/assets/"):
            # Client Yaml.Util.Loader definition requests (71 YAML schemas)
            # Returning 404 allows the client to load complete embedded offline resources
            self.send_json({"error": "use embedded offline yaml", "path": url}, status=404)
        else:
            self.send_json({"error": "not found", "path": url}, status=404)

    def do_POST(self):
        url = self.path.split("?")[0]
        content_length = int(self.headers.get("Content-Length", 0))
        post_data = self.rfile.read(content_length)

        if url == "/accounts":
            # Return account info. An empty players list prompts character creation.
            self.send_json({
                "players": [],
                "max_player_slot_count": 4,
                "player_slot_count": 1,
                "account_id": "local_dev_account",
                "discord_name": "LocalPlayer"
            })
        elif url == "/sessions":
            # Account session token response
            self.send_json({
                "user_id": "1",
                "session_token": "local-session-token-01"
            })
        elif url == "/players":
            # Character creation response
            self.send_json({"entity_id": "1001"})
        else:
            self.send_json({"error": "not found", "path": url}, status=404)

def run():
    server_address = (HOST, PORT)
    httpd = HTTPServer(server_address, DurangoGatewayHandler)
    print(f"=====================================================")
    print(f" Durango Local Gateway Server")
    print(f" Listening on http://{HOST}:{PORT}/")
    print(f" Press Ctrl+C to stop.")
    print(f"=====================================================")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n[Gateway] Shutting down.")
        httpd.server_close()

if __name__ == "__main__":
    run()
