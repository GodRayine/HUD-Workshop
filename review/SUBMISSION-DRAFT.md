# HUD Workshop — proposed testing submission (NOT READY TO SEND)

An in-game HUD layout editor with snapping, multi-selection, alignment, per-element properties and bounded layout-code import/export. It edits local UI configuration and does not assign actions or send gameplay/network requests. Editing is paused during combat and transitions. Saved chat visibility is maintained while the plugin runs.

Target: testing/live/HudEditor. Repository: https://github.com/GodRayine/HUD-Workshop; project_path: plugin; owner: GodRayine. Source commit: NOT YET SELECTED — do not submit the published1.0 commit as the reviewed1.1 implementation.

## AI involvement — factual disclosure draft
The owner specified the product requirements, iteratively tested the UI in game and reported issues. An AI coding agent wrote the implementation, investigated APIs, produced automated tests and made most technical implementation decisions. The owner has not independently authored a substantial portion of the code. No percentage split is claimed. This is substantial agent-led implementation, closest to the policy's Auto category rather than equal Pair programming; classification and eligibility should be confirmed with maintainers. No independent human source-code review is claimed at this stage.

## Validation
72 automated geometry, codec and transaction-boundary checks pass locally. The transaction failure tests simulate memory/disk operations and do not exercise native SaveFile. The owner confirmed opening, closing with Escape and reopening the1.1 inspector. The latest cancellation changes and native persistence regression suite still need in-game validation. Full class/special-mode coverage, clean installation and updating are pending. Do not turn pending checks into completed claims.

## Review-sensitive areas
Unsafe access pinned to Dalamud15.0.3.4; temporary native option loading; slot/global config differences for chat; preview ownership and cancellation; two separate persistence stores. See READINESS.md for unresolved issues.

This draft is for the owner's review only. Submission, upstream comments and publication require a new user instruction.
