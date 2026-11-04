# AGENTS.md — Cricket-2D Development Instructions

## 1. Project Mission

Cricket-2D is a high-performance, physics-based 2D cricket game built with Godot 4 and C#.

The game includes realistic ball physics, bat collisions, ground interactions, character animation, and replay functionality.

**The highest priority of this project is exceptional runtime performance, smooth gameplay, and efficient resource utilization.**

All code, architectural decisions, features, assets, and optimizations must follow the priorities defined below.

---

## 2. Non-Negotiable Priorities

Follow this priority order when making engineering decisions:

1. **Runtime Performance:** Maximum achievable FPS, minimal frame-time variation, low latency, and efficient CPU/GPU usage.
2. **Clean Architecture:** Maintainable, scalable, modular, and easily modifiable code.
3. **Readability:** Straightforward, understandable code with clear responsibilities and naming.
4. **Physics Correctness:** Consistent, accurate, and believable cricket physics.
5. **Visual Quality:** Attractive characters, animations, backgrounds, and effects without compromising performance.

Never sacrifice significant runtime performance for unnecessary abstractions, visual effects, or architectural complexity.

However, do not introduce fragile, unsafe, or incorrect code merely to obtain marginal theoretical performance improvements.

**Prefer measured optimizations over assumptions.**

---

## 3. Performance Requirements

### Primary Performance Target

- Target sustained **120 FPS or higher at 3840 × 2160 (4K)** on the defined reference hardware.
- Rendering must support frame rates above 120 FPS.
- Do not introduce an artificial 60 FPS or 120 FPS rendering cap.
- Avoid FPS-dependent game behavior.
- Prioritize consistent frame pacing over occasional high FPS readings.
- Target approximately **8.33 ms or less per rendered frame** at 120 FPS.
- Aim for low input latency and minimal frame-time spikes.
- Keep CPU, GPU, memory, and power consumption efficient.

These targets must be validated on actual hardware. Do not claim the game achieves 4K/120 FPS without measurement.

### Frame Rate and Physics Separation

Rendering FPS and physics simulation frequency must be independently configurable.

- Use a fixed timestep for physics.
- Start with a configurable 120 Hz physics simulation rate.
- Do not couple physics behavior to rendering FPS.
- Use interpolation when necessary to maintain smooth rendering.
- Support displays with refresh rates above 120 Hz.
- Avoid unnecessary synchronization or blocking operations.
- Do not increase physics frequency without evidence that the change improves simulation quality.

Do not disable VSync or force uncapped rendering indiscriminately. Make presentation and synchronization settings configurable while ensuring no hardcoded engine-side FPS cap. Consider tearing, display refresh rates, and unnecessary GPU utilization.

### Performance Budgets

Treat the following as initial engineering targets:

- Render frame time: ≤ 8.33 ms for 120 FPS.
- Physics processing: aim for substantially less than one physics-tick budget.
- Avoid regular garbage collection during gameplay.
- Avoid long main-thread stalls.
- Keep replay playback and recording overhead small.
- Keep asset uploads, decompression, and expensive initialization out of time-critical gameplay paths.

Track average FPS, 1% low FPS, frame-time percentiles, physics time, memory consumption, and allocation activity.

---

## 4. Mandatory Coding Rules

### General

- Write efficient, production-quality code.
- Keep implementations simple and understandable.
- Follow established C# and Godot conventions.
- Use descriptive class, method, and variable names.
- Follow single-responsibility principles.
- Prefer composition over unnecessary inheritance.
- Keep related functionality together.
- Minimize coupling between systems.
- Avoid unnecessary dependencies.
- Make configuration parameters easy to modify.
- Remove obsolete code when replacing implementations.

### Absolutely Avoid

- Infinite loops or unbounded work.
- Memory leaks or undisposed resources.
- Unnecessary per-frame allocations.
- Excessive garbage collection.
- Redundant physics calculations.
- Repeated expensive scene-tree searches.
- Repeated resource loading during gameplay.
- Unnecessary object instantiation or destruction.
- Blocking I/O in gameplay callbacks.
- Excessive logging during gameplay.
- Unnecessary timers or background threads.
- Expensive operations inside nested loops.
- Duplicate code and redundant systems.
- Dead code, unused variables, and unused imports.
- Unnecessary physics bodies or collision checks.
- Rebuilding unchanged geometry every frame.
- Hidden dependencies and overly complex abstractions.

Never leave temporary debugging code, unused experiments, or obsolete implementations in production paths.

### Allocation Rules

In frequently executed methods such as `_Process()`, `_PhysicsProcess()`, and `_IntegrateForces()`:

- Avoid unnecessary heap allocations.
- Avoid unnecessary LINQ queries and temporary collections.
- Avoid unnecessary string formatting.
- Reuse buffers and collections where practical.
- Cache references to frequently accessed nodes.
- Prefer appropriate value types for small data structures.
- Avoid creating delegates or closures repeatedly.
- Avoid allocating new arrays every frame.
- Use object pooling when measurements show it is beneficial.

Do not introduce pooling or custom memory management unless the benefits justify the additional complexity.

---

## 5. Godot-Specific Optimization

### Scene Architecture

- Keep scene trees reasonably shallow.
- Avoid unnecessary nodes.
- Avoid redundant scripts attached to multiple objects.
- Cache frequently accessed node references.
- Prefer event-driven updates over constant polling where appropriate.
- Disable processing on nodes that do not require updates.
- Avoid unnecessary signals emitted every frame.
- Use efficient collision layers and masks.

### Rendering

- Minimize unnecessary draw calls and material changes.
- Minimize unnecessary transparency and overdraw.
- Avoid oversized textures.
- Use appropriate texture compression and filtering.
- Reuse materials and textures where possible.
- Avoid unnecessary shaders and post-processing.
- Use texture atlases when beneficial.
- Do not update static visual elements continuously.
- Cache expensive visual calculations.
- Ensure background assets are optimized for their displayed size.

At 4K, carefully monitor fill rate, transparency, texture memory, and GPU workload.

### Asset Management

- Load reusable assets through a consistent resource-management strategy.
- Avoid repeated loading of the same resources.
- Preload or asynchronously load assets when appropriate.
- Dispose of or release unused resources safely.
- Avoid excessive resource duplication.
- Use suitable texture resolutions.
- Do not use unnecessarily high-resolution artwork.
- Prefer scalable, reusable character assets.

Do not reduce visual quality unnecessarily. Optimize based on actual rendering cost and visual impact.

---

## 6. Physics Performance and Correctness

Cricket physics must be accurate, predictable, and efficient.

The simulation includes:

- Gravity.
- Air resistance.
- Ball spin.
- Bat angular motion.
- Ball-ground collisions.
- Ball-bat collisions.
- Collision impulse response.
- Friction and restitution.

### Rules

- Use a fixed physics timestep.
- Keep physics calculations independent of rendering.
- Avoid unnecessary custom physics solvers.
- Use Godot's built-in physics capabilities where suitable.
- Add custom models only where required.
- Avoid duplicate collision processing.
- Minimize unnecessary contact queries.
- Use continuous collision detection or swept tests when required for fast-moving balls.
- Do not sacrifice critical collision accuracy merely to improve FPS.
- Use consistent physical units and explicit conversions.
- Keep physics parameters configurable.
- Avoid nondeterministic gameplay logic where practical.

Physics must remain stable when rendering FPS fluctuates.

---

## 7. Character Animation Optimization

Character animation must be visually convincing without generating excessive runtime overhead.

- Prefer reusable skeletal animation systems.
- Avoid unnecessary per-frame bone calculations.
- Cache skeleton and bone references.
- Use efficient interpolation.
- Calculate inverse kinematics only when required.
- Avoid redundant animation-state evaluations.
- Minimize unnecessary mesh deformation.
- Reuse animations across compatible characters.
- Keep animation logic independent of gameplay physics where appropriate.
- Ensure the bat visual and collision geometry remain synchronized.

Never implement complex full-body physics unless it provides a measurable gameplay or realism benefit.

---

## 8. Replay System Requirements

Replay functionality includes:

- Pause.
- Slow motion.
- Reverse playback.
- Fast-forward.
- Frame-by-frame stepping.
- Timeline seeking.

### Replay Performance Rules

- Use a preallocated snapshot buffer where practical.
- Avoid unnecessary allocations during snapshot capture.
- Keep snapshots compact.
- Record only the state required for replay and diagnostics.
- Avoid re-running physics during ordinary replay playback.
- Interpolate stored states efficiently.
- Prevent replay playback from generating new physics impulses.
- Keep replay storage bounded.
- Avoid copying entire snapshot buffers unnecessarily.
- Avoid excessive serialization during gameplay.

Replay must not produce significant FPS drops during normal gameplay.

Design replay data structures to support future recording and export features without requiring a complete rewrite.

---

## 9. Memory and Resource Management

Memory efficiency is mandatory.

- Avoid leaks and accidental object retention.
- Disconnect event handlers and signals when required.
- Release unnecessary references.
- Dispose of owned disposable resources.
- Avoid excessive temporary allocations.
- Use bounded caches and buffers.
- Avoid unbounded collections.
- Prevent unnecessary resource duplication.
- Monitor memory usage during prolonged gameplay.
- Ensure scene transitions clean up resources correctly.

The game must remain stable during extended sessions and repeated replay operations.

---

## 10. Threading and Asynchronous Work

- Keep main-thread work minimal.
- Use background processing only for appropriate tasks.
- Never access thread-unsafe Godot APIs from worker threads.
- Avoid blocking waits on the main thread.
- Avoid busy-waiting loops.
- Avoid excessive thread creation.
- Minimize synchronization overhead.
- Use cancellation for long-running operations.
- Ensure background tasks terminate and release resources correctly.

Do not introduce multithreading merely because it appears faster.

---

## 11. Architecture and Maintainability

The codebase must remain easy to understand and modify.

Separate responsibilities into clear systems:

- Game state.
- Physics simulation.
- Character animation.
- Input handling.
- Replay management.
- Rendering and visual effects.
- Audio.
- Debugging and diagnostics.

Use interfaces and abstractions only when they provide meaningful benefits.

Avoid overengineering.

Prefer small, focused components over large classes that manage unrelated systems.

Preserve clear boundaries between simulation, presentation, and input.

---

## 12. Optimization Workflow

Before optimizing code:

1. Identify the actual bottleneck.
2. Measure the current performance.
3. Determine the expected benefit.
4. Make the smallest effective change.
5. Measure performance again.
6. Verify correctness.
7. Keep the optimization only if the improvement is meaningful.

Use Godot's profiler and appropriate external profiling tools.

Do not optimize based solely on intuition.

When a performance issue is detected, investigate:

- CPU frame time.
- GPU frame time.
- Physics processing time.
- Rendering overhead.
- Garbage collection.
- Memory allocations.
- Texture memory.
- Collision processing.
- Animation evaluation.
- Scene-tree processing.
- Resource loading.

Document important performance tradeoffs.

---

## 13. Testing and Validation

Every significant change must preserve existing functionality.

### Mandatory Checks

- Project builds successfully.
- No new runtime errors.
- No unnecessary warnings.
- Physics remains stable.
- Collisions behave correctly.
- Replay remains functional.
- Resources are released correctly.
- No uncontrolled memory growth.
- No unexpected main-thread stalls.
- No unnecessary FPS caps.
- No significant performance regression.

### Performance Regression Testing

Test representative scenarios:

1. Ball in free flight.
2. Ball-ground collision.
3. Bat-ball collision.
4. Fully animated batsman.
5. Multiple visual effects.
6. Replay playback.
7. Slow-motion playback.
8. Repeated replay seeking.
9. Prolonged gameplay.

Use release/export builds for final performance validation, not only editor runs.

Maintain a repeatable benchmark scene and record hardware, resolution, graphics settings, and test conditions.

---

## 14. Rules for Codex Changes

Whenever modifying the project:

1. Inspect the existing implementation first.
2. Understand dependencies and performance-sensitive systems.
3. Reuse existing components where practical.
4. Avoid unnecessary rewrites.
5. Make focused, minimal changes.
6. Remove replaced or obsolete code.
7. Preserve coding consistency.
8. Do not introduce unnecessary packages.
9. Validate the project after modification.
10. Evaluate possible performance regressions.

When implementing a new feature, consider its runtime cost before writing code.

If multiple approaches exist, prefer the one providing the best combination of:

- Runtime performance.
- Low resource usage.
- Maintainability.
- Simplicity.
- Correctness.
- Scalability.

### Required Change Summary

After completing a substantial coding task, report:

- What changed.
- Files created, modified, or removed.
- Performance implications.
- Testing performed.
- Any remaining risks or limitations.

Do not claim performance improvements without measurements.

---

## 15. Production Build Optimization

Optimize final builds separately from editor development.

- Use appropriate release/export configurations.
- Disable unnecessary debugging instrumentation.
- Remove temporary diagnostic features from production execution paths.
- Avoid unnecessary production logging.
- Remove unused assets when safe.
- Use suitable asset compression.
- Ensure efficient startup and resource loading.
- Avoid unnecessary runtime dependencies.
- Validate the final exported build at 4K.

Do not assume a successful debug build proves production performance.

---

## 16. Final Engineering Principles

**Performance is a feature, not an afterthought.**

Every feature must be evaluated for its effects on:

- FPS.
- Frame pacing.
- CPU utilization.
- GPU utilization.
- Memory consumption.
- Input latency.
- Physics stability.
- Maintainability.

The goal is not merely to reach a high FPS number.

The goal is to deliver a cricket game that feels exceptionally smooth, responds immediately to player input, maintains stable physics, and runs efficiently.

**Always prioritize measured performance, clean code, predictable behavior, and minimal resource overhead.**

**Target: sustained 120+ FPS at 4K on reference hardware, without an artificial rendering FPS limit.**