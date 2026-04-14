# Snapshots Folder

SaveManager stores development snapshot saves in this folder as JSON files.

- `checkpoint_latest.json`: durable checkpoint save written only when checkpoint triggers
- `session_live.json`: live session save used for Continue priority

This folder is intended for local development in Unity Editor.

Version-control policy for this folder:

- Keep the folder itself (`Assets/Snapshots.meta`) and this `README.md` (+ `.meta`) in Git.
- Do not commit runtime-generated snapshot files (`checkpoint_latest.json*`, `session_live.json*`).
