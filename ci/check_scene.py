"""Validate Unity's committed editable Scene without requiring Unity or a license.

Checks serialized object references, visual authoring components, and the
orthogonal connected rail graph. This is NOT a Unity import/build smoke test.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets/Scenes/Main.unity"
GUID_BOARD = "e0937a5cf8e54a8f918193b5204e6211"
GUID_NODE = "34d3f4a4afaa4c5890f34de317b2d5c2"
GUID_TRACK = "8b7ee6715c3c4c498da0bd21ec314507"


def required(pattern, text, what, flags=0):
    match = re.search(pattern, text, flags)
    assert match, f"Missing {what}"
    return match.group(1)


def main():
    scene = SCENE.read_text(encoding="utf-8")
    assert scene.startswith("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n")
    documents = re.findall(r"(?ms)^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", scene)
    assert documents, "Unity YAML has no objects"
    objects = {}
    for class_id, file_id, data in documents:
        assert file_id not in objects, f"Duplicate serialized fileID: {file_id}"
        objects[file_id] = (int(class_id), data)
    gameobjects = {k for k, (t, _) in objects.items() if t == 1}
    transforms = {k for k, (t, _) in objects.items() if t == 4}
    assert gameobjects and transforms

    for file_id, (class_id, body) in objects.items():
        if class_id in {4, 23, 33, 20, 81, 108, 114, 102}:
            go = required(r"m_GameObject: \{fileID: (\d+)\}", body, f"GameObject in {file_id}")
            assert go in gameobjects, f"Dangling GameObject reference: {go}"
        if class_id == 4:
            father = required(r"m_Father: \{fileID: (\d+)\}", body, "Transform parent")
            assert father == "0" or father in transforms
            for child in re.findall(r"m_Children:(.*?)(?=\n  m_Father:)", body, re.S):
                for ref in re.findall(r"fileID: (\d+)", child):
                    assert ref in transforms, f"Bad child transform ref {ref}"
    mono = {k: b for k, (t, b) in objects.items() if t == 114}
    by_script = lambda guid: {
        key: body for key, body in mono.items()
        if re.search(rf"m_Script: \{{fileID: 11500000, guid: {guid}, type: 3\}}", body)
    }
    boards = by_script(GUID_BOARD)
    nodes = by_script(GUID_NODE)
    tracks = by_script(GUID_TRACK)
    assert len(boards) == 1, "Exactly one SceneRailBoard is required"
    assert len(nodes) >= 3, "Scene should contain editable station GameObjects"
    assert len(tracks) >= 2, "Scene should contain editable track GameObjects"
    node_ids = []
    node_by_serial = {}
    for serial, body in nodes.items():
        node_id = int(required(r"(?m)^  nodeId: (\d+)$", body, f"node ID {serial}"))
        assert re.search(r"tileRenderer: \{fileID: [1-9]\d+\}", body), f"Missing static tile renderer at {node_id}"
        node_ids.append(node_id)
        node_by_serial[serial] = node_id
    assert sorted(node_ids) == list(range(len(nodes))), "Scene node IDs must be contiguous and unique"
    links = []
    for serial, body in tracks.items():
        a = required(r"(?m)^  from: \{fileID: (\d+)\}", body, "track from")
        b = required(r"(?m)^  to: \{fileID: (\d+)\}", body, "track to")
        highlight = required(r"(?m)^  routeHighlight: \{fileID: (\d+)\}", body, "track overlay")
        assert a in node_by_serial and b in node_by_serial and highlight in gameobjects
        pair = tuple(sorted((node_by_serial[a], node_by_serial[b])))
        assert pair[0] != pair[1] and pair not in links, f"Invalid or duplicate rail {pair}"
        links.append(pair)

    board = next(iter(boards.values()))
    start = int(required(r"(?m)^  startNodeId: (\d+)$", board, "start node"))
    step = float(required(r"(?m)^  gridSize: (\d+(?:\.\d+)?)$", board, "grid size"))
    assert start in node_ids and step > 0
    for field in ("followCamera", "ghostMarker", "goalMarker", "diceVisual"):
        target = required(rf"(?m)^  {field}: \{{fileID: (\d+)\}}", board, field)
        assert target in objects, f"Missing {field} object"
    assert len(re.findall(r"(?m)^  - \{fileID: \d+\}$", board)) >= 4, "Need four static train slots"
    transform_for_go = {}
    for serial in transforms:
        body = objects[serial][1]
        transform_for_go[required(r"m_GameObject: \{fileID: (\d+)\}", body, "transform GO")] = body
    coords = {}
    for serial, body in nodes.items():
        go = required(r"m_GameObject: \{fileID: (\d+)\}", body, "node GO")
        position = required(r"(?m)^  m_LocalPosition: (\{.*?\})$", transform_for_go[go], "node position")
        x = float(required(r"x: (-?\d+(?:\.\d+)?)", position, "x"))
        z = float(required(r"z: (-?\d+(?:\.\d+)?)", position, "z"))
        px,pz=round(x/step),round(z/step)
        assert abs(x-px*step)<.05 and abs(z-pz*step)<.05, "Node off grid"
        coords[node_by_serial[serial]] = px,pz
    assert len(set(coords.values()))==len(coords), "Overlapping authored station coordinates"
    connections={i:set() for i in node_ids}
    for a,b in links:
        x1,z1=coords[a];x2,z2=coords[b]
        assert abs(x1-x2)+abs(z1-z2)==1, f"Rail {a}-{b} is not one grid step"
        connections[a].add(b);connections[b].add(a)
    seen={start};pending=[start]
    while pending:
        for nxt in connections[pending.pop()]:
            if nxt not in seen:
                seen.add(nxt);pending.append(nxt)
    assert len(seen)==len(coords), "Scene rails do not connect all stations"

    material_guids = set()
    for path in (ROOT / "Assets/Art/Materials").glob("*.mat.meta"):
        material_guids.update(re.findall(r"guid: ([a-f0-9]{32})", path.read_text()))
    for _, (cid, body) in objects.items():
        if cid == 23:
            for guid in re.findall(r"m_Materials:\s*-\s*\{fileID: 2100000, guid: ([0-9a-f]{32})", body):
                assert guid in material_guids, f"Missing material GUID {guid}"
    for name,guid in (("SceneRailBoard",GUID_BOARD),("SceneRailNode",GUID_NODE),
                      ("SceneRailSegment",GUID_TRACK)):
        meta=ROOT/f"Assets/Scripts/{name}.cs.meta"
        assert meta.exists() and f"guid: {guid}" in meta.read_text()
    for runtime in ("Assets/Scripts/BoardView.cs","Assets/Scripts/DiceRollView.cs"):
        source = (ROOT/runtime).read_text()
        assert "GameObject.CreatePrimitive" not in source, "Scenery must not spawn at runtime"
        assert "new GameObject(" not in source, "View must reference authored scene objects"
    print(f"PASS: {len(nodes)} editable nodes, {len(tracks)} editable tracks, "
          f"{len(gameobjects)} saved GameObjects, connected board and valid serialized links")


if __name__ == "__main__":
    main()
