# Vibes ASB Manager — Roadmap

Engineering backlog from the June 2026 code audit. Scope is framed around what this app
**is**: a local-first, single-user tool for managing Azure Service Bus namespaces.

Out of scope by design (don't add here): authentication and at-rest encryption of connection
strings — both are deliberate trade-offs for a local tool.

**How to use:** pick one item, move it to In progress, open a branch, finish it, tick the box.
Items are self-contained — each has the problem, why it matters, where to look, and a sketch.

**Effort:** `S` ≈ ≤1h · `M` ≈ half-day · `L` ≈ multi-session.

---

## ✅ Recently shipped (through v1.9.20)

**v1.9.18** — DLQ & active message tables reflect current state (authoritative peek snapshot);
paging logic extracted to a unit-tested `MessageSnapshotPager`. CI/Docker pinned & aligned to
.NET 10 (`global.json`); `Microsoft.Extensions.*` → 10.0.9; OpenTelemetry off vulnerable 1.15.0.

**v1.9.19** — GitHub Actions bumped to Node-24 majors (clears the Node 20 deprecation).

**v1.9.20** — **B1** shared infra singletons (one client pool per namespace) + client-cache
contract tests; **E1** container healthcheck; **E2** non-root image; **E3** compose `version:`
cleanup; **E4** removed unused `DataProtection.Abstractions`.

---

## Suggested order

Front-loaded with high-value correctness; bigger structural/feature work later.
(A1, A2, A3, B1, C1, C2/C3/C4, D1 and the E-series are done — see Recently shipped above.)

1. **B3** — connection-handle abstraction + Microsoft Entra ID auth (biggest feature unlock)
2. **B2 / D2** — opportunistic polish

---

## A. Azure Service Bus correctness

- [x] **A1 — Handle session-enabled entities `[M]`** — ✅ shipped & emulator-verified. Active queue and
  subscription purges detect a session entity by catching the `InvalidOperationException` a non-session
  receiver throws against a `RequiresSession` entity, then drain each session via `AcceptNextSession`
  (`ReceiveAndDelete`); "no more sessions" surfaces as `ServiceTimeout`, bounded by a dedicated
  short-timeout drain client so a purge doesn't stall ~60s after the last message. Dead-letter ops
  (purge-DLQ / replay / remove) use **regular** receivers — a session entity's dead-letter sub-queue is
  not itself session-scoped (`AcceptNextSession` on it throws "Cannot create a MessageSession for a
  sub-queue"). Replayed messages preserve `SessionId` so the session entity accepts the resend.
  Non-session paths unchanged.
  ✅ Verified against the Azure Service Bus emulator — `tests/Vibes.ASBManager.Tests.Integration` (5
  tests: session queue & subscription purge, session-DLQ purge, session replay round-trip, plain-queue
  regression). The pure replay-message mapping is also covered by a unit test.

- [x] **A2 — Push snapshot paging into the infra layer (one receiver) `[M]`** — ✅ shipped. Added
  `Peek{Queue,Subscription}[DeadLetter]SnapshotAsync` to `IMessageBrowser` /
  `AzureServiceBusMessaging`: each creates one receiver and pages with `MessageSnapshotPager`
  (relocated to the Application layer so the infra reuses the unit-tested loop), passing the pager's
  anchor straight to `PeekMessagesAsync` on that single receiver. `EntitiesView` calls the snapshot
  methods directly and the per-page `PeekSelectedMessagesAsync` is gone, so a 500-message refresh
  issues one receiver instead of ~10. Verified by emulator peek-paging tests (queue / queue-DLQ /
  subscription) that force several short pages — the original DLQ partial-batch bug class.

- [x] **A3 — Purge drains to empty, with progress `[S]`** — ✅ shipped. Purge no longer stops at a
  hardcoded 1000: it drains until the entity is empty, bounded by a high safety ceiling
  (`MessagingDefaults.PurgeCeiling` = 100,000) so a live/huge entity still terminates and reports
  rather than looping. The four purge methods take an `IProgress<int>` and report the running total;
  `EntitiesView` shows a live "Purging… N" count on the button, disables the other actions while a
  purge runs, and cancels it on dispose. If the ceiling is hit the toast says more may remain. The
  four near-identical drain loops are unified behind one helper. Emulator tests verify drain-to-empty
  with progress and that an explicit cap is honoured exactly. (The drain tolerates transient empty
  `ReceiveAndDelete` batches — a real gap the tests surfaced — before concluding the entity is empty.)

## B. Client & connection lifecycle

- [x] **B1 — Share the infra singleton across its interfaces `[S]`** — ✅ shipped (one instance per concrete, forwarded to its interfaces; tests assert it)
  `AddAzureServiceBusInfrastructure` registers the same class under 4 interfaces as 4 separate
  singletons → four `AzureServiceBusMessaging` (and four `AzureServiceBusAdmin`) instances, each
  with its own client cache, so up to 4× the AMQP connections.
  *Where:* `Infrastructure.AzureServiceBus/DependencyInjection.cs`.
  *Approach:*
  ```csharp
  services.AddSingleton<AzureServiceBusMessaging>();
  services.AddSingleton<IMessageBrowser>(sp => sp.GetRequiredService<AzureServiceBusMessaging>());
  // …same instance for IMessageSender, IMessageMaintenance, IDeadLetterMaintenance; ditto Admin
  ```
  *Done when:* one instance per concrete class is shared across all its interfaces.

- [ ] **B2 — Evict cached clients on connection edit/delete `[S]`**
  Clients are cached by connection string and disposed only at shutdown; editing/deleting a
  connection leaves the old `ServiceBusClient` alive. *Where:* `GetClient` caches in
  `AzureServiceBusMessaging` / `AzureServiceBusAdmin`; connection CRUD in `JsonConnectionStore` /
  `Connections.razor`. *Approach:* expose an eviction hook keyed by connection string, called on
  save/delete (cleaner after B3).

- [ ] **B3 — Connection-handle abstraction + Microsoft Entra ID auth `[L]`**
  Every infra method takes a raw `connectionString`, coupling all layers to SAS. A handle (keyed by
  connection Id) would centralize client creation/eviction, shrink signatures, and enable
  `DefaultAzureCredential` / `TokenCredential` auth (`new ServiceBusClient(fqNamespace, credential)`)
  — important for orgs that disable SAS. *Where:* `ConnectionInfo` (add auth-mode + namespace),
  all infra method signatures, `AddConnectionDialog`. *Done when:* a namespace can be added and
  browsed using Entra ID instead of a connection string.

## C. Blazor component

- [x] **C1 — Decompose `EntitiesView` `[L]`** — ✅ shipped (v1.9.26), verified against a live
  namespace. The browse / live / counts / purge state + operations are extracted into a plain,
  testable `MessageBrowsingController` (UI-free — it raises `StateChanged` / `Notify` rather than
  touching the renderer); `EntitiesView.MessageBrowser.cs` is now a thin adapter that delegates to it,
  and the pure bits (snapshot target, DLQ reconcile, error-toast dedup, purge text) are unit-tested.
  A further self-contained `MessagesPanel` component was **deliberately not pursued**: the action bar
  is shared between message and entity actions, so splitting it would force either a visible UI change
  or cross-component `@ref` wiring for little gain over the controller extraction.

- [x] **C2 — Replace the polling loops with `PeriodicTimer` `[M]`** — ✅ shipped: the three loops
  (active, dlq, counts) now run through one shared `RunPollLoop` helper using `PeriodicTimer` (no
  per-iteration `Task.Delay`). **Descoped:** truly merging the three loops into one — the shared
  helper removes the duplication, and a real merge changes live-mode timing/UX that isn't
  runtime-verifiable without a live namespace. Low value to pursue further.

- [x] **C3 — De-duplicate error toasts `[S]`** — ✅ shipped: refresh failures surface once per
  distinct message (tracked per active/dlq/counts op, reset on success), so a dropped connection no
  longer spams a toast every 2s. Interval back-off deferred (low value for a local tool).

- [x] **C4 — Tighten background-thread state access `[S]`** — ✅ shipped (`_disposed` is now `volatile`; `_refreshing*` flags use atomic `Interlocked` guards)
  `_disposed` / `_refreshing*` are plain fields touched by background tasks and the render thread.
  *Approach:* mark `_disposed` `volatile` and/or funnel state transitions through `InvokeAsync`.

## D. Testing & CI

- [x] **D1 — Service Bus emulator integration tests `[M]`** — ✅ largely shipped. `tests/emulator`
  (Docker compose + config) plus `Vibes.ASBManager.Tests.Integration` cover session
  purge/DLQ/replay (A1) and peek paging incl. the DLQ partial-batch bug class (A2), run against the
  real emulator. The suite is in the solution and **auto-skips** when no emulator is reachable.
  Deliberately **not wired into CI** (the emulator is amd64-only and adds minutes per run); it's a
  local / pre-release gate — see `tests/emulator/README.md`. *Remaining (optional):* run it in CI
  behind an opt-in if we ever want it gating PRs.

- [ ] **D2 — Keep extracting pure logic for unit tests `[S, ongoing]`**
  Continue pulling testable logic out of components (as with `MessageSnapshotPager`): connection
  string parsing edge cases, rule formatting, settings mapping.

## E. Packaging / ops

- [x] **E1 — Add a container healthcheck `[S]`** — ✅ shipped (TCP liveness probe in the Dockerfile,
  env-agnostic, no extra image deps). ⚠️ The "default to Production" half is **deferred**: the app has
  no `/Error` page, so Production's `UseExceptionHandler("/Error")` would 404. Revisit alongside adding
  an `/Error` page; Development stays the default for now (detailed errors suit a local tool).

- [x] **E2 — Run the container as non-root `[S]`** — ✅ shipped (runs as the image's `app` user; `App_Data` owned by `app`, `chmod 700`)
  `Dockerfile` does `chown 0:0 … && chmod -R 777 /app/App_Data`. *Approach:* run as the aspnet
  image's non-root `app` user with `chmod 700` on the data dir.

- [x] **E3 — Drop the obsolete compose `version:` key `[S]`** — ✅ shipped (removed from `docker-compose.yml` and the README example)
  `version: "3.9"` is ignored by Compose v2 — remove it.

- [x] **E4 — Remove unused `DataProtection.Abstractions` `[S]`** — ✅ shipped
  Referenced in `Infrastructure.Storage.File.csproj` but unused in code. Drop it (it'd be the hook
  *if* at-rest encryption is ever added — but that's intentionally out of scope).

---

*Living document — add findings as they surface; tick boxes as items ship.*
