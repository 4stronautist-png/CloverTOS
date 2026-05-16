# Papaya Quest Trace Workflow

Use this when Clover quest behavior must be copied from the Papaya test client/server flow.

## Capture

1. Open an elevated PowerShell.
2. Start capture:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\Ubuntu-20.04\home\z3ck\Melia-TOS-Server\CloverTOS\tools\papaya_port\Start-QuestTrace.ps1" -Name "papaya-west-siauliai-new-character" -Note "New character through West Siauliai handoff"
```

3. Launch the Papaya test client and play the target quest segment.
4. Stop capture with the exact command printed by the start script.

The output goes to `C:\CloverTOS-Captures\quest-traces\<timestamp>-<name>` and includes:

- `*.etl` and `capture.pcapng`
- packet text from PktMon
- process snapshots
- copied client logs when discoverable
- metadata with start/stop timestamps

## Minimum Useful Segment

For the first import pass, capture from new character login until the first handoff out of West Siauliai. That segment covers:

- basic command tutorial
- opening cutscene
- quest auto-start/progress/turn-in
- NPC marker availability
- kill objectives
- private/layered monsters
- follow-up quest activation

After that, capture only chapter checkpoints and any Clover divergence.
