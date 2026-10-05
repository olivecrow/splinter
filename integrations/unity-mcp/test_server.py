"""Focused tests against the prepared external server using its installed runtime."""
import asyncio
import importlib
import pathlib
import sys
import unittest
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

sys.path.insert(0, sys.argv.pop(1))
module = importlib.import_module('services.tools.execute_code')


class ServerTests(unittest.TestCase):
    def call(self, response=None, **kwargs):
        sender = AsyncMock(return_value=response or {'success': True, 'data': {'job_id': 'abc'}})
        with patch.object(module, 'get_unity_instance_from_context', AsyncMock(return_value='Exact@hash')):
            with patch.object(module, 'send_with_unity_instance', sender):
                value = asyncio.run(module.execute_code(SimpleNamespace(), **kwargs))
        return value, sender

    def test_start_forwards_fixed_request_and_target_without_reload_replay(self):
        value, send = self.call(action='start', code='return 42;', request_id='one')
        self.assertTrue(value['success'])
        args = send.call_args.args
        self.assertEqual(args[1], 'Exact@hash')
        self.assertEqual(args[3]['request_id'], 'one')
        self.assertEqual(args[3]['code'], 'return 42;')
        self.assertFalse(send.call_args.kwargs['retry_on_reload'])

    def test_start_without_request_never_dispatches(self):
        value, send = self.call(action='start', code='return 42;')
        self.assertFalse(value['success'])
        send.assert_not_awaited()

    def test_get_does_not_forward_code(self):
        _, send = self.call(action='get_job', job_id='abc', code='dangerous')
        self.assertNotIn('code', send.call_args.args[3])
        self.assertEqual(send.call_args.args[3]['job_id'], 'abc')

    def test_get_without_identity_never_dispatches(self):
        value, send = self.call(action='get_job')
        self.assertFalse(value['success'])
        send.assert_not_awaited()

    def test_null_message_preserves_transport_error_hint(self):
        response = {'success': False, 'message': None, 'error': 'TimeoutError', 'hint': 'inspect', 'code': 'x'}
        value, _ = self.call(response=response, action='execute', code='return 1;')
        self.assertEqual(value, response)

    def test_explicit_query_budget(self):
        _, send = self.call(action='get_job', request_id='one', timeout_seconds=90)
        self.assertEqual(send.call_args.args[3]['timeout_seconds'], 90)

    def test_budget_is_bounded(self):
        for timeout in (0, 301):
            value, send = self.call(action='get_job', request_id='one', timeout_seconds=timeout)
            self.assertFalse(value['success'])
            send.assert_not_awaited()


unittest.main()
