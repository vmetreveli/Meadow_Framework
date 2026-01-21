# Meadow Framework

A modular .NET framework for building clean architecture applications with CQRS, Domain-Driven Design, and reliable
messaging patterns.

## Installation

Add the desired Meadow Framework packages to your project:

```bash
# Meta-package (includes all components)
dotnet add package Meadow.Framework

# Individual packages
dotnet add package Meadow.Framework.Domain.Abstractions
dotnet add package Meadow.Framework.Application.Abstractions
dotnet add package Meadow.Framework.Infrastructure.Abstractions
dotnet add package Meadow.Framework.IntegrationEvents
dotnet add package Meadow.Framework.Infrastructure
dotnet add package Meadow.Framework.Mediator
dotnet add package Meadow.Framework.Outbox
dotnet add package Meadow.Framework.Exceptions
dotnet add package Meadow.Framework.Analyzer
```

## Quick Start

### Option 1: Use the Meta-Package (Recommended)

```csharp
using Meadow.Framework;

var builder = WebApplication.CreateBuilder(args);

// Add all Meadow Framework components
builder.Services.AddMeadowFramework(
    builder.Configuration,
    typeof(Program).Assembly // Assemblies to scan for handlers
);

var app = builder.Build();
```

### Option 2: Selective Registration

Register only the components you need:

```csharp
using Meadow.Framework.Infrastructure;
using Meadow.Framework.Mediator;
using Meadow.Framework.Outbox;
using Meadow.Framework.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure services (interceptors, security)
builder.Services.AddMeadowInfrastructure(builder.Configuration);

// CQRS Mediator (commands, queries, events)
builder.Services.AddMeadowMediator(typeof(Program).Assembly);

// Reliable outbox pattern
builder.Services.AddMeadowOutbox(builder.Configuration);

// Exception handling
builder.Services.AddMeadowExceptions();

var app = builder.Build();

// Use exception handling middleware
app.UseMeadowExceptions();
```

## Component Overview

### Meadow.Framework.Infrastructure

Provides infrastructure implementations:

- **Entity Framework Interceptors**: Automatic audit trails and soft deletes
- **Security Services**: Sensitive data masking
- **Repository Implementations**: EF Core repository base classes
- **Extension Methods**: `UseMeadowInterceptors()` for DbContext configuration

```csharp
// Configure your DbContext with Meadow interceptors
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseMeadowInterceptors()); // Adds audit and soft delete interceptors
```

### Meadow.Framework.Mediator

Implements CQRS pattern with automatic handler discovery:

- **Commands**: Fire-and-forget operations
- **Queries**: Data retrieval operations
- **Events**: Domain events and integration events

```csharp
// Register mediator with handler scanning
builder.Services.AddMeadowMediator(
    typeof(Program).Assembly, // Your application assembly
    typeof(DomainLayer).Assembly // Domain assemblies
);
```

### Meadow.Framework.Outbox

Reliable messaging with the outbox pattern:

- **Transactional Outbox**: Messages stored with business data
- **Background Processing**: Automatic retry and publishing
- **Configurable Options**: Batch size, retry policies, schedules

```csharp
// Configure outbox with custom options
builder.Services.AddMeadowOutbox(builder.Configuration, options =>
{
    options.BatchSize = 50;
    options.MaxRetryAttempts = 5;
    options.ProcessingSchedule = "0/15 * * * * ?"; // Every 15 seconds
});
```

### Meadow.Framework.Exceptions

Centralized error handling:

- **Exception Middleware**: Automatic error response formatting
- **Problem Details**: RFC 7807 compliant error responses
- **Logging**: Structured error logging

```csharp
// Add exception handling
builder.Services.AddMeadowExceptions();

// Use the middleware
app.UseMeadowExceptions();
```

## Advanced Configuration

### Custom DbContext Setup

```csharp
using Meadow.Framework.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Your entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

// In Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
           .UseMeadowInterceptors()); // Automatic audit trails

builder.Services.AddScoped(typeof(IRepositoryBase<,>), typeof(RepositoryBase<AppDbContext, ,>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
```

### Custom Outbox Configuration

```csharp
builder.Services.AddMeadowOutbox(builder.Configuration, options =>
{
    options.ProcessingSchedule = "0 0/5 * * * ?"; // Every 5 minutes
    options.BatchSize = 100;
    options.MaxRetryAttempts = 3;
    options.RetryDelaySeconds = 30;
    options.EnableBackgroundProcessing = true;
});
```

## Architecture

Meadow Framework follows Clean Architecture principles:

```
Domain Layer (Domain.Abstractions)
    ↓
Application Layer (Application.Abstractions)
    ↓
Infrastructure Layer (Infrastructure, Mediator, Outbox, Exceptions)
```

## Contributing

See the main repository for contribution guidelines.