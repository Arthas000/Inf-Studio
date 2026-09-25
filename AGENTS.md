# Working contract — InFalsus Studio 0.9

Read README and Documentation/CODEX_HANDOFF.md first. This source pack is imported into the user's existing Unity project, not a full Hub project. No Unity/C# execution is claimed for this release.

Preserve the calibrated camera/stage, source-token round trips, existing GUIDs, Alt handles/Ctrl selection, group-head semantics, and explicit Save-only persistence. Sky group is not an AFF timing group. Music transport rate and sample pitch are separate. Do not rewrite unknown binary records from guesses. Do not copy repository fonts or overwrite Packages/ProjectSettings. Build via Editor/StudioBuild08.cs (legacy class name, current0.9 output) and report actual logs, not static-test counts as proof of compilation.

Arcade-plus research is in Documentation/ARCADE_PLUS_COMPARISON.md. Each proposed optimization must preserve listed acceptance tests and data contracts. Do not use that repo's BPM-driven travel formula for SPC track-driven travel. No autosave without new user consent.
