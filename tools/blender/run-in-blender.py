"""
Runs a Python file inside the open Blender through its MCP add-on (Blender Lab, localhost:9876):

    python tools/blender/run-in-blender.py <file>

The file gets REPO, the repository's folder, and can set `result` (a dict) to send something back.
"""
import json
import os
import socket
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def run(code):
    with socket.create_connection(("127.0.0.1", 9876), timeout=600) as s:
        s.sendall(json.dumps({"type": "execute", "code": code, "strict_json": False}).encode() + b"\0")
        buf = b""
        while not buf.endswith(b"\0"):
            chunk = s.recv(65536)
            if not chunk:
                break
            buf += chunk
    return json.loads(buf.rstrip(b"\0"))


with open(sys.argv[1], encoding="utf-8") as f:
    reply = run("REPO = " + repr(REPO) + "\n" + f.read())
print(json.dumps(reply, indent=1)[:8000])
sys.exit(0 if reply.get("status") == "ok" else 1)
