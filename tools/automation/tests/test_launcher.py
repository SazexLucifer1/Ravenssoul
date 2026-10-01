"""Launcher security and port handling (no game process needed)."""

import socket
import unittest

from utopia_automation.launcher import GameProcess, free_port_pair


class LauncherTest(unittest.TestCase):
    def test_token_is_never_on_the_command_line(self):
        game = GameProcess("godot", ".", port=50000)
        command = " ".join(game.command())
        self.assertNotIn(game.token, command)
        self.assertGreaterEqual(len(game.token), 64)
        self.assertIn("--automation-profile=qa", command)
        self.assertNotIn("--automation-bind", command, "launcher must keep the loopback default")

    def test_each_launch_gets_a_fresh_token(self):
        self.assertNotEqual(GameProcess("g", ".").token, GameProcess("g", ".").token)

    def test_free_port_pair_is_bindable(self):
        port = free_port_pair()
        with socket.socket() as a, socket.socket() as b:
            a.bind(("127.0.0.1", port))
            b.bind(("127.0.0.1", port + 1))

    def test_headless_flag(self):
        self.assertIn("--headless", GameProcess("g", ".", headless=True).command())
        self.assertNotIn("--headless", GameProcess("g", ".", headless=False).command())


if __name__ == "__main__":
    unittest.main()
