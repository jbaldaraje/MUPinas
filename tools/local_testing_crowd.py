#!/usr/bin/env python3
"""Measure a bounded crowd on an isolated OpenMU developer actor endpoint.

The endpoint must be loopback-only. An already spawned GM actor and an enabled
local testing feature are required. This measures server simulation, not clients.
"""

import argparse
from collections import Counter
import json
from pathlib import Path
import socket
import subprocess
import time


def request(port, **message):
    with socket.create_connection(("127.0.0.1", port), timeout=180) as connection:
        stream = connection.makefile("rwb")
        stream.write((json.dumps(message) + "\n").encode())
        stream.flush()
        response = json.loads(stream.readline())
    if not response.get("ok"):
        raise RuntimeError(response)
    return response


def command(options, text):
    cursor = request(options.port, cmd="events", actor=options.actor)["last_seq"]
    started = time.monotonic()
    request(options.port, cmd="say", actor=options.actor, text=text)
    response = request(options.port, cmd="events", actor=options.actor, since=cursor)
    messages = [e["message"] for e in response["events"] if e["type"] == "message"]
    failures = [m for m in messages if m.startswith("@LT1 error")]
    if failures:
        raise RuntimeError(failures)
    return {"command": text, "elapsed_seconds": time.monotonic() - started, "messages": messages}


def docker_sample(container):
    if not container:
        return None
    result = subprocess.run(
        ["docker", "stats", "--no-stream", "--format", "{{json .}}", container],
        capture_output=True, text=True, check=True, timeout=15,
    )
    return json.loads(result.stdout)


def run(options):
    report = {"kind": "server-side simulation", "client_fps": None, "samples": []}
    try:
        report["baseline"] = docker_sample(options.container)
        for count in options.counts:
            sample = command(options, f"/localtest crowd {count} {options.map} {options.x} {options.y} {options.radius} {options.behavior} 300 100")
            # Allow the normal periodic task to produce a CPU sampling interval.
            time.sleep(options.settle)
            status = command(options, "/localtest status")
            bots = [m.split() for m in status["messages"] if m.startswith("@LT1 bot ")]
            crowd = next(m for m in status["messages"] if m.startswith("@LT1 crowd "))
            actual = int(crowd.split()[3])
            if actual != count:
                raise RuntimeError(f"Requested {count}, actual {actual}: {crowd}")
            sample.update(actual=actual, classes=dict(Counter(b[3] for b in bots)), status=status["messages"], docker=docker_sample(options.container))
            report["samples"].append(sample)
            options.output.write_text(json.dumps(report, indent=2) + "\n")
            print(f"requested={count} actual={actual} classes={sample['classes']}", flush=True)
    finally:
        report["cleanup"] = command(options, "/localtest remove all")
        report["after_cleanup"] = command(options, "/localtest status")
        options.output.write_text(json.dumps(report, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=56666)
    parser.add_argument("--actor", required=True)
    parser.add_argument("--container")
    parser.add_argument("--counts", type=int, nargs="+", default=[10, 25, 50, 100])
    parser.add_argument("--map", type=int, default=0)
    parser.add_argument("--x", type=int, default=135)
    parser.add_argument("--y", type=int, default=125)
    parser.add_argument("--radius", type=int, default=8)
    parser.add_argument("--behavior", choices=["idle", "wander", "combat"], default="idle")
    parser.add_argument("--settle", type=float, default=5)
    parser.add_argument("--output", type=Path, required=True)
    options = parser.parse_args()
    if any(c < 1 or c > 100 for c in options.counts) or not 1 <= options.settle <= 30:
        parser.error("Counts must be 1..100 and settle time 1..30 seconds.")
    options.output.parent.mkdir(parents=True, exist_ok=True)
    run(options)


if __name__ == "__main__":
    main()
