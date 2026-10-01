# shaders/

Shared `.gdshader` files (CanvasItem shaders; renderer is GL Compatibility).
None yet. Rules: shaders are visual-only, never gameplay truth; any motion
effect must check `UiMotion.reduced()` (pass a uniform) so reduced motion
disables it.
