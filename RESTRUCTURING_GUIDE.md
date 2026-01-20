# Meadow Framework Restructuring Guide

## Project Status

This document outlines the restructuring of the Meadow Framework to follow the recommended Clean Architecture pattern with proper separation of concerns.

### ✅ Completed Steps

1. **Created New Abstraction Packages**
   - `Meadow.Framework.Domain.Abstractions` - Pure domain concepts (entities, aggregates, value objects, domain events)
   - `Meadow.Framework.Application.Abstractions` - CQRS contracts (commands, queries, handlers, dispatchers)
   - `Meadow.Framework.Infrastructure.Abstractions` - Infrastructure contracts (persistence patterns, cross-cutting concerns)
   - `Meadow.Framework.IntegrationEvents` - Integration events domain model
   - `Meadow.Framework` - Meta package (convenience reference to all components)

2. **Created Core Domain Abstractions**
   - `EntityBase<TId>` - Base class for all entities
   - `ValueObject` - Base class for value objects
   - `AggregateRoot<TId>` - Base class for aggregate roots
   - `IAggregateRoot` - Interface for aggregate root contracts
   - `IEvent` & `IDomainEvent` - Event interfaces
   - `IRepositoryBase<TEntity, TId>` - Repository interface
   - `IUnitOfWork` - Unit of work interface
   - `Specification<TEntity, TId>` - Specification pattern base class

3. **Created CQRS Abstractions**
   - `IMessage` & `IMessage<T>` - Base message interfaces
   - `ICommand` & `ICommand<T>` - Command interfaces
   - `IQuery` & `IQuery<T>` - Query interfaces
   - `ICommandHandler<T>` & `ICommandHandler<T, TResult>` - Command handler interfaces
   - `IQueryHandler<T, TResult>` - Query handler interface
   - `ICommandDispatcher` - Command dispatcher interface
   - `IQueryDispatcher` - Query dispatcher interface
   - `IDispatcher` - Combined dispatcher interface

4. **Updated Solution File**
   - Added 5 new projects to the solution with unique GUIDs
   - Added corresponding build configurations for each new project
   - Organized new packages in `/src` folder structure

### 📋 Remaining Work

The following tasks still need to be completed to fully implement the recommended architecture:

#### Phase 1: Complete Domain Abstractions
- [ ] Create domain value type interfaces (TypeId, EntityId, AggregateId)
- [ ] Create audit entity interfaces (IAuditableEntity, IDeletableEntity)
- [ ] Move Primitives/Types files to proper locations
- [ ] Create sample domain event base classes

#### Phase 2: Complete Application Layer
- [ ] Create pagination support interfaces (IPagedQuery)
- [ ] Create pipeline behavior interfaces for cross-cutting concerns
- [ ] Create notification/event handler interfaces for domain event handling

#### Phase 3: Complete Infrastructure Abstractions
- [ ] Create persistence abstractions (IDbContext, IRepository)
- [ ] Create interceptor abstractions
- [ ] Create security abstractions
- [ ] Create seed data abstractions

#### Phase 4: Create Integration Events Package
- [ ] Create `IntegrationBaseEvent` abstract class
- [ ] Create `IntegrationEventStatus` enum
- [ ] Create `OutboxMessage` entity
- [ ] Create outbox repository interfaces

#### Phase 5: Update Existing Packages
- [ ] Update `Meadow_Framework.Core` to reference new abstractions
- [ ] Update `Meadow.Framework.Mediator` to reference new abstractions
- [ ] Update `Meadow.Framework.Outbox` to reference new abstractions
- [ ] Update `Meadow_Framework.Exceptions` to reference Infrastructure.Abstractions
- [ ] Update all namespace references throughout the codebase

#### Phase 6: Update Project Dependencies
- [ ] Update all `.csproj` files with correct ProjectReferences
- [ ] Remove transitive dependency duplications
- [ ] Verify no circular references exist

#### Phase 7: Namespace Migration
- [ ] Update namespace imports in all files
- [ ] Create backward compatibility aliases (optional)
- [ ] Update all consumer projects referencing old namespaces

### 📁 Project Structure Overview

```
src/
├── Meadow.Framework.Domain.Abstractions/
│   ├── Primitives/              (EntityBase, ValueObject, AggregateRoot)
│   ├── Events/                  (IDomainEvent, IEvent)
│   ├── Repositories/            (IRepositoryBase, IUnitOfWork)
│   └── Specifications/          (Specification<T>)
│
├── Meadow.Framework.Application.Abstractions/
│   ├── Commands/                (ICommand, ICommandHandler, ICommandDispatcher)
│   ├── Queries/                 (IQuery, IQueryHandler, IQueryDispatcher)
│   ├── Dispatchers/             (IDispatcher)
│   └── Messaging/               (IMessage)
│
├── Meadow.Framework.Infrastructure.Abstractions/
│   ├── Persistence/             (IDbContext contracts)
│   ├── Interceptors/
│   ├── Security/
│   └── Seed/
│
├── Meadow.Framework.IntegrationEvents/
│   ├── Abstractions/Events/     (Integration event contracts)
│   └── Infrastructure/          (Outbox patterns)
│
└── Meadow.Framework/
    └── Extensions/              (ServiceCollection extensions)
```

### 🔗 Dependency Graph

```
Domain.Abstractions (no dependencies)
    ↑
    ├─→ Application.Abstractions
    └─→ Infrastructure.Abstractions
    └─→ IntegrationEvents
        ↑
        ├─→ Core (implementations)
        ├─→ Mediator
        ├─→ Outbox
        └─→ Exceptions
            ↑
            └─→ Framework (meta-package)
```

### 📝 Migration Checklist for Developers

When working with the new structure:

1. ✅ Reference `Meadow.Framework.Domain.Abstractions` for domain entities
2. ✅ Reference `Meadow.Framework.Application.Abstractions` for CQRS patterns
3. ✅ Reference `Meadow.Framework.Infrastructure.Abstractions` for persistence patterns
4. ⏳ Reference individual packages as needed, or use `Meadow.Framework` meta-package
5. ⏳ Update all old namespaces to new locations

### 📚 Namespace Mapping Reference

| Old Namespace | New Namespace |
|---------------|---------------|
| `Meadow_Framework.Core.Abstractions.Primitives` | `Meadow.Framework.Domain.Abstractions.Primitives` |
| `Meadow_Framework.Core.Abstractions.Events` | `Meadow.Framework.Domain.Abstractions.Events` |
| `Meadow_Framework.Core.Abstractions.Repository` | `Meadow.Framework.Domain.Abstractions.Repositories` |
| `Meadow_Framework.Core.Abstractions.Specifications` | `Meadow.Framework.Domain.Abstractions.Specifications` |
| `Meadow.Framework.Mediator.Abstractions.Commands` | `Meadow.Framework.Application.Abstractions.Commands` |
| `Meadow.Framework.Mediator.Abstractions.Queries` | `Meadow.Framework.Application.Abstractions.Queries` |
| `Meadow.Framework.Mediator.Abstractions.Dispatchers` | `Meadow.Framework.Application.Abstractions.Dispatchers` |
| `Meadow.Framework.Mediator.Abstractions.Messaging` | `Meadow.Framework.Application.Abstractions.Messaging` |

### 🎯 Key Benefits of This Structure

1. **Clear Layer Separation** - Domain, Application, and Infrastructure are clearly separated
2. **Reduced Coupling** - Each layer has minimal dependencies
3. **Easier Testing** - Abstractions can be mocked independently
4. **Better Maintainability** - Clear responsibility boundaries
5. **Scalability** - Easy to add new packages following the pattern
6. **Standards Compliance** - Follows Clean Architecture and CQRS patterns

### 🔧 Build & Verification

To verify the restructure is working:

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Verify no circular references
# (Use ReSharper or similar tool to detect)
```

### 📞 Questions & Notes

- GUIDs for new projects are placeholders - verify they're unique in your environment
- Consider creating test projects for each new package
- Plan namespace update strategy (hard break vs. forwarding aliases)
- Document breaking changes for consumers of the framework
