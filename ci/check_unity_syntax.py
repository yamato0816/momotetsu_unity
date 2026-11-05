"""
A lightweight syntax check for Unity scripts without requiring an Editor license.
It checks C# grammar, not Unity API compatibility or runtime behavior.
"""
from pathlib import Path

from tree_sitter import Language, Parser
import tree_sitter_c_sharp


def main():
    root = Path(__file__).resolve().parents[1]
    parser = Parser(Language(tree_sitter_c_sharp.language()))
    source_files = sorted((root / "Assets").rglob("*.cs"))
    assert source_files, "Unity scripts were not found"
    failures = 0
    for file in source_files:
        tree = parser.parse(file.read_bytes())
        if tree.root_node.has_error:
            failures += 1
            print(f"PARSE ERROR: {file.relative_to(root)}")
            queue = [tree.root_node]
            while queue:
                node = queue.pop()
                if node.type == "ERROR" or node.is_missing:
                    print(f"  {node.type} at {node.start_point} ... {node.end_point}")
                queue.extend(reversed(node.children))
        else:
            print(f"OK: {file.relative_to(root)}")
    if failures:
        raise SystemExit(f"C# parse failed: {failures} file(s)")
    print(f"Passed: {len(source_files)} C# source files (syntax only)")


if __name__ == "__main__":
    main()
