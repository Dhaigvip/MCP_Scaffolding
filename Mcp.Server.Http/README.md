Level 1 — Test the MCP Server Standalone (before embedding)

npx @modelcontextprotocol/inspector
```

Then point it at your running server:
```
Transport: HTTP
URL: http://localhost:5000/mcp


Option B: curl against the SSE/HTTP endpoint
Run your server, then send a raw MCP tools/call request:

# 1. List available tools
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list",
    "params": {}
  }'

# 2. Call hello.say
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: test-123" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "hello.say",
      "arguments": { "name": "Alice" }
    }
  }'


  Option C: xUnit unit tests (for the governance layer)


  POSTMAN

  {
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2024-11-05",
    "capabilities": {},
    "clientInfo": { "name": "postman", "version": "1.0" }
  }
}

Use the Mcp-Session-Id you receive from server for all next requests.
Set header Mcp-Session-Id


{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list",
  "params": {}
}

{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "hello.say",
    "arguments": { "name": "Alice" }
  }
}
