/**
 * Browser-friendly authoritative turn-based backend.
 * A Cloudflare Durable Object serializes all game actions by room.
 * No direct WebGL server socket or paid realtime database is required.
 */
const cors = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET,POST,OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
  'Cache-Control': 'no-store'
};
const alphabet = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
const json = (value, status = 200) =>
  new Response(JSON.stringify(value), { status, headers: { ...cors, 'Content-Type': 'application/json; charset=utf-8' } });
const pick = (n) => crypto.getRandomValues(new Uint32Array(1))[0] % n;
const randomCode = () => Array.from({ length: 6 }, () => alphabet[pick(alphabet.length)]).join('');
const limit = (s, max) => String(s || '').trim().slice(0, max);
const error = (message, status = 400) => json({ ok: false, error: message }, status);
const clone = (object) => structuredClone(object);

function adjacency(board) {
  const links = Array.from({ length: board.nodes.length }, () => []);
  for (const { a, b } of board.edges) { links[a].push(b); links[b].push(a); }
  return links;
}
export function shortestPath(board, source, target) {
  const links = adjacency(board);
  const parent = new Array(links.length).fill(-2), queue = [source];
  parent[source] = -1;
  for (let head = 0; head < queue.length; head++) {
    const at = queue[head];
    if (at === target) break;
    for (const next of links[at]) if (parent[next] === -2) { parent[next] = at; queue.push(next); }
  }
  if (parent[target] === -2) return [];
  const path = [];
  for (let at = target; at !== -1; at = parent[at]) path.push(at);
  return path.reverse();
}
const distance = (board, from, to) => {
  const path = shortestPath(board, from, to);
  return path.length ? path.length - 1 : 1e9;
};
function validateBoard(input) {
  if (!input || !Array.isArray(input.nodes) || !Array.isArray(input.edges) ||
    input.nodes.length < 3 || input.nodes.length > 450 ||
    input.edges.length < 2 || input.edges.length > 1200) throw Error('Map size is invalid (3–450 nodes, 2–1200 rails)');
  const map = clone(input);
  if (!Number.isInteger(map.startNode) || map.startNode < 0 || map.startNode >= map.nodes.length)
    throw Error('Invalid startNode');
  const cells = new Set();
  map.nodes.forEach((n, i) => {
    if (n.id !== i || !Number.isInteger(n.x) || !Number.isInteger(n.z) ||
      Math.abs(n.x) > 100 || Math.abs(n.z) > 100 || cells.has(n.x + ':' + n.z))
      throw Error('Nodes need unique sequential IDs and grid coordinates');
    cells.add(n.x + ':' + n.z);
    n.name = limit(n.name, 32);
    n.kind = Number(n.kind);
    if (!Number.isInteger(n.kind) || n.kind < 0 || n.kind > 5) throw Error('Invalid station type');
    n.price = Math.max(0, Math.min(10000, Math.floor(Number(n.price) || 0)));
    n.income = Math.max(0, Math.min(1000, Math.floor(Number(n.income) || 0)));
    n.owner = -1;
  });
  const edges = new Set();
  map.edges.forEach((e) => {
    if (!Number.isInteger(e.a) || !Number.isInteger(e.b) ||
      e.a < 0 || e.b < 0 || e.a >= map.nodes.length || e.b >= map.nodes.length || e.a === e.b)
      throw Error('Invalid rail endpoint');
    const a = map.nodes[e.a], b = map.nodes[e.b];
    if (Math.abs(a.x - b.x) + Math.abs(a.z - b.z) !== 1)
      throw Error('Rail endpoints must be adjacent on the grid');
    const key = [e.a, e.b].sort((x,y) => x-y).join(':');
    if (edges.has(key)) throw Error('Duplicate rail');
    edges.add(key);
  });
  if (map.nodes.some((n) => !shortestPath(map, map.startNode, n.id).length))
    throw Error('Map must be fully connected');
  if (map.nodes.filter(n => n.kind !== 0 && n.name.trim().length > 0).length < 3)
    throw Error('Add at least three named destination stations');
  map.title = limit(map.title, 70);
  return map;
}
const wealth = (state, seat) => state.players[seat].cash +
  state.board.nodes.filter(n => n.owner === seat).reduce((total,n) => total + n.price, 0);
const nextDestination = (state, previous = -1) => {
  const from = state.players[state.currentSeat].nodeId;
  let candidates = state.board.nodes.filter(n => n.kind !== 0 && n.name && n.name.trim() && n.id !== previous &&
    distance(state.board, from, n.id) >= 5);
  if (!candidates.length) candidates = state.board.nodes.filter(n => n.kind !== 0 && n.name && n.name.trim() && n.id !== previous);
  if (!candidates.length) return;
  state.destination = candidates[pick(candidates.length)].id;
  // The poor ghost only appears once someone first reaches a destination.
  state.ghostSeat = state.reachedGoals === 0 ? -1 : state.players.map((p,i) => i)
    .sort((a,b) => distance(state.board,state.players[b].nodeId,state.destination) -
                    distance(state.board,state.players[a].nodeId,state.destination))[0];
};
const finishTurn = (state) => {
  const current = state.currentSeat;
  const player = state.players[current];
  if (state.ghostSeat === current) {
    if (player.shield > 0) { player.shield--; state.message += ' / Shield blocked the ghost'; }
    else {
      const fee = 80 + Math.floor(state.turnCount / state.players.length) * 6;
      player.cash -= fee; state.message += ' / Ghost -' + fee;
    }
  }
  state.turnCount++;
  if (state.turnCount % state.players.length === 0)
    for (const station of state.board.nodes)
      if (station.kind === 2 && station.owner >= 0 && station.owner < state.players.length)
        state.players[station.owner].cash += station.income;
  if (state.turnCount >= 18 * state.players.length) {
    state.phase = 'finished';
    state.winnerSeat = state.players.map((p,i)=>i).sort((a,b)=>wealth(state,b)-wealth(state,a))[0];
    state.message = state.players[state.winnerSeat].name + ' WINS!';
  } else {
    state.currentSeat = (current + 1) % state.players.length;
    state.phase = 'ready'; state.remaining = 0; state.previousNode = -1;
  }
};
function arrival(state) {
  const p = state.players[state.currentSeat], n = state.board.nodes[p.nodeId];
  if (p.nodeId === state.destination) {
    p.cash += 1600; state.reachedGoals++;
    state.message = p.name + ' reached the GOAL! +1600';
    nextDestination(state, state.destination);
    finishTurn(state); return;
  }
  if (n.kind === 2) {
    if (n.owner === -1 && p.cash >= n.price) { state.phase = 'buy'; state.message = n.name + ': buy for ' + n.price + '?'; return; }
    if (n.owner >= 0 && n.owner !== state.currentSeat) {
      const toll = Math.max(30, Math.floor(n.income / 2));
      p.cash -= toll; state.players[n.owner].cash += toll;
      state.message = 'Paid ' + toll + ' toll at ' + n.name;
    }
  } else if (n.kind === 3 && p.cards.length < 3) {
    const draw = ['EXPRESS','CASH','SHIELD'][pick(3)];
    p.cards.push(draw); state.message = p.name + ' got ' + draw;
  } else if (n.kind === 4) { p.cash += 180; state.message = 'Bonus station! +180'; }
  else if (n.kind === 5) { p.cash -= 140; state.message = 'Unlucky station! -140'; }
  finishTurn(state);
}
export function applyAction(state, seat, action, node = -1, card = -1) {
  if (!state || seat < 0 || seat >= state.players.length) return false;
  if (action === 'start' && !state.started && seat === 0) {
    state.started = true; state.phase = 'ready';
    nextDestination(state);
    state.message = 'Reach ' + state.board.nodes[state.destination].name + '!';
    return true;
  }
  if (!state.started || state.phase === 'finished' || seat !== state.currentSeat) return false;
  const player = state.players[seat];
  if (action === 'roll' && state.phase === 'ready') {
    state.rolled = pick(6) + 1 + player.diceBoost;
    player.diceBoost = 0; state.remaining = state.rolled; state.previousNode = -1;
    state.phase = 'moving'; state.message = player.name + ' rolled ' + state.rolled;
    return true;
  }
  if (action === 'move' && state.phase === 'moving' && state.remaining > 0) {
    const links = adjacency(state.board)[player.nodeId];
    if (!links.includes(node) || (node === state.previousNode && links.length > 1)) return false;
    state.previousNode = player.nodeId; player.nodeId = node; state.remaining--;
    state.message = player.name + ' travelling... ' + state.remaining + ' left';
    if (state.remaining === 0) arrival(state);
    return true;
  }
  if ((action === 'buy' || action === 'skip') && state.phase === 'buy') {
    const stop = state.board.nodes[player.nodeId];
    if (action === 'buy' && stop.kind === 2 && stop.owner < 0 && player.cash >= stop.price) {
      player.cash -= stop.price; stop.owner = seat;
      state.message = player.name + ' bought ' + stop.name;
    }
    finishTurn(state); return true;
  }
  if (action === 'card' && state.phase === 'ready' &&
      Number.isInteger(card) && card >= 0 && card < player.cards.length) {
    const used = player.cards[card];
    if (!['EXPRESS','CASH','SHIELD'].includes(used)) return false;
    player.cards.splice(card, 1);
    if (used === 'EXPRESS') player.diceBoost += 2;
    if (used === 'CASH') player.cash += 350;
    if (used === 'SHIELD') player.shield++;
    state.message = player.name + ' used ' + used; return true;
  }
  return false;
}
function player(name, start) {
  return { name: limit(name, 16) || 'Player', nodeId: start, cash: 1200, diceBoost: 0, shield: 0, cards: [] };
}
const envelope = (room) => ({
  ok: true, code: room.code, version: room.version, snapshot: room.match
});

export class RailRoom {
  constructor(ctx) { this.ctx = ctx; }
  async fetch(request) {
    let input = {};
    try {
      const url = new URL(request.url);
      const action = url.pathname.slice(1);
      if (request.method === 'POST') {
        const raw = await request.text();
        if (raw.length > 130000) return error('Request exceeds 130 KB', 413);
        input = JSON.parse(raw || '{}');
      }
      let room = await this.ctx.storage.get('room');
      if (action === 'create' && request.method === 'POST') {
        if (room) return error('Room code collision, please retry', 409);
        const board = validateBoard(input.board);
        const code = limit(input.code, 6);
        const token = crypto.randomUUID();
        room = {
          code, version: 1, tokens: [token],
          match: {
            board, players: [player(input.name, board.startNode)], room: code,
            phase: 'lobby', message: 'Waiting for players', currentSeat: 0,
            destination: board.startNode, ghostSeat: -1, remaining: 0, rolled: 0,
            previousNode: -1, turnCount: 0, reachedGoals: 0, winnerSeat: -1, started: false
          }
        };
        await this.ctx.storage.put('room', room);
        return json({ ...envelope(room), token, seat: 0 });
      }
      if (!room) return error('Room not found', 404);
      if (action === 'state' && request.method === 'GET') return json(envelope(room));
      if (action === 'join' && request.method === 'POST') {
        if (room.match.started) return error('This match already started');
        if (room.match.players.length >= 4) return error('Room is full (4 players)');
        const token = crypto.randomUUID(), seat = room.match.players.length;
        room.tokens.push(token);
        room.match.players.push(player(input.name, room.match.board.startNode));
        room.match.message = room.match.players[seat].name + ' joined the room';
        room.version++;
        await this.ctx.storage.put('room', room);
        return json({ ...envelope(room), token, seat });
      }
      if (action === 'action' && request.method === 'POST') {
        const seat = room.tokens.indexOf(input.token);
        if (seat < 0) return error('Invalid player token', 403);
        if (!applyAction(room.match, seat, input.action, input.node, input.card))
          return error('Action is not legal at this point', 400);
        room.version++;
        await this.ctx.storage.put('room', room);
        return json(envelope(room));
      }
      return error('Unknown room action', 404);
    } catch (e) {
      return error(e.message || 'Bad request');
    }
  }
}
export default {
  async fetch(request, env) {
    if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers: cors });
    try {
      const path = new URL(request.url).pathname.split('/').filter(Boolean);
      if (path.length === 1 && path[0] === 'health')
        return json({ ok: true, service: 'railtrip-online', version: 1 });
      if (path.length === 1 && path[0] === 'rooms' && request.method === 'POST') {
        const raw = await request.text();
        if (raw.length > 130000) return error('Request too large', 413);
        let input;
        try { input = JSON.parse(raw); } catch { return error('Invalid JSON'); }
        for (let attempt = 0; attempt < 8; attempt++) {
          const code = randomCode();
          const id = env.ROOMS.idFromName(code);
          const result = await env.ROOMS.get(id).fetch('https://room.internal/create', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ...input, code })
          });
          if (result.status === 409) continue;
          return result;
        }
        return error('Could not allocate a room', 503);
      }
      if (path.length === 3 && path[0] === 'rooms' &&
          /^[A-Z2-9]{6}$/.test(path[1]) && ['state','join','action'].includes(path[2])) {
        const id = env.ROOMS.idFromName(path[1]);
        return env.ROOMS.get(id).fetch(new Request('https://room.internal/' + path[2], request));
      }
      return error('Unknown API endpoint', 404);
    } catch (e) { return error(e.message || 'Internal error', 500); }
  }
};
