"""Minimal Python API for Aspire demo — health endpoint only."""

from http.server import HTTPServer, BaseHTTPRequestHandler
import json
import os


class HealthHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/health":
            self._respond(200, {"status": "healthy"})
        elif self.path == "/":
            self._respond(200, {"service": "python-api", "status": "running"})
        else:
            self._respond(404, {"error": "not found"})

    def _respond(self, status: int, body: dict):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.write(json.dumps(body).encode())

    def write(self, data: bytes):
        self.wfile.write(data)

    def log_message(self, format, *args):
        print(f"[python-api] {args[0]}")


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "5100"))
    server = HTTPServer(("0.0.0.0", port), HealthHandler)
    print(f"Python API listening on port {port}")
    server.serve_forever()
