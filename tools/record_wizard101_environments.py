"""Record local installed versions and upstream commits for repeatable evaluation."""

import json
from pathlib import Path
import subprocess

from wizard101_hub import ROOT, TOOLS, inspect_tool, tool_paths, write_json


def main():
    environments = {}
    sources = {}
    for name in TOOLS:
        source, python = tool_paths(name)
        environment_key = str(python.relative_to(ROOT))
        if environment_key not in environments and python.is_file():
            packages = subprocess.run([str(python), "-m", "pip", "list", "--format=json"],
                                      capture_output=True, text=True, check=True)
            environments[environment_key] = json.loads(packages.stdout)
        git = subprocess.run(["git", "-C", str(source), "rev-parse", "HEAD"],
                             capture_output=True, text=True)
        sources[name] = {"source": str(source.relative_to(ROOT)),
                         "containing_repository_commit": git.stdout.strip() if git.returncode == 0 else None}
    report = {"environments": environments, "sources": sources,
              "tools": [inspect_tool(name) for name in TOOLS]}
    output = ROOT / ".local/artifacts/environment-snapshot.json"
    write_json(output, report)
    print(f"Recorded {len(environments)} isolated environments in {output}")


if __name__ == "__main__":
    main()
