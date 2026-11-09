# RaceDay

## System Description

RaceDay is a full-stack, web-based event management system for South Africa's road running, walking, and cycling community. It replaces paper-based registration, spreadsheets, and disconnected communication with a single platform where Event Organisers can create and manage events and categories and capture participant results, while Participants can browse events, enter events under a category, view their own enrolments, and track their personal race results.

The project is built progressively across three parts. This repository currently covers:

- **Part 1:** system architecture and database planning (ERD, API endpoint plan, SQL script in `/docs`).
- **Part 2:** the RESTful API (ASP.NET Core Web API in C#, Entity Framework Core Code-First on SQL Server, session-based role authentication, Swagger, unit tests and CI/CD).
- **Part 3:** the ASP.NET Core MVC web client (`RaceDay.Web`) that consumes the API, with JWT/session login, role-based navigation, Azure Blob image uploads, Docker and CI.

## User Roles

- **Organiser:** can create, edit, and delete events; manage event categories; capture/record participant results; and view all participant enrolments.
- **Participant:** can register/login; browse events and categories; enter/enrol in events under a category; view their own enrolments; and track their personal race results.

Role-based access is enforced at the API level: a request with no session returns `401 Unauthorized`, and a logged-in user calling an endpoint for the other role returns `403 Forbidden`.

---

## Part 1: Planning Documents (`/docs`)

- [`ERD.png`](./docs/ERD.png): Entity Relationship Diagram (6 entities: Role, User, Event, Category, Enrolment, Result) with primary keys, foreign keys, and cardinalities.
- [`endpoints.md`](./docs/endpoints.md): API endpoint specification plan covering authentication, user profile, events, categories, enrolments, and results.
- [`schema.sql`](./docs/schema.sql): SQL Server script that creates all tables with constraints and seeds realistic sample data (organisers, participants, events, categories, enrolments, and results).

### Part 1 Video Presentation

Unlisted YouTube walkthrough of the planning documents, ERD decisions, endpoint plan choices, and a live run of the SQL script in SSMS:

[Watch here](https://youtu.be/pel2TtPONrU?si=vZKNJygwRrMd79dU)

---

## Part 2: RESTful API

### Project structure

```
RaceDay.sln
RaceDay.Api/                  ASP.NET Core 8 Web API
  Models/                     Role, User, Event, Category, Enrolment, Result (match the ERD)
  Data/RaceDayDbContext.cs    EF Core Code-First context (matches docs/schema.sql)
  DTOs/                       Request and response records
  Services/                   PBKDF2 password hasher, session key constants
  Authorization/              RequireSessionAttribute (session + role checks)
  Controllers/                Auth, Users, Events, Categories, Enrolments, Results
RaceDay.Api.Tests/            xUnit tests (WebApplicationFactory + EF Core InMemory)
docs/                         Part 1 planning documents
.github/workflows/            CI/CD workflows
```

### Setup and run instructions

**Prerequisites:** .NET 8 SDK, SQL Server (LocalDB, Express or full), and Visual Studio 2022 (or the `dotnet` CLI).

1. Clone the repository and open `RaceDay.sln` in Visual Studio.
2. Set your SQL Server connection string in `RaceDay.Api/appsettings.json` under `ConnectionStrings:DefaultConnection`.
3. Create the database from the Code-First model. From the `RaceDay.Api` folder run:
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
   This creates the schema defined in `Data/RaceDayDbContext.cs` and seeds the two roles (Organiser, Participant).
4. Run the API:
   ```
   dotnet run --project RaceDay.Api
   ```
5. Open Swagger UI at `https://localhost:<port>/swagger`. Every endpoint is listed with its purpose, request body and response codes.
6. Try it out: call `POST /api/auth/register`, then `POST /api/auth/login`. Swagger UI keeps the session cookie in your browser, so role-protected endpoints (for example `POST /api/events` as an Organiser) work straight afterwards.

### Authentication and role-based access

- Users register by providing their details and choosing a role (Organiser or Participant).
- On login the API stores the user's id and role in server-side session state, tracked by an HTTP-only session cookie (`RaceDay.Session`). The authenticated state and role persist across subsequent requests.
- `[RequireSession]` requires any logged-in user. `[RequireSession("Organiser")]` and `[RequireSession("Participant")]` also restrict the endpoint to that role.
- Passwords are never stored in plain text. They are hashed with PBKDF2-SHA256 (100,000 iterations, random salt per user) in `Services/PasswordHasher.cs`.
- Organisers can only edit or delete their own events, categories and results, and can only view enrolments for their own events. Participants can only view or cancel their own enrolments.

### Endpoints

The API implements every endpoint from the Part 1 plan in [`docs/endpoints.md`](./docs/endpoints.md), covering authentication, user profile, events, categories, event enrolments and results. All of them can be viewed and tested in Swagger UI.

**Note on a deliberate change from the Part 1 plan:** the Part 1 plan described JWT bearer-token authentication. The Part 2 brief requires session management to maintain the user's authenticated state and role, so the API uses cookie-backed session authentication instead. All routes, roles and response codes are otherwise unchanged, and the change is explained at the top of `endpoints.md`.

### Unit tests

Run all tests with:

```
dotnet test RaceDay.sln
```

The tests start the real API with `WebApplicationFactory` against an in-memory database, so no SQL Server instance is needed. Each area has both success and failure cases:

- **Registration and login:** valid registration, missing password, duplicate email, wrong password, unknown email.
- **Event management:** an Organiser can create an event; a Participant is rejected (403); an unauthenticated request is rejected (401); an Organiser cannot delete another Organiser's event (403).
- **Event enrolments:** a Participant can enrol and the enrolment is recorded and returned by `GET /api/enrolments/me`; duplicate enrolment is rejected (409); an Organiser cannot enrol (403).
- **Results:** the owning Organiser can capture a result; a second result for the same enrolment is rejected (409); a Participant cannot capture a result (403); the leaderboard is publicly readable.

### CI/CD

- [`.github/workflows/dotnet-ci.yml`](./.github/workflows/dotnet-ci.yml) restores, builds the solution and runs all unit tests on every push and pull request.
- [`.github/workflows/validate-docs.yml`](./.github/workflows/validate-docs.yml) (from Part 1) checks that the `/docs` folder exists and contains the ERD, endpoint plan and SQL script.

**Successful green build (Part 2):**

![Part 2 CI/CD passing](./docs/ci-success-part2.png)

### Part 2 Video Presentation

Unlisted YouTube walkthrough of the running API, code structure, endpoints, authentication and unit tests:

[Watch here](https://youtu.be/YOUR-PART2-VIDEO-ID)

---

## Part 3: MVC Web Client (`RaceDay.Web`)

The MVC app contains **no DbContext and no SQL**. Every read and write goes through the Part 2 API using a typed `HttpClient` from `IHttpClientFactory` (`Services/RaceDayApiClient.cs`).

```
Browser --> RaceDay.Web (MVC, session holds JWT + role) --> RaceDay.Api (REST) --> SQL Server
                                                                       \--> Azure Blob Storage (images)
```

### What it does

- **Login/session:** on login the API returns a JWT. MVC stores the token, role, user id and name in server-side session (HTTP-only cookie) and sends the token as `Authorization: Bearer` on every API call.
- **Role-based navigation:** anonymous users see Browse/Login/Register; Organisers see Dashboard and My events (categories, participants, results); Participants see My enrolments and My results. Everyone logged in gets Profile.
- **Route protection:** the `[RequireLogin("Organiser")]` / `[RequireLogin("Participant")]` action filter redirects anonymous users to the login page (with a return URL) and sends the wrong role to an Access Denied page. Expired API tokens (401) log the user out cleanly.
- **Organiser:** dashboard metrics, event CRUD with banner upload, category setup, participant lists (filter by category), results capture (HH:MM:SS + position, editable).
- **Participant:** filterable race browsing (search, date, type), one-click enrolment, My Enrolments (active/past, cancel), My Results ("47th out of 312 finishers"), profile with picture upload.
- **Azure Blob rule:** MVC never talks to Azure. It forwards the multipart file to the API (`POST /api/events/{id}/banner`, `POST /api/users/{id}/profile-picture`); the API uploads to Azure Blob Storage and saves the URL.
- **UX:** Bootstrap 5, status badges, `asp-validation-for` client and server validation, friendly error pages and TempData alerts when the API is unreachable.

### Running locally (Visual Studio / CLI)

1. Start the API (profile `http`, Development): `dotnet run --project RaceDay.Api` (listens on `http://localhost:5100`).
2. Start the MVC app: `dotnet run --project RaceDay.Web` (opens `http://localhost:5200`).
3. The API address is `ApiSettings:BaseUrl` in `RaceDay.Web/appsettings.json`.

### Docker (single command)

Run the API on your host first (step 1 above), then from the repository root:

```
docker build -t raceday-mvc ./RaceDay.Web && docker run --rm -p 8080:8080 raceday-mvc
```

Open `http://localhost:8080`. The container reaches the host API through `ApiSettings__BaseUrl=http://host.docker.internal:5100/` (set in the Dockerfile). On Linux add `--add-host=host.docker.internal:host-gateway` to `docker run`. To point at another API: `-e ApiSettings__BaseUrl=https://your-api.example.com/`.

The `Dockerfile` (in `RaceDay.Web/`) is multi-stage: the .NET 8 SDK image restores and publishes, and only the published output is copied into the slim `aspnet:8.0` runtime image.

### Azure Blob Storage configuration (API side)

Set these for `RaceDay.Api` (user-secrets or environment variables, never commit them):

```
dotnet user-secrets set "AzureBlob:ConnectionString" "<storage account connection string>" --project RaceDay.Api
```

`AzureBlob:ContainerName` defaults to `raceday-images`. The container is created with public blob read access so image URLs work in `<img>` tags (the storage account must allow blob public access). Without a connection string, uploads return a friendly 503 and everything else keeps working.

**Change the JWT signing key** (`Jwt:Key`, at least 32 characters) before deploying; the value in `appsettings.json` is for development only.

### Changes to Parts 1-2 made for Part 3

- Events gained `DistanceKm`, `EventType` (Run/Walk/Cycle) and `BannerUrl`; users gained `ProfilePictureUrl`. `docs/schema.sql`, `docs/ERD.png` and `docs/endpoints.md` were updated to match. Run a new EF migration (`dotnet ef migrations add Part3Columns`) if you already created the database.
- The API accepts a JWT bearer token as well as the Part 2 session cookie; Part 2 behaviour and tests are unchanged.

### CI/CD

[`.github/workflows/mvc-ci.yml`](./.github/workflows/mvc-ci.yml) builds `RaceDay.Web` and then builds its Docker image on every push. `dotnet-ci.yml` also builds the whole solution (now including the MVC project).

**Successful green build (Part 3):** add a screenshot as `docs/ci-success-part3.png`.

![Part 3 CI/CD passing](./docs/ci-success-part3.png)

### Part 3 Video Presentation

Unlisted YouTube walkthrough of the MVC app, role-based flows, Azure Blob upload, Docker run and CI:

[Watch here](https://youtu.be/YOUR-PART3-VIDEO-ID)

---

## AI Disclosure

AI assistance (Claude, Anthropic) was used in this project. For Part 1 it helped draft the ERD structure, the endpoint specification table and the SQL script. For Part 2 it helped draft the API code (EF Core model, controllers, session-based authorization filter), the unit tests and the CI workflow. For Part 3 it helped draft the MVC client (controllers, views, API client, action filter), the upload endpoints, the Dockerfile and the CI workflow. All AI-assisted work was reviewed, understood and adapted by the author, who is responsible for the final submission. No AI-generated voiceover was used in any video presentation.
