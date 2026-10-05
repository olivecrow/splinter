"""Validate FastMCP's actual outer HTTP/WS middleware without Unity/network IO."""
import unittest
import asyncio
import os
import pathlib
import sys
from unittest.mock import patch
import httpx
from fastmcp import FastMCP
from starlette.middleware import Middleware
from starlette.responses import JSONResponse
from starlette.routing import WebSocketRoute
from starlette.testclient import TestClient
from starlette.websockets import WebSocketDisconnect
from local_auth import LocalAuthMiddleware

prepared = pathlib.Path(sys.argv.pop(1)).resolve()
sys.path.insert(0, str(prepared))
from cli.utils import connection
from cli.utils.config import CLIConfig


class TransportAuthTests(unittest.TestCase):
    token = '34' * 32

    def app(self):
        server = FastMCP('auth-regression')

        @server.custom_route('/api/instances', methods=['GET'])
        async def instances(request):
            return JSONResponse({'authorized': True, 'instances': []})

        async def plugin(websocket):
            await websocket.accept()
            await websocket.send_text('authorized')
            await websocket.close()

        server._additional_http_routes.append(WebSocketRoute('/hub/plugin', plugin))
        return server.http_app(middleware=[Middleware(LocalAuthMiddleware, token=self.token)])

    def test_mcp_and_additional_routes_are_guarded_before_dispatch(self):
        with TestClient(self.app(), base_url='http://127.0.0.1') as client:
            self.assertEqual(client.post('/mcp').status_code, 401)
            self.assertEqual(client.get('/api/instances').status_code, 401)
            response = client.get('/api/instances', headers={'Authorization': 'Bearer ' + self.token})
            self.assertEqual(response.status_code, 200)
            self.assertTrue(response.json()['authorized'])

    def test_plugin_connection_is_guarded_before_accept(self):
        with TestClient(self.app(), base_url='http://127.0.0.1') as client:
            with self.assertRaises(WebSocketDisconnect) as denial:
                with client.websocket_connect('/hub/plugin'):
                    self.fail('Unauthenticated plugin accepted')
            self.assertEqual(denial.exception.code, 1008)
            with client.websocket_connect('/hub/plugin', headers={'X-API-Key': self.token}) as socket:
                self.assertEqual(socket.receive_text(), 'authorized')

    def test_prepared_cli_uses_auth_and_never_sends_it_to_another_host(self):
        original = httpx.AsyncClient
        async def request():
            def client(**kwargs):
                return original(transport=httpx.ASGITransport(app=self.app()), **kwargs)
            with patch.dict(os.environ, {'SPLINTER_MCP_TOKEN': self.token}):
                with patch.object(connection.httpx, 'AsyncClient', client):
                    response = await connection.list_unity_instances(CLIConfig())
                    self.assertTrue(response['authorized'])
                    with self.assertRaises(connection.UnityConnectionError):
                        connection.local_client(CLIConfig(host='example.com'))
        asyncio.run(request())


if __name__ == '__main__':
    unittest.main()
