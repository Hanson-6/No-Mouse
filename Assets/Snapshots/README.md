# Snapshots Folder

SaveManager stores development snapshot saves in this folder as JSON files.

- `snapshot_*.json`: one save snapshot per Save&Quit
- `manifest.json`: tracks latest file and keeps at most 1 snapshot

This folder is intended for local development in Unity Editor.

Version-control policy for this folder:

- Keep the folder itself (`Assets/Snapshots.meta`) and this `README.md` (+ `.meta`) in Git.
- Do not commit runtime-generated snapshot files (`snapshot_*.json*`, `manifest.json*`).
