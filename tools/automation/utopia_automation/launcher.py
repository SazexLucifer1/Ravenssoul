"""Launch a game build with the automation server enabled and clean it up."""

from __future__ import annotations

import os
import secrets
import socket
import subprocess
import time
from pathlib import Path
from typing import IO

from .client import TOKEN_ENV, AutomationClient
from .errors import TransportError


def free_port_pair() -> int:
    """A port P such that P (HTTP) and P+1 (WebSocket) are currently free on loopback."""
    for _ in range(50):
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            port = probe.getsockname()[1]
        if port >= 65534:
            continue
        try:
            with socket.socket() as a, socket.socket() as b:
                a.bind(("127.0.0.1", port))
                b.bind(("127.0.0.1", port + 1))
            return port
        except OSError:
            continue
    raise RuntimeError("no free port pair found")


class GameProcess:
    """Starts Godot with `-- --automation ...`, a fresh short-lived token passed
    via environment (never on the command line), and a loopback-only bind.

        with GameProcess(godot, project, profile="qa", headless=False) as game:
            with game.client() as client:
                ...
    """

    def __init__(self, godot: str, project: str | os.PathLike[str], *, profile: str = "qa",
                 headless: bool = True, port: int | None = None, token_ttl_s: int = 900,
                 log_path: str | os.PathLike[str] | None = None, extra_args: list[str] | None = None,
                 startup_timeout_s: float = 60.0):
        self.godot = godot
        self.project = Path(project).resolve()
        self.profile = profile
        self.headless = headless
        self.port = port or free_port_pair()
        self.token = secrets.token_hex(32)
        self.token_ttl_s = token_ttl_s
        self.log_path = Path(log_path) if log_path else None
        self.extra_args = extra_args or []
        self.startup_timeout_s = startup_timeout_s
        self.process: subprocess.Popen[bytes] | None = None
        self._log_file: IO[bytes] | None = None

    @property
    def url(self) -> str:
        return f"http://127.0.0.1:{self.port}"

    def command(self) -> list[str]:
        cmd = [self.godot, "--path", str(self.project), "--audio-driver", "Dummy"]
        if self.headless:
            cmd.append("--headless")
        else:
            cmd += ["--rendering-driver", "opengl3"]
        cmd += self.extra_args
        cmd += ["--", "--automation", f"--automation-profile={self.profile}",
                f"--automation-port={self.port}", f"--automation-token-ttl={self.token_ttl_s}"]
        return cmd

    def start(self) -> "GameProcess":
        env = dict(os.environ, **{TOKEN_ENV: self.token})
        if self.log_path:
            self.log_path.parent.mkdir(parents=True, exist_ok=True)
            self._log_file = open(self.log_path, "wb")
        self.process = subprocess.Popen(self.command(), env=env, stdout=self._log_file or subprocess.DEVNULL,
                                        stderr=subprocess.STDOUT)
        deadline = time.monotonic() + self.startup_timeout_s
        probe = AutomationClient(self.url, self.token)
        while True:
            if self.process.poll() is not None:
                raise RuntimeError(f"game exited during startup with code {self.process.returncode}; see {self.log_path}")
            try:
                probe.health()
                return self
            except TransportError:
                if time.monotonic() > deadline:
                    self.stop()
                    raise RuntimeError("automation server did not become reachable")
                time.sleep(0.25)

    def client(self, timeout_s: float = 30.0) -> AutomationClient:
        return AutomationClient(self.url, self.token, timeout_s)

    def wait(self, timeout_s: float = 20.0) -> int:
        assert self.process is not None
        return self.process.wait(timeout=timeout_s)

    def stop(self, timeout_s: float = 10.0) -> int | None:
        """Graceful app.quit, then terminate, then kill. Always closes the log."""
        try:
            if self.process and self.process.poll() is None:
                try:
                    self.client(5).quit()
                    self.process.wait(timeout=timeout_s)
                except Exception:
                    self.process.terminate()
                    try:
                        self.process.wait(timeout=timeout_s)
                    except subprocess.TimeoutExpired:
                        self.process.kill()
                        self.process.wait()
            return self.process.returncode if self.process else None
        finally:
            if self._log_file:
                self._log_file.close()
                self._log_file = None

    def __enter__(self) -> "GameProcess":
        return self.start()

    def __exit__(self, *exc: object) -> None:
        self.stop()
