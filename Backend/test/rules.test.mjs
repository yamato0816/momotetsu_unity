import test from 'node:test';
import assert from 'node:assert/strict';
import worker, { shortestPath, applyAction, RailRoom } from '../src/worker.js';

const board = {
  title: 'test', startNode: 0,
  nodes: [
    {id:0,x:0,z:0,name:'START',kind:1,owner:-1,price:0,income:0},
    {id:1,x:1,z:0,name:'STORE',kind:2,owner:-1,price:100,income:20},
    {id:2,x:2,z:0,name:'CARD',kind:3,owner:-1,price:0,income:0},
    {id:3,x:2,z:1,name:'GOAL',kind:1,owner:-1,price:0,income:0}
  ], edges: [{a:0,b:1},{a:1,b:2},{a:2,b:3}]
};
const state = () => ({
  board: structuredClone(board), players: [
    {name:'A',nodeId:0,cash:1200,diceBoost:0,shield:0,cards:[]},
    {name:'B',nodeId:0,cash:1200,diceBoost:0,shield:0,cards:[]}
  ], phase:'lobby', started:false, currentSeat:0, previousNode:-1,
  ghostSeat:-1, destination:3, turnCount:0, remaining:0, rolled:0, message:'', reachedGoals:0
});
test('BFS shortest path', () => {
  assert.deepEqual(shortestPath(board, 0, 3), [0,1,2,3]);
  assert.deepEqual(shortestPath(board, 2, 0), [2,1,0]);
});
test('only host may start, and only current player may move', () => {
  const s=state();
  assert.equal(applyAction(s,1,'start'),false);
  assert.equal(applyAction(s,0,'start'),true);
  assert.equal(applyAction(s,1,'roll'),false);
  assert.equal(applyAction(s,0,'roll'),true);
  assert.equal(s.phase,'moving');
  assert.equal(applyAction(s,0,'move',3),false);
  assert.equal(applyAction(s,0,'move',1),true);
});
test('unnamed rail event squares never become a destination', () => {
  for (let i=0; i<25; i++) {
    const s=state();
    s.board.nodes[2].name='';
    assert.equal(applyAction(s,0,'start'),true);
    assert.notEqual(s.board.nodes[s.destination].name,'');
    assert.equal(s.ghostSeat,-1);
  }
});
test('cards work only before roll', () => {
  const s=state();
  applyAction(s,0,'start');
  s.players[0].cards=['CASH','EXPRESS','SHIELD'];
  assert.equal(applyAction(s,0,'card',-1,0),true);
  assert.equal(s.players[0].cash,1550);
  assert.equal(applyAction(s,0,'card',-1,0),true);
  assert.equal(s.players[0].diceBoost,2);
  assert.equal(applyAction(s,0,'roll'),true);
  assert.equal(applyAction(s,0,'card',-1,0),false);
});
test('DO room creates and accepts authorized join', async () => {
  const data=new Map();
  const obj = new RailRoom({storage:{get:async k=>data.get(k),put:async(k,v)=>data.set(k,v)}});
  const create = await obj.fetch(new Request('https://room.internal/create', {
    method:'POST',body:JSON.stringify({code:'ABC234',name:'Host',board})
  }));
  assert.equal(create.status,200);
  const first=await create.json();
  assert.equal(first.seat,0);
  assert.equal(first.snapshot.players.length,1);
  const joined=await obj.fetch(new Request('https://room.internal/join',{
    method:'POST',body:JSON.stringify({name:'Guest'})
  }));
  const second=await joined.json();
  assert.equal(second.seat,1);
  const denied=await obj.fetch(new Request('https://room.internal/action',{
    method:'POST',body:JSON.stringify({token:'wrong',action:'start'})
  }));
  assert.equal(denied.status,403);
  const start=await obj.fetch(new Request('https://room.internal/action',{
    method:'POST',body:JSON.stringify({token:first.token,action:'start'})
  }));
  assert.equal((await start.json()).snapshot.phase,'ready');
});

test('public HTTP endpoints route room creation and state correctly', async () => {
  const rooms=new Map();
  const env={ ROOMS: {
    idFromName: code=>code,
    get: code=>{
      if(!rooms.has(code)) {
        const storage=new Map();
        const obj=new RailRoom({storage:{get:async k=>storage.get(k),put:async(k,v)=>storage.set(k,v)}});
        rooms.set(code,obj);
      }
      return rooms.get(code);
    }
  }};
  const create=await worker.fetch(new Request('https://railtrip.test/rooms', {
    method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({name:'Host',board})
  }),env);
  assert.equal(create.status,200);
  const first=await create.json();
  assert.match(first.code,/^[A-Z2-9]{6}$/);
  assert.equal(first.snapshot.players.length,1);
  const get=await worker.fetch(new Request('https://railtrip.test/rooms/'+first.code+'/state'),env);
  assert.equal(get.status,200);
  assert.equal((await get.json()).version,1);
  const options=await worker.fetch(new Request('https://railtrip.test/rooms', {method:'OPTIONS'}),env);
  assert.equal(options.status,204);
});
