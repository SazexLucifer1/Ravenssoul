# 0002 — GL Compatibility renderer
Status: Accepted
Date: 2026-10-01

## Context
2D tactics game for Windows and Linux; no 3D, no advanced lighting needs.

## Decision
Use `gl_compatibility` (OpenGL 3.3) for desktop.

## Consequences
Runs on older/integrated GPUs and in software (Mesa llvmpipe), which also
lets CI capture screenshots under Xvfb. Some Forward+-only features
(e.g. advanced 2D lighting performance, compute-based GPU particles) are
unavailable; prefer CPUParticles2D.

## Alternatives considered
Forward+ (unnecessary for 2D, higher requirements); Mobile (no benefit on desktop).
