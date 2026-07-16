# Meadow Framework — Code Review & Improvement Plan

Reviewed: 2026-07-14, branch `master` (df214f4). Scope: `Meadow_Framework.Core`, `Meadow_Framework.Tests`, recent domain-event/outbox work.

## Current state

The solution contains three projects (`Meadow_Framework.Core`, `Meadow_Framework.Tests`, `Meadow_Framework.Analyzer`). The `src/Meadow.Framework.*` and root `Meadow.*` folders are empty scaffolding that is not part of the solution. **`Meadow_Framework.Tests` does not compile** — the generic `IOutboxRepository<TDbContext>` / `OutboxJob<TDbContext>` refactor broke `EventDispatcherTests` and `OutboxJobTests` (3 × CS0305).

---

## Phase 0 — Restore a green build (do first)

1. **Fix test compilation.** `EventDispatcherTests.cs:18`, `OutboxJobTests.cs:17,20` still use the non-generic `IOutboxRepository` / `OutboxJob`. Update them to `IOutboxRepository<TestDbContext>` / `OutboxJob<TestDbContext>` (the tests already have an in-memory fixture context to use).
2. Add a CI gate (GitHub Actions: `dotnet build` + `dotnet test`) so a broken master can't recur.

## Phase 1 — Wire up the domain-event/outbox pipeline (it is currently dead code)

The feature added in 950e43b/df214f4 never runs in a consuming app:

1. **No interceptor is ever registered.** There is no `AddInterceptors(...)` call anywhere; `DomainEventEntitiesInterceptor`, `InsertOutboxMessagesInterceptor`, `UpdateAuditableEntitiesInterceptor`, `UpdateDeletableEntitiesInterceptor` are all unreferenced. Re-enable DbContext registration in `AddFramework` (the whole `AddDbContext` block in `Extensions.cs:66-96` is commented out) and attach the interceptors via `options.AddInterceptors(sp.GetRequiredService<...>())`. Register the interceptors themselves in DI.
2. **`IOutboxRepository<TDbContext>` is never registered**, but `OutboxJob<TDbContext>` is scheduled per context every 10 s and resolves it with `GetRequiredService` — every job execution will throw. Register `IOutboxRepository<TCtx>` → `OutboxRepository<TCtx>` for each discovered context.
3. **`EventDispatcher.PublishIntegrationEventAsync` is a no-op** (body fully commented out, `EventDispatcher.cs:60-84`). The outbox job "publishes" into the void and never marks messages `Completed`, so the same batch is reprocessed forever. Reinstate the MassTransit publish + state transition, inside a try/catch with retry/poison handling.
4. **Decide on ONE domain-event strategy.** `DomainEventEntitiesInterceptor` (dispatch in-process handlers) and `InsertOutboxMessagesInterceptor` (persist to outbox) both drain and clear the same `_domainEvents` list — if both are attached, whichever runs first starves the other. Recommended model:
   - Domain events → in-process handlers, dispatched **within the same scope/transaction** as `SaveChanges`.
   - Integration events → outbox rows in the same transaction, published by `OutboxJob`.
5. **Handler scope is wrong.** `EventDispatcher.PublishDomainEventAsync` creates a fresh scope from the root provider (`EventDispatcher.cs:32`), so handlers get a *different* DbContext than the one being saved: their entity changes are silently lost and they can't join the ambient transaction. Resolve handlers from the current scope (inject `IServiceProvider` scoped, or resolve handlers directly), and dispatch *before* the save completes so handler changes are included, or after commit for post-commit semantics — but pick one and document it.
6. **`needEvents` parameter of `AddFramework` is ignored** — the event bus (RabbitMQ + Quartz) is always registered, so any app without RabbitMQ config crashes (`config!.Host` NRE in `ConfigureRabbitMq`, `Extensions.cs:345`). Honor the flag and validate `RabbitMqOptions` with a clear error.

## Phase 2 — Correctness bugs (independent of wiring)

1. **`AggregateRoot<TId>.Id` shadows `EntityBase<TId>.Id` and is never assigned** (`AggregateRoot.cs:65`). The constructor sets the *base* `Id`; reads through the derived type (and EF Core's mapping) see the `new` property, which stays `default`. Every `OutboxMessage` gets `Guid.Empty` as PK → the second insert fails. Delete the `new Id` property.
2. **`EntityBase.operator ==` returns `false` for `null == null`** (`EntityBase.cs:46-49`). Use `first is null ? second is null : first.Equals(second)`.
3. **`RepositoryBase.GetPaginatedAsync`** (`RepositoryBase.cs:271-290`):
   - Starts `LongCountAsync` and a second query on the same `DbContext` concurrently → `InvalidOperationException` ("a second operation started on this context"). Await sequentially.
   - `.Include(p => p)` is not a navigation and throws at runtime. Remove it.
   - The local `query` variable is built and never used.
4. **`InsertOutboxMessagesInterceptor`** (`InsertOutboxMessagesInterceptor.cs`):
   - `_ = ConvertDomainEventsToOutboxMessages(...)` is fire-and-forget async racing `SaveChanges`; the method's `Task<Task>` signature is a bug in itself. The work is synchronous in nature — build the list and `context.Set<>().AddRange(...)` synchronously.
   - **Double serialization**: it passes an already-serialized JSON string into the `OutboxMessage(object message, ...)` constructor, which serializes it *again* and records `Type = System.String`. `RecreateMessage()` then returns a string, not an event. Pass the event object once.
5. **Insecure deserialization**: `TypeNameHandling.All` (interceptor) and `Type.GetType(dbValue)` in `OutboxMessage.RecreateMessage` deserialize attacker-influencable DB data into arbitrary types — a known RCE gadget vector, and version-brittle. Use a whitelisted event-type registry (map event name → CLR type) and `System.Text.Json`.
6. **`UnitOfWork.Dispose` disposes the DI-owned DbContext** (`UnitOfWork.cs:34-38`) — the container owns its lifetime; a consumer disposing the UoW kills the context for the rest of the scope. Remove `IDisposable` or make it a no-op.
7. **`BaseDbContext.OnModelCreating` applies configurations from `Assembly.GetExecutingAssembly()`** — always the framework assembly, never the consumer's. Use `GetType().Assembly` (derived context's assembly) plus the framework's `OutboxMessageConfig` explicitly.
8. **`EventDispatcher` reflection per publish** — cache `MethodInfo`/compiled delegates per event type, or define a non-generic `IDomainEventHandlerWrapper` to avoid `Invoke` entirely.

## Phase 3 — Outbox robustness

- `GetAllReadyToSend` is unbounded and unordered: add `ORDER BY EventDate`, a batch limit, and an index on `(State, EventDate)` in `OutboxMessageConfig`.
- Add retry count / `ProcessedOn` / error columns and a poison state instead of retrying forever.
- The 10-second trigger interval is hardcoded in `AddEventBus`; the existing `AddJobAndTrigger<T>` (cron from configuration) is dead code — reuse it or expose the interval in options.
- Consumer receive-endpoint names are bare class names (`Extensions.cs:356`) — prefix with service/context name to avoid queue collisions between services.
- `OutboxMessage.Content` is a non-nullable `string` that is never assigned (null at runtime); `Type` has a public setter. Tighten the model.

## Phase 4 — Architecture & hygiene

1. **Layering**: `Abstractions` types reference `Infrastructure` (`IOutboxRepository<TDbContext> where TDbContext : BaseDbContext`, `IEventDispatcher` importing `Infrastructure.Events`). Abstractions should not know about EF. Either key the outbox abstraction on a marker interface (`IDbContext`) or move it to Infrastructure.
2. **Duplicate primitives**: `Abstractions/Kernel/Types/{AggregateRoot,AggregateId,EntityId,TypeId}` vs `Abstractions/Primitives/{AggregateRoot,Types/...}`, and two `IDomainEventDispatcher` (Events/ and Kernel/). Keep one hierarchy (Primitives), delete the other.
3. **Delete dead code**: the large commented blocks in `Extensions.cs`, `EventDispatcher.cs`, `OutboxJob.cs`, `AggregateRoot.cs`; the empty scaffold folders (`src/*`, `Meadow.Abstractions`, `Meadow.Core`, `Meadow.EntityFrameworkCore`, `Meadow.AspNetCore`, `Meadow.MassTransit`) — or move real code into them, but don't keep both. If the multi-package split (Abstractions / EFCore / AspNetCore / MassTransit) is the goal, do it as its own phase after the code is healthy.
4. **Options pattern**: replace magic configuration paths (`AppConfiguration:RabbitMQ`, `AppConfiguration:Quartz:*`) with typed, validated options (`services.AddOptions<RabbitMqOptions>().BindConfiguration(...).ValidateOnStart()`).
5. Standardize on `System.Text.Json` (Newtonsoft is used only in the outbox path while MassTransit is configured with STJ).
6. Fix the 144 XML-doc warnings or scope `GenerateDocumentationFile` down; several docs are wrong (e.g. `DomainEventEntitiesInterceptor` says "converts domain events into outbox messages" — it dispatches handlers).

## Phase 5 — Testing & release readiness

- Restore/extend tests: interceptor behavior (events dispatched exactly once, cleared after save, not dispatched on failed save), outbox round-trip (serialize → job → publish → state `Completed`), repository pagination, `EntityBase` equality including nulls.
- Add a minimal sample app (`Meadow.Sample.Api`) exercising `AddFramework` end-to-end — it would have caught the unregistered-repository and no-op-publisher issues immediately.
- Only then: package split + NuGet publishing (MinVer is already configured in `Directory.Build.props`).

## Suggested order of attack

| Step | Content | Why first |
|------|---------|-----------|
| 0 | Fix test compilation, add CI | Master is red |
| 1 | Phase 2 items 1–4 (Id shadowing, equality, pagination, interceptor race) | Silent data corruption |
| 2 | Phase 1 (wire interceptors, repositories, real publisher) | The headline feature doesn't run |
| 3 | Phase 3 (outbox robustness) + Phase 2 item 5 (deserialization security) | Production safety |
| 4 | Phases 4–5 (architecture, cleanup, sample, packaging) | Sustainability |
