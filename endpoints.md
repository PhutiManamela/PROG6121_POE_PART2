# RaceDay API Endpoint Specification Plan

Base URL: `/api`
Auth: ASP.NET Core session, started by `POST /api/auth/login` and tracked via a server-side session
cookie on every subsequent request (no token header). *(Updated from the original "JWT bearer token"
wording in this plan: the Part 2 brief explicitly requires session management to maintain the user's
authenticated state and role, so the API is implemented with cookie-backed session auth instead of
JWT. The endpoints, routes, roles and responses below are otherwise unchanged from this plan.)*
Roles: `None` (public, no login needed), `Any` (any logged-in user), `Organiser`, `Participant`.

## 1. Authentication

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Register a new user as Organiser or Participant. | None | `{ "firstName": "string", "lastName": "string", "email": "string", "password": "string", "role": "Organiser \| Participant" }` | 201 Created – returns new user id and role; 400 Bad Request – validation errors; 409 Conflict – email already registered |
| POST | `/api/auth/login` | Authenticate a user and issue a JWT. | None | `{ "email": "string", "password": "string" }` | 200 OK – `{ "token": "string", "userId": int, "role": "string" }`; 400 Bad Request – missing fields; 401 Unauthorized – invalid credentials |

## 2. User Profile

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/users/{id}` | View a user's own profile details. | Any (self only) | None | 200 OK – user object (no password hash); 401 Unauthorized – no/invalid token; 403 Forbidden – requesting another user's profile; 404 Not Found – id does not exist |
| PUT | `/api/users/{id}` | Edit the logged-in user's profile. | Any (self only) | `{ "firstName": "string", "lastName": "string", "email": "string" }` | 200 OK – updated user object; 400 Bad Request – validation errors; 401 Unauthorized; 403 Forbidden – editing another user; 404 Not Found |

## 3. Events

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events` | Browse all upcoming events (supports optional `?search=` and `?date=` query filters). | None | None | 200 OK – array of event objects |
| GET | `/api/events/{id}` | View full detail of a single event, including its categories. | None | None | 200 OK – event object with nested categories; 404 Not Found |
| POST | `/api/events` | Create a new event. | Organiser | `{ "name": "string", "description": "string", "eventDate": "date", "location": "string" }` | 201 Created – new event object; 400 Bad Request – validation errors; 401 Unauthorized; 403 Forbidden – non-organiser |
| PUT | `/api/events/{id}` | Update an existing event owned by the logged-in organiser. | Organiser | `{ "name": "string", "description": "string", "eventDate": "date", "location": "string" }` | 200 OK – updated event object; 400 Bad Request; 401 Unauthorized; 403 Forbidden – not the owning organiser; 404 Not Found |
| DELETE | `/api/events/{id}` | Delete an event owned by the logged-in organiser. | Organiser | None | 204 No Content; 401 Unauthorized; 403 Forbidden – not the owning organiser; 404 Not Found; 409 Conflict – event has active enrolments |

## 4. Categories

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | List all race categories for an event (e.g. 5km, 10km, 21km, Marathon). | None | None | 200 OK – array of category objects; 404 Not Found – event does not exist |
| POST | `/api/events/{eventId}/categories` | Add a new category to an event. | Organiser | `{ "name": "string", "distanceKm": number, "maxParticipants": int, "entryFee": number }` | 201 Created – new category object; 400 Bad Request; 401 Unauthorized; 403 Forbidden – not the owning organiser; 404 Not Found – event does not exist |
| PUT | `/api/categories/{id}` | Update a category's details. | Organiser | `{ "name": "string", "distanceKm": number, "maxParticipants": int, "entryFee": number }` | 200 OK – updated category object; 400 Bad Request; 401 Unauthorized; 403 Forbidden; 404 Not Found |
| DELETE | `/api/categories/{id}` | Remove a category from an event. | Organiser | None | 204 No Content; 401 Unauthorized; 403 Forbidden; 404 Not Found; 409 Conflict – category has enrolments |

## 5. Event Enrolments

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/categories/{categoryId}/enrolments` | Enter/enrol the logged-in participant into a category. | Participant | None | 201 Created – new enrolment object; 401 Unauthorized; 403 Forbidden – organiser account; 404 Not Found – category does not exist; 409 Conflict – already enrolled, or category full |
| GET | `/api/enrolments/me` | View the logged-in participant's own enrolments. | Participant | None | 200 OK – array of enrolment objects with event/category details |
| GET | `/api/events/{eventId}/participants` | View all participant enrolments for an event. | Organiser | None | 200 OK – array of participant/enrolment objects; 401 Unauthorized; 403 Forbidden – not the owning organiser; 404 Not Found |
| DELETE | `/api/enrolments/{id}` | Cancel the logged-in participant's own enrolment. | Participant | None | 204 No Content; 401 Unauthorized; 403 Forbidden – not the owning participant; 404 Not Found |

## 6. Results

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/enrolments/{enrolmentId}/result` | Capture/record the finish result for an enrolment. | Organiser | `{ "finishTime": "HH:MM:SS", "position": int }` | 201 Created – new result object; 400 Bad Request; 401 Unauthorized; 403 Forbidden – not the owning organiser; 404 Not Found – enrolment does not exist; 409 Conflict – result already recorded |
| PUT | `/api/results/{id}` | Correct a previously captured result. | Organiser | `{ "finishTime": "HH:MM:SS", "position": int }` | 200 OK – updated result object; 400 Bad Request; 401 Unauthorized; 403 Forbidden; 404 Not Found |
| GET | `/api/results/me` | View the logged-in participant's personal race results. | Participant | None | 200 OK – array of result objects with event/category details |
| GET | `/api/categories/{categoryId}/leaderboard` | View the public leaderboard/results for a category. | None | None | 200 OK – array of results ordered by position/finish time; 404 Not Found – category does not exist |
