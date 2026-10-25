# MeetingPlannerAPI

A small calendar/scheduling backend API (ASP.NET Core, `net10.0`, EF Core with SQLite).

## Purpose: a DDD and Aggregate showcase

This project exists to demonstrate **Domain-Driven Design** and the **Aggregate pattern**. It is a teaching and
take-home exercise, **not** a realistic or real-world business solution, and it should not be deployed or used as one.

What it is meant to show:

- **A single aggregate root.** `Meeting` owns its `Participant` children. Participants can only be created and changed
  through the meeting, and the repository loads and saves the whole aggregate.
- **Invariants enforced in the domain.** `Meeting.Create` and `Meeting.Update` validate everything (title, time range,
  UTC times, participant limits, duplicate emails) and throw a `DomainException` with a stable error code. The
  API layer does not re-implement those rules.
- **Aggregate-wide optimistic concurrency.** `Meeting.Version` is bumped by the domain on every change, including
  participant-only changes, and is used as the ETag (`If-Match`) and the EF concurrency token.
- **Ports and adapters.** `IMeetingRepository`, `IUnitOfWork` and `INotificationService` are defined inward and
  implemented in `Infrastructure`.
- **Hand-written use-case handlers** instead of a mediator library, to keep the flow easy to read.

### What it deliberately leaves out

This is intentionally simplified. In particular it has **no authentication or authorisation**, no users or tenants, no
calendars or time zones beyond UTC, no recurring meetings, no room or resource booking and no real notification
delivery (`LoggingNotificationService` only writes to the console). Overlapping meetings are allowed, and the
availability check is advisory only, with no guarantee against double booking. Accept/decline is a plain Outlook-style
reply: there is no organiser, no tentative answer and no notification of the reply, and because there is no
authentication anyone can answer on behalf of any participant. The design choices favour clarity of the pattern over
production concerns such as scale, security or operations.

## Structure

One project with folder-based layers. Convention, not the compiler, enforces the dependency direction
`API → Application → Domain` and `Infrastructure → Application/Domain`.

| Folder | Contents |
| --- | --- |
| `Domain/` | `Meeting` aggregate root, `Participant` entity, `MeetingLimits`, `IMeetingRepository` |
| `Application/` | One handler per use case, with commands, queries and DTOs |
| `Infrastructure/` | EF Core / SQLite persistence, migrations, notification service |
| `API/` | Controllers, request/response contracts, error handling |

## Endpoints

| Method and route | Purpose |
| --- | --- |
| `POST /api/meetings` | Create a meeting (returns `Location` and `ETag`) |
| `GET /api/meetings/{id}` | Get a meeting (returns `ETag`; 404 if unknown or cancelled) |
| `GET /api/meetings` | Search with date range, text and participant filters, paged |
| `PUT /api/meetings/{id}` | Full replace; requires `If-Match` (428 without it, 412 if stale) |
| `DELETE /api/meetings/{id}` | Soft cancel (idempotent) |
| `GET /api/availability` | Advisory check of which participants are already booked |
| `PUT /api/meetings/{id}/participants/{participantId}/response` | Accept or decline an invitation (`{ "response": "Accepted" }`); no `If-Match` needed; accepting a clashing meeting still succeeds and returns `availabilityConflicts` |

`MeetingPlannerAPI/MeetingPlannerAPI.http` has sample requests. In Development the OpenAPI document is served at
`/openapi/v1.json`.

## Running

```
cd MeetingPlannerAPI
dotnet run --launch-profile http     # http://localhost:5169
```

In Development, EF migrations are applied on startup and the SQLite file `MeetingPlanner.db` is created automatically
(it is git-ignored).

## Tests

```
dotnet test MeetingPlannerAPI.slnx
```

The xUnit project `MeetingPlannerAPIProject.Tests` covers domain invariants, handlers (with fakes), a concurrency race
on two database contexts, and end-to-end API behaviour against a throwaway SQLite file.
