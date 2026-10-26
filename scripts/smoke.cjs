#!/usr/bin/env node
"use strict";

// Infrastructure smoke test only. No account login, rendering or gameplay is tested.
const assert = require("node:assert/strict");
const WebSocket = require("../node_modules/ws");
const PacketParser = require("../src/proxy/packet-parser");

const webUrl = process.env.SMOKE_WEB_URL || "http://127.0.0.1:8080/";
const adminUrl = process.env.SMOKE_ADMIN_URL || "http://127.0.0.1:8090/";
const wsUrl = process.env.SMOKE_WS_URL || "ws://127.0.0.1:7100";
const timeoutMs = 20_000;
const clients = new Set();

function connect() {
  const ws = new WebSocket(wsUrl, { handshakeTimeout: timeoutMs });
  clients.add(ws);
  const parser = new PacketParser("smoke");
  const packets = [];
  const waiting = [];
  let terminalError;

  function fail(error) {
    terminalError = error;
    for (const waiter of waiting.splice(0)) {
      clearTimeout(waiter.timer);
      waiter.reject(error);
    }
  }

  parser.on("packet", (packet) => {
    const waiter = waiting.shift();
    if (waiter) {
      clearTimeout(waiter.timer);
      waiter.resolve(Buffer.from(packet));
    } else {
      packets.push(Buffer.from(packet));
    }
  });
  ws.on("message", (data) => parser.feed(Buffer.from(data)));
  ws.on("error", fail);
  ws.on("close", (code) => {
    clients.delete(ws);
    fail(new Error(`WebSocket closed before the next packet (code ${code})`));
  });

  return {
    socket: ws,
    next(label) {
      if (packets.length) return Promise.resolve(packets.shift());
      if (terminalError) return Promise.reject(terminalError);
      return new Promise((resolve, reject) => {
        const waiter = { resolve, reject, timer: undefined };
        waiter.timer = setTimeout(() => {
          const index = waiting.indexOf(waiter);
          if (index !== -1) waiting.splice(index, 1);
          reject(new Error(`Timed out after ${timeoutMs}ms waiting for ${label}`));
        }, timeoutMs);
        waiting.push(waiter);
      });
    },
    send(packet) {
      assert.equal(ws.readyState, WebSocket.OPEN, "WebSocket must be open before sending");
      ws.send(packet);
    },
    async close() {
      if (ws.readyState === WebSocket.CLOSED) return;
      await new Promise((resolve) => {
        const timer = setTimeout(() => { ws.terminate(); resolve(); }, 1000);
        ws.once("close", () => { clearTimeout(timer); resolve(); });
        ws.close();
      });
    },
  };
}

async function checkHttp() {
  const web = await fetch(webUrl, { signal: AbortSignal.timeout(timeoutMs) });
  assert.equal(web.status, 200, "Web server must return HTTP 200");
  assert.match(web.headers.get("content-type") || "", /text\/html/i);
  assert.equal(web.headers.get("cross-origin-opener-policy"), "same-origin");
  assert.equal(web.headers.get("cross-origin-embedder-policy"), "require-corp");
  const body = await web.text();
  assert.match(body, /OpenMU Browser Stack/);
  assert.match(body, /No WASM client found/i, "Expected the upstream placeholder, not a game client");
  console.log("PASS web server: upstream placeholder served with COOP/COEP headers");

  const admin = await fetch(adminUrl, { signal: AbortSignal.timeout(timeoutMs) });
  assert.equal(admin.status, 200, "Admin HTTP endpoint must return 200");
  assert.match(admin.headers.get("content-type") || "", /text\/html/i);
  const adminBody = await admin.text();
  assert.match(adminBody, /<html[\s>]/i, "Admin endpoint must serve HTML");
  assert.match(adminBody, /OpenMU/i, "Admin HTML must identify OpenMU");
  console.log("PASS admin panel: OpenMU HTML returned (admin authentication not tested)");
}

async function checkMuTransport() {
  const connectServer = connect();
  const hello = await connectServer.next("Connect Server hello");
  assert.equal(hello.toString("hex"), "c1040001", "Unexpected Connect Server hello");
  console.log("PASS WebSocket proxy -> Connect Server: MU hello received");

  connectServer.send(Buffer.from("c104f406", "hex"));
  const list = await connectServer.next("server list");
  assert.ok(list.length >= 7, "Server list is too short");
  assert.equal(list[0], 0xc2);
  assert.equal(list.readUInt16BE(1), list.length);
  assert.equal(list[3], 0xf4);
  assert.equal(list[4], 0x06);
  const count = list.readUInt16BE(5);
  assert.ok(count > 0, "No available Game Server in Connect Server response");
  assert.equal(list.length, 7 + count * 4, "Unexpected server-list entry layout");
  const serverId = list.readUInt16LE(7);
  console.log(`PASS server discovery: ${count} Game Server(s), selected ID ${serverId}`);

  const request = Buffer.from([0xc1, 0x06, 0xf4, 0x03, 0, 0]);
  request.writeUInt16LE(serverId, 4);
  connectServer.send(request);
  const redirect = await connectServer.next("Game Server redirect");
  assert.equal(redirect.length, 22);
  assert.equal(redirect.subarray(0, 4).toString("hex"), "c116f403");
  const redirectHost = redirect.subarray(4, 20).toString("ascii").replace(/\0.*$/, "");
  const redirectPort = redirect.readUInt16LE(20);
  const expected = new URL(wsUrl);
  assert.equal(redirectHost, expected.hostname, "Redirect must use the configured proxy host");
  assert.equal(redirectPort, Number(expected.port || (expected.protocol === "wss:" ? 443 : 80)));
  console.log(`PASS redirect rewriting: ${redirectHost}:${redirectPort}`);

  // The upstream proxy consumes an IP-keyed pending target on the next connection.
  await connectServer.close();
  const gameServer = connect();
  const entered = await gameServer.next("Game Server greeting");
  assert.ok(entered.length >= 12, "Game Server greeting is too short");
  assert.equal(entered[0], 0xc1);
  assert.equal(entered[1], entered.length);
  assert.equal(entered[2], 0xf1);
  assert.equal(entered[3], 0x00);
  assert.equal(entered[4], 1, "Game Server connection was rejected");
  console.log("PASS WebSocket proxy -> Game Server: successful GameServerEntered greeting");
  await gameServer.close();
}

(async () => {
  try {
    await checkHttp();
    await checkMuTransport();
    console.log("PASS infrastructure smoke completed; login, 3D client and gameplay remain untested/unavailable");
  } catch (error) {
    console.error(`FAIL infrastructure smoke: ${error.stack || error.message}`);
    process.exitCode = 1;
  } finally {
    for (const client of clients) client.terminate();
  }
})();
