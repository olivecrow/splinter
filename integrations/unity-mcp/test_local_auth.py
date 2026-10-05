"""No Editor or live server: exercise auth before any route is dispatched."""
import asyncio
import os
import unittest
from unittest.mock import patch
from local_auth import LocalAuthMiddleware, require_token


class AuthTests(unittest.TestCase):
    token = '12' * 32

    def request(self, kind, headers, path='/mcp'):
        dispatched = []
        responses = []

        async def app(scope, receive, send):
            dispatched.append(scope['path'])

        async def send(value):
            responses.append(value)

        asyncio.run(LocalAuthMiddleware(app, self.token)(
            {'type': kind, 'path': path, 'headers': headers}, None, send))
        return dispatched, responses

    def test_missing_or_wrong_http_token_never_dispatches_any_route(self):
        for path in ('/mcp', '/api/instances', '/api/command', '/health'):
            for headers in ([], [(b'authorization', b'Bearer wrong')]):
                dispatched, responses = self.request('http', headers, path)
                self.assertFalse(dispatched)
                self.assertEqual(responses[0]['status'], 401)

    def test_editor_socket_requires_the_same_private_token(self):
        for headers in ([], [(b'x-api-key', b'wrong')]):
            dispatched, responses = self.request('websocket', headers, '/hub/plugin')
            self.assertFalse(dispatched)
            self.assertEqual(responses[0]['code'], 1008)
        dispatched, responses = self.request('websocket', [(b'x-api-key', self.token.encode())], '/hub/plugin')
        self.assertEqual(dispatched, ['/hub/plugin'])
        self.assertFalse(responses)

    def test_duplicate_headers_and_browser_origins_are_rejected(self):
        authorization = (b'authorization', b'Bearer ' + self.token.encode())
        for headers in ([authorization, authorization], [authorization, (b'origin', b'http://localhost')]):
            dispatched, _ = self.request('http', headers)
            self.assertFalse(dispatched)

    def test_authorized_http_is_forwarded(self):
        dispatched, responses = self.request('http', [(b'authorization', b'Bearer ' + self.token.encode())])
        self.assertEqual(dispatched, ['/mcp'])
        self.assertFalse(responses)

    def test_startup_rejects_absent_or_weak_configuration(self):
        for token in ('', 'short', 'x' * 64):
            with patch.dict(os.environ, {'SPLINTER_MCP_TOKEN': token}):
                with self.assertRaises(ValueError):
                    require_token()
        with patch.dict(os.environ, {'SPLINTER_MCP_TOKEN': self.token}):
            self.assertEqual(require_token(), self.token)


if __name__ == '__main__':
    unittest.main()
