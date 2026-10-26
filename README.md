# MUPinas — OpenMU Browser Stack

A separate local OpenMU deployment with PostgreSQL, a WebSocket-to-TCP proxy and a static web server. Based on [beater-studios/openmu-browser-stack](https://github.com/beater-studios/openmu-browser-stack) at commit `870a70f37c8f3b8da3c255a903c9068b7fdb7260`; upstream history, application source and MIT license are retained. The original documentation is in [README.upstream.md](README.upstream.md).

**This is working server infrastructure, not a playable browser game.** Upstream does not include a WASM client. The web page displays `No WASM client found`; game-account login, character selection, 3D rendering and multiplayer gameplay are not implemented by this repository.

## Requirements

- Docker Desktop running on macOS, or Docker Engine on Linux.
- Docker Compose **2.24.4+** (the local override uses `!override`).
- Node.js **22+** and npm. `.nvmrc` selects Node 22; use `nvm install && nvm use` if you already use nvm.
- Git, Bash and curl.
- Free host ports: 8080, 8090, 7100, 44405 and 45901–45903.

The container image digests are pinned. Their upstream images provide ARM64 and AMD64 variants. Validation was performed on Linux AMD64; Apple Silicon execution has not been tested here.

## Install and run

```bash
git clone https://github.com/jbaldaraje/MUPinas.git
cd MUPinas
bash scripts/install.sh
bash scripts/start.sh
```

Installation uses `npm ci`, validates Compose, pulls the pinned images and creates an ignored `.env` from `.env.loopback.example` only when no `.env` exists. Existing configuration is preserved. Startup uses the dedicated Compose project `mu-browser`, launches the Node processes and checks the real MU connection flow. Use a single checkout of this deployment at a time; its ports and Compose project name are shared.

All published ports and the Node listeners are bound to `127.0.0.1`. PostgreSQL is reachable only inside the Docker network. In a local installation the web page uses port 8080, the admin panel uses 8090, and the WebSocket proxy uses 7100. A cloud instance's loopback endpoints are internal and cannot be opened as your Mac's local services.

The local override corrects the upstream example's old port mappings: the admin process is configured to listen on container port 80, and the original-protocol Game Server endpoints use 45901–45903. The proxy preserves Game Server port numbers while rewriting redirects to its own host and port. Do not independently remap these host game ports.

## Verify and operate

```bash
# Repeat the infrastructure checks
node scripts/smoke.cjs

# Show status and container logs
bash scripts/compose.sh ps
bash scripts/compose.sh logs --tail 100 openmu postgres

# Show Node process logs
cat .run/proxy.log
cat .run/web.log

# Stop services while retaining database data
bash scripts/stop.sh

# Start again and rerun readiness checks
bash scripts/start.sh
```

The smoke test requires HTTP 200 and expected page content, COOP/COEP headers, the MU Connect Server hello, a nonempty server list, a rewritten redirect and a successful GameServerEntered greeting through a second WebSocket. A passing result does **not** establish game-account authentication or gameplay.

Data lives in the `mu-browser_pgdata` Docker volume. **Never run `docker compose down -v` unless you intend to erase it.** Stop/start retains data; volumes must be backed up separately when moving to another machine. Git does not contain the database, `.env`, Node dependencies, logs or running processes.

If ports conflict, stop the conflicting deployment before starting this one. The scripts preserve the upstream source; they do not modify another OpenMU installation. The proxy stores redirects by client IP, so concurrent browser tabs/clients sharing an IP can interfere; that remains an upstream limitation.

Configure admin authentication and review network exposure before any remote deployment. Original MU Online assets and trademarks may require separate authorization; the infrastructure's [MIT license](LICENSE) does not license those assets.
