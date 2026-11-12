"""Make a draft for bounded random exploration in private slowed Dodge games."""

import argparse
from copy import deepcopy
import hashlib
from pathlib import Path

from game_packs import load_pack, save_pack


def build(workspace, source, library):
    workspace = Path(workspace).resolve(strict=True)
    source = Path(source).resolve(strict=True)
    library = Path(library).resolve()
    if not library.is_relative_to(workspace):
        raise ValueError("The exploration library must be inside the workspace")
    pack = deepcopy(load_pack(source))
    if pack.get("game_id") != "godot-dodge-the-creeps" or "context" not in pack:
        raise ValueError("Choose a context-aware Dodge source pack")
    if "experimental_model" in pack or "experimental_outcome_model" in pack:
        raise ValueError("Choose a non-model source pack")
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    pack["version"] = "0.13.0-" + digest[:12]
    pack["title"] = "Dodge bounded exploration research prototype"
    pack["context"]["avoidance_policy"] = "directional-explore-v35"
    pack["context"]["constraints"].pop("risk_weight_percent", None)
    pack["context"]["constraints"].pop("edge_weight_percent", None)
    pack["exploration_provenance"] = {
        "source_pack_sha256": digest,
        "exploration_rate": 0.2,
    }
    return save_pack(pack, library, source_directory=source.parent)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("workspace", type=Path)
    parser.add_argument("source", type=Path)
    parser.add_argument("library", type=Path)
    args = parser.parse_args()
    print(build(args.workspace, args.source, args.library))


if __name__ == "__main__":
    main()
