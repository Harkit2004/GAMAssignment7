# SpriteGameOpenTk

## Watch Demo

## Overview

I added three grounded movement modes (Idle, Walk, Run) and a Jump. Arrow keys move the character; holding Shift switches Walk to Run; Space triggers a jump when grounded. A simple floor is rendered at the bottom of the scene so the jump arc, landing, and grounded state are clearly visible. Left-facing animations reuse the right-facing sheets by mirroring UVs, so you only need Idle.png, Walk.png, Run.png, and Jump.png.

Under the hood there’s a tiny state machine driving both physics and animation. The `State` enum covers `None` (idle), `Walk_Left/Right`, `Run_Left/Right`, and `Jump`. Input sets a desired horizontal velocity (walk/run speed), gravity integrates vertical motion, and a jump impulse is applied only when grounded. Animation is picked from the current state, with per‑state frame counts and frame times, and left variants are mirrored via negative UV width. The character writes its model matrix from its simulated position each frame and collides with the floor by clamping to the ground’s top and zeroing vertical velocity.

A few challenges came up: there is no left walk/run sheet, so I mirrored the right sheet in the fragment sampling by flipping `uSize.x` and offsetting `uOffset.x` to the frame’s right edge. Keeping frames crisp and avoiding atlas bleeding required `Nearest` filtering and `ClampToEdge`. Timing jitter was solved with a simple accumulator that advances frames at fixed intervals, and jump/state transitions were made robust by gating jump on the grounded flag and letting air time force the `Jump` animation until landing. The result feels responsive, is easy to tweak (constants for speeds, gravity, frame sizes), and keeps the logic readable and maintainable.
