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
(B1 and the E-series are done — see Recently shipped above.)

1. **A1** — fix session-enabled entities (correctness gap users can hit)
2. **D1** — Service Bus emulator integration tests (would have caught the DLQ bug)
3. **C2 / C3 / C4** — polling modernization, error-toast backoff, thread-safety
4. **A2** — push snapshot paging into the infra layer (one receiver per snapshot)
5. **C1** — decompose `EntitiesView`
6. **B3** — connection-handle abstraction + Microsoft Entra ID auth (biggest feature unlock)
7. **A3 / B2 / D2** — opportunistic polish

---

## A. Azure Service Bus correctness

- [ ] **A1 — Handle session-enabled entities `[M]`**
  You can create `RequiresSession` queues/subscriptions and peek them, but purge/remove/replay
  use non-session receivers (`ReceiveMessagesAsync`) which throw on session-required entities.
  *Where:* `AzureServiceBusMessaging` (purge/remove/replay methods); `RequiresSession` surfaces in
  `AzureServiceBusAdmin` settings + create-queue.
  *Approach:* detect `RequiresSession` and use `AcceptNextSessionAsync`, or disable those actions
  in the UI for session entities. *Done when:* purge/remove/replay either work on session entities
  or are clearly unavailable, with no silent exception.

- [ ] **A2 — Push snapshot paging into the infra layer (one receiver) `[M]`**
  `PeekSnapshotAsync` currently creates a fresh receiver per page via `PeekSelectedMessagesAsync`.
  A single long-lived receiver advances its own peek cursor, which is cheaper (no per-page AMQP
  link churn) and avoids the cold-receiver partial-batch issue at the source.
  *Where:* `AzureServiceBusMessaging` (add `PeekQueueSnapshotAsync`/`...Subscription...`),
  `IMessageBrowser`, and `EntitiesView.MessageBrowser.cs`.
  *Approach:* move the `MessageSnapshotPager.CollectAsync` loop down behind the interface, owning
  one receiver. *Done when:* a 500-message DLQ refresh issues one receiver, not ~10.

- [ ] **A3 — Make purge cap configurable / loop to count `[S]`**
  Purge is a best-effort `ReceiveAndDelete` drain hardcoded at `maxMessages: 1000`; large queues
  need repeated clicks. *Where:* purge methods in `AzureServiceBusMessaging`, call sites in
  `EntitiesView.razor`. *Approach:* surface the cap as an option, or loop until the runtime count
  reaches zero with a progress indicator.

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

- [ ] **C1 — Decompose `EntitiesView` `[L]`**
  ~1,300 + ~580 lines doing tree, selection, settings, rules, browsing, sending, purging.
  *Approach:* extract a self-contained `MessagesPanel` owning the browse/live/counts state (the
  tree is already split out; `MessageSnapshotPager` is the extraction pattern to follow).

- [ ] **C2 — Replace the polling loops with `PeriodicTimer` `[M]`**
  Three hand-rolled `Task.Run` + `Task.Delay` loops (active, dlq, counts) with manual CTS juggling.
  *Where:* `EntitiesView.MessageBrowser.cs` (`StartLive*`, `StartCountsPolling`). *Approach:* use
  `PeriodicTimer`; since "live" is now snapshot-on-a-timer, fold the two live loops into the counts
  loop (refresh counts → refresh whichever list is visible).

- [ ] **C3 — Back off / de-duplicate error toasts `[S]`**
  Failures inside the 2s loops call `Snackbar.Add(...)` every tick — a dropped connection spams a
  toast every 2 seconds. *Approach:* surface one sticky error and pause/back off polling on repeated
  failures.

- [ ] **C4 — Tighten background-thread state access `[S]`**
  `_disposed` / `_refreshing*` are plain fields touched by background tasks and the render thread.
  *Approach:* mark `_disposed` `volatile` and/or funnel state transitions through `InvokeAsync`.

## D. Testing & CI

- [ ] **D1 — Service Bus emulator integration tests `[M]`**
  The whole Azure layer is `[ExcludeFromCodeCoverage]` with no integration coverage — exactly the
  gap that let the DLQ partial-batch bug ship. *Approach:* add an Azure Service Bus emulator
  (Docker) service to a new integration test project and cover peek/send/purge/replay; wire it into
  CI. *Done when:* CI exercises real peek paging against the emulator.

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
