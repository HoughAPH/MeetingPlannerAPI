# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

MeetingPlanner take-home: a calendar/scheduling backend API. It is an ASP.NET Core Web API on `net10.0` with controller-based routing. It is a single project with folder-based DDD layers.

Repository layout: the solution root (`MeetingPlannerAPI.slnx`, `CLAUDE.md`) holds the API project `MeetingPlannerAPI/` (with `Program.cs`, the layer folders and the csproj). The xUnit test project `MeetingPlannerAPIProject.Tests/` sits beside it, and `PLAN.md` and a concurrency write-up may be regenerated later. Run `dotnet run` and `dotnet ef` from `MeetingPlannerAPI/`.

Implemented so far: the `Meeting` aggregate, the `Participant` entity, `POST /api/meetings` (returns a `Location` header), `GET /api/meetings/{id}` (returns the `ETag`; 404 if unknown or cancelled), `GET /api/meetings` (filters and text search), `PUT /api/meetings/{id}` (full replace with optimistic concurrency) and `DELETE /api/meetings/{id}` (soft cancel), plus `GET /api/availability` (advisory participant availability, warn-only). Notifications are sent through `INotificationService` on create, update and cancel (`LoggingNotificationService` just writes to the console). Accept/reject (`Participant.IsAttending`) is planned but not built. Tests are in `MeetingPlannerAPIProject.Tests/` (see Testing).

## Commands

```
dotnet build MeetingPlannerAPI.slnx
dotnet run --launch-profile http     # http://localhost:5169
dotnet run --launch-profile https    # https://localhost:7227 (also serves http://localhost:5169)
dotnet test                          # run a single test: dotnet test --filter "FullyQualifiedName~TestName"
dotnet ef migrations add <Name> --output-dir Infrastructure/Persistence/Migrations
```

Both launch profiles set `ASPNETCORE_ENVIRONMENT=Development`, which maps the OpenAPI document (`/openapi/v1.json`) and applies EF migrations on startup. Data lives in a SQLite file, `MeetingPlanner.db`, which is git-ignored. `MeetingPlannerAPI.http` has sample requests.

The `InitialCreate` migration lives in `Infrastructure/Persistence/Migrations`. Do not use `dotnet run --no-build` after adding a migration without rebuilding first. The run will use a stale binary and report "no such table".

## Architecture

Layers are top-level folders and namespaces in the one project. Nothing but convention enforces the dependency direction: `API → Application → Domain`, and `Infrastructure → Application/Domain`. Domain must not reference any other layer.

- `Domain/`: `Meeting` (aggregate root) owns its `Participant` children. Only `Meeting.Create` can construct a meeting, and it enforces every invariant by throwing `DomainException` with a stable `Code`. Field limits live in `MeetingLimits` and are shared by the domain, the API validation attributes and the EF mapping. `IMeetingRepository` is the port.
- `Application/`: one hand-written handler per use case (`CreateMeetingHandler`, `CancelMeetingHandler`, `SearchMeetingsHandler`, `CheckAvailabilityHandler`) with record commands, queries and DTOs. There is no MediatR. Handlers are registered in `AddApplication()` and injected directly into the controller. Handlers only see UTC times.
- `Infrastructure/`: EF Core with SQLite. `AppDbContext` also implements `IUnitOfWork`. `MeetingRepository.SearchAsync` implements overlap semantics (a meeting matches `from`/`to` if it ends after `from` and starts before `to`). Text search uses `EF.Functions.Like` with escaping. Registered in `AddInfrastructure()`.
- `API/`: `MeetingsController`, request and response contracts, and `DomainExceptionHandler`, which turns `DomainException` and `RequestValidationException` into 400 problem details. Contracts accept `DateTimeOffset` and are normalised to UTC in `ContractMappings`.

Conventions that are easy to miss:
- Cancelling is a soft delete: `Meeting.Cancel()` sets `CancelledAtUtc` and returns `false` if already cancelled. `CancelMeetingHandler` only notifies when the state actually changed, so repeated DELETEs return 204 without re-notifying. `MeetingRepository.SearchAsync` filters out cancelled meetings; `GetByIdAsync` does not.
- `Meeting.Version` is incremented by the domain, not the database, and is an EF concurrency token. Every `Meeting` mutation must call `Version++`, including participant-only changes. `AppDbContext.SaveChangesAsync` converts `DbUpdateConcurrencyException` to `ConcurrencyConflictException`, which `DomainExceptionHandler` maps to 412. `PUT` requires `If-Match` (428 without it); `ETags` formats and parses the header.
- All times are UTC `DateTime` in the domain. SQLite returns `Kind=Unspecified`, so the EF mapping uses a value converter that restores `Utc`.
- Overlapping meetings are allowed; nothing blocks them. Availability is advisory only: `CheckAvailabilityHandler` asks `IMeetingRepository.FindParticipantConflictsAsync` (same overlap rule as search, cancelled meetings ignored, optional `excludeMeetingId`) which participants are on other overlapping meetings. `MeetingsController` calls it after a successful create or update and adds `availabilityConflicts` to the response (omitted when empty). It runs after the save, so it is a warning, not a guarantee. The conflict list is sorted in memory because EF cannot translate `OrderBy` over the projected record.
- Participant emails are stored lower-cased, are unique per meeting (domain check plus a unique index), and the search filter lower-cases its input before comparing.
- `public partial class Program;` at the end of `Program.cs` is there so tests can use `WebApplicationFactory<Program>`.

## Testing

`MeetingPlannerAPIProject.Tests/` is an xUnit project (net10.0) that references the API project. Run it with `dotnet test MeetingPlannerAPI.slnx`.

- `MeetingDomainTests`: `Meeting`/`Participant` invariants, error codes, `Version` increments and participant reconciliation.
- `CancelMeetingHandlerTests`: the handler against hand-written fakes (repository, unit of work, notifier), including notify-once and the lost-race cases.
- `CheckAvailabilityHandlerTests`: the availability handler against a fake repository (email normalisation, grouping of conflicts per participant, window and limit validation).
- `MeetingConcurrencyTests`: two `AppDbContext`s on one SQLite file reproduce the simultaneous-save race.
- `MeetingsApiTests`: end-to-end through `WebApplicationFactory<Program>`. Each test class instance overrides `ConnectionStrings:Default` with a throwaway temp SQLite file, so `MeetingPlanner.db` is never touched.

## Tooling notes

`.claude/` and `.mcp.json` exist at both the solution root and in `MeetingPlannerAPI/`. They hold Visual Studio integration hooks and a shim (`vs-*.ps1`), and `.mcp.json` configures the VS MCP servers. These support debugging and semantic tools from Visual Studio and are not part of the application.
