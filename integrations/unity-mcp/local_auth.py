"""Authenticate every local HTTP/WS route before it reaches the MCP server."""
import hmac
import os
import re


def require_token():
    token = os.environ.get('SPLINTER_MCP_TOKEN', '')
    if not re.fullmatch(r'[0-9a-fA-F]{64}', token):
        raise ValueError('Set SPLINTER_MCP_TOKEN to a private random 32-byte hex token')
    return token


class LocalAuthMiddleware:
    def __init__(self, app, token):
        self.app = app
        self.token = token.encode('ascii')

    async def __call__(self, scope, receive, send):
        if scope['type'] not in ('http', 'websocket'):
            return await self.app(scope, receive, send)
        headers = scope.get('headers', [])
        # MCP uses Bearer; the Unity plugin sends its existing X-API-Key header.
        name = b'x-api-key' if scope['type'] == 'websocket' else b'authorization'
        values = [v for k, v in headers if k.lower() == name]
        candidate = values[0] if len(values) == 1 else b''
        if scope['type'] == 'http':
            candidate = candidate[7:] if candidate.startswith(b'Bearer ') else b''
        # This local desktop endpoint does not serve browser-origin requests.
        origin = any(k.lower() == b'origin' for k, _ in headers)
        if not origin and hmac.compare_digest(candidate, self.token):
            return await self.app(scope, receive, send)
        if scope['type'] == 'websocket':
            await send({'type': 'websocket.close', 'code': 1008})
        else:
            await send({'type': 'http.response.start', 'status': 401,
                        'headers': [(b'www-authenticate', b'Bearer'), (b'content-type', b'text/plain')]})
            await send({'type': 'http.response.body', 'body': b'Unauthorized'})
