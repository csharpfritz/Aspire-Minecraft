// Minimal Node.js API for Aspire demo — health endpoint only.

const http = require("http");

const port = process.env.PORT || 5200;

const server = http.createServer((req, res) => {
  res.setHeader("Content-Type", "application/json");

  if (req.url === "/health") {
    res.writeHead(200);
    res.end(JSON.stringify({ status: "healthy" }));
  } else if (req.url === "/") {
    res.writeHead(200);
    res.end(JSON.stringify({ service: "node-api", status: "running" }));
  } else {
    res.writeHead(404);
    res.end(JSON.stringify({ error: "not found" }));
  }
});

server.listen(port, "0.0.0.0", () => {
  console.log(`Node API listening on port ${port}`);
});
