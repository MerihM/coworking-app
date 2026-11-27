# Coworking App

[![CI](https://github.com/MerihM/coworking-app/actions/workflows/ci.yml/badge.svg)](https://github.com/MerihM/coworking-app/actions/workflows/ci.yml)

Information system for managing a coworking center: members, memberships, payments, rooms,
workspaces, reservations and a manager report. Built from the design document
*"Projektovanje informacionih sistema – IS za upravljanje coworking centrom"*
(requirements FR01–FR12, NFR01–NFR05, business rules BR01–BR05, test cases TC01–TC08).

**Stack:** ASP.NET Core Web API (C#, .NET 10) · Entity Framework Core 10 · PostgreSQL 18 ·
React 19 + TypeScript · Mantine UI · Docker (database) · GitHub Actions (CI)

![Home page](docs/screenshots/01-home.png)

> **Sažetak (BS).** Informacioni sistem za upravljanje coworking centrom: članovi i članarine,
> uplate, prostorije i radna mjesta, rezervacije s provjerom poslovnih pravila (BR01–BR05) i
> izvještaj o zauzetosti i prihodima za menadžera. Backend je ASP.NET Core Web API (C#) s
> PostgreSQL bazom, frontend je React aplikacija na engleskom i bosanskom jeziku. Upute za
> pokretanje su u poglavlju [Running the app](#running-the-app), pristupni podaci za demo u
> [Demo accounts](#demo-accounts), a odstupanja od projektnog dokumenta u
> [Deviations from the design document](#deviations-from-the-design-document).
> Kod je u potpunosti komentarisan; uz projekat je priložen i dump baze (`database/coworking_db.sql`).

---

## Contents

- [Features by role](#features-by-role)
- [Screenshots](#screenshots)
- [Running the app](#running-the-app)
- [Demo accounts](#demo-accounts)
- [Database dump](#database-dump)
- [Tests](#tests)
- [Architecture](#architecture)
- [Design document → code](#design-document--code)
- [Deviations from the design document](#deviations-from-the-design-document)
- [Development process](#development-process)

---

## Features by role

| Role | What they can do | Requirements |
|---|---|---|
| **Visitor** (not logged in) | See the price list, search free workspaces for a date and time, register | FR01, FR02 |
| **Member** | Buy a membership, reserve a workspace in ≤ 4 steps (with alternative offers when the slot is taken), see and cancel own reservations | FR03–FR06, UC03–UC08 |
| **Receptionist** | Manage members (create, edit, deactivate, delete), sell memberships at the desk, record payments, see and cancel any reservation | FR07, FR08, FR05 |
| **Manager** | Everything the receptionist can do, plus rooms and workspaces, membership types and the **occupancy and revenue report** | FR09–FR11 |
| **Administrator** | Employee accounts and roles (create, edit, deactivate, reset password, delete) | FR12 |

Business rules enforced by the system:

| Rule | Where it is checked |
|---|---|
| **BR01** no overlapping reservations of one workspace | domain + service check, and a PostgreSQL `EXCLUDE` constraint, so even two simultaneous requests can't both succeed |
| **BR02** reserving needs a membership active on that date | `Reservation.Create` → `Member.HasActiveMembershipOn` |
| **BR03** a member cancels at the latest 2 h before the start; later only reception | `Reservation.CancelByMember` / `CancelByEmployee` |
| **BR04** working hours (weekdays 07–22, Saturday 09–17, Sunday closed), at least 1 h | `WorkingHours.EnsureWithin` (also in the availability search) |
| **BR05** a membership becomes active only after payment; a renewal starts when the current one ends | `Employee.RecordPayment` → `Membership.Pay` |

Other things worth knowing:
- The UI is in **English and Bosnian** (switch in the header) and works on phones (375 px and up).
- Every create / list / edit / delete required by the assignment exists. Records that other data
  still points to (for example a room with workspaces, or a member with payments) can't be
  deleted. They are **deactivated** instead, and the API answers `409 conflict.in_use`.
- Errors come back as RFC 7807 *ProblemDetails* with a stable `code` and `ruleCode` (for example
  `ruleCode: "BR04"`), which the frontend translates.

---

## Screenshots

| | |
|---|---|
| **Availability search with "Reserve"** (member)<br>![Availability](docs/screenshots/02-availability-reserve.png) | **Confirm the reservation**<br>![Confirm](docs/screenshots/03-reserve-confirm.png) |
| **Slot taken → alternative offers (UC06)**<br>![Alternatives](docs/screenshots/04-alternatives.png) | **My reservations**<br>![My reservations](docs/screenshots/05-my-reservations.png) |
| **My membership**<br>![My membership](docs/screenshots/06-my-membership.png) | **Reception – members**<br>![Members](docs/screenshots/07-reception-members.png) |
| **Reception – record a payment (BR05)**<br>![Record payment](docs/screenshots/08-record-payment.png) | **Reception – reservations**<br>![Reservations](docs/screenshots/09-reception-reservations.png) |
| **Reception – payments**<br>![Payments](docs/screenshots/10-payments.png) | **Manager – rooms**<br>![Rooms](docs/screenshots/11-manager-rooms.png) |
| **Manager – room with its workspaces**<br>![Room detail](docs/screenshots/12-room-detail.png) | **Manager – membership types**<br>![Membership types](docs/screenshots/13-membership-types.png) |
| **Manager – occupancy and revenue report (FR11)**<br>![Report](docs/screenshots/14-report.png) | **Administrator – staff accounts**<br>![Staff](docs/screenshots/15-admin-staff.png) |
| **Bosnian language**<br>![Bosnian](docs/screenshots/16-bs-report.png) | **Phone – availability**<br>![Mobile availability](docs/screenshots/17-mobile-availability.png) |
| **Phone – menu**<br>![Mobile menu](docs/screenshots/18-mobile-menu.png) | |

---

## Running the app

Everything below works the same on **Windows, macOS and Linux**. Every command is one line with
no shell variables, so it can be pasted into PowerShell, cmd, bash or zsh. Run all commands from
the **repository root** unless the step says otherwise.

### 1. Prerequisites

| Tool | Version | Windows (PowerShell) | macOS (Homebrew) | Linux |
|---|---|---|---|---|
| .NET SDK | 10.0.100 or newer 10.x | `winget install Microsoft.DotNet.SDK.10` | `brew install --cask dotnet-sdk` | Arch: `sudo pacman -S dotnet-sdk aspnet-runtime aspnet-targeting-pack` · Ubuntu/Debian: [Microsoft's instructions](https://learn.microsoft.com/dotnet/core/install/linux) |
| Node.js | 26 (see `frontend/.nvmrc`) | `winget install OpenJS.NodeJS` or nvm-windows | `brew install node` or nvm | `nvm install 26` (or the distro package) |
| Docker | Docker Desktop / Docker Engine with Compose | `winget install Docker.DockerDesktop` | `brew install --cask docker` | Arch: `sudo pacman -S docker docker-compose` · Ubuntu: [Docker Engine](https://docs.docker.com/engine/install/) |
| Git | any | `winget install Git.Git` | `brew install git` | `sudo pacman -S git` / `sudo apt install git` |

Docker must be **running** before you start: it hosts the database, and the backend integration
tests use it too (Testcontainers).

### 2. Get the code and start the database

```
git clone https://github.com/MerihM/coworking-app.git
cd coworking-app
cp .env.example .env
docker compose up -d
docker compose ps
```

- On Windows `cmd`, use `copy .env.example .env` instead of `cp` (PowerShell understands `cp`).
- `.env` holds the database name, user and password used by Docker. The defaults are fine
  locally.
- Wait until `docker compose ps` shows the `db` service as **healthy**.

### 3. Configure the API (dotnet user-secrets)

Secrets are kept outside the repository with
[dotnet user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). The values
below are the **demo values**: the password in the connection string must match
`POSTGRES_PASSWORD` in your `.env`.

```
dotnet user-secrets set "ConnectionStrings:Coworking" "Host=localhost;Port=5432;Database=coworking_db;Username=coworking;Password=change-me" --project backend/src/Coworking.Api
dotnet user-secrets set "Jwt:SigningKey" "dev-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz" --project backend/src/Coworking.Api
dotnet user-secrets set "Seed:Admin:Email" "admin@coworking.ba" --project backend/src/Coworking.Api
dotnet user-secrets set "Seed:Admin:Password" "Admin12345" --project backend/src/Coworking.Api
dotnet user-secrets set "Seed:Admin:FirstName" "Admin" --project backend/src/Coworking.Api
dotnet user-secrets set "Seed:Admin:LastName" "Coworking" --project backend/src/Coworking.Api
dotnet user-secrets set "Seed:DemoPassword" "Demo12345" --project backend/src/Coworking.Api
```

`dotnet user-secrets list --project backend/src/Coworking.Api` shows what is set.

### 4. Create the database tables

```
dotnet tool restore
dotnet ef database update --project backend/src/Coworking.Infrastructure --startup-project backend/src/Coworking.Api
```

`dotnet tool restore` installs the pinned `dotnet-ef` (from `dotnet-tools.json`) for this repo only.

### 5. Start the API

```
dotnet run --project backend/src/Coworking.Api
```

- The API listens on **http://localhost:5080** in the Development environment.
- On its first start it creates the administrator and the **demo center**: rooms, workspaces,
  the price list, demo accounts, 4 weeks of past activity, and two upcoming reservations.
  Dates are relative to the day you start it.
- In development: API reference at http://localhost:5080/scalar, OpenAPI document at
  `/openapi/v1.json`, and health check at `/health`.

Leave it running and open a **second terminal** in the repository root.

### 6. Start the frontend

```
cd frontend
npm ci
npm run dev
```

Open **http://localhost:5173**. Vite forwards `/api` to the API on port 5080, so the browser
only ever talks to one address.

### Starting again later

```
docker compose up -d
dotnet run --project backend/src/Coworking.Api
```

Then run `npm run dev` inside `frontend/`. To start from an empty demo database, drop it:
`dotnet ef database drop -f --project backend/src/Coworking.Infrastructure --startup-project backend/src/Coworking.Api`.
Then repeat steps 4 and 5.

---

## Demo accounts

> These are **development demo values** for trying the app. They exist only in a database created
> by the demo seeder or restored from the included dump. Never use them anywhere real.

| Role | E-mail | Password |
|---|---|---|
| Administrator | `admin@coworking.ba` | `Admin12345` |
| Manager | `manager@coworking.ba` | `Demo12345` |
| Receptionist | `receptionist@coworking.ba` | `Demo12345` |
| Member (paid Monthly membership, 2 upcoming reservations) | `member@coworking.ba` | `Demo12345` |
| Members with 4 weeks of history | `tarik.mehic@`, `sara.kovacevic@`, `haris.delic@`, `dino.hasanovic@`, `ajla.mujic@coworking.ba` | `Demo12345` |

The demo center has 6 rooms and 35 workspaces:
- OS-01…24 flexible desks, OS-25…30 fixed desks;
- offices OF-1…3, meeting rooms MR-A and MR-B;
- membership types Daily 15 KM, Weekly 60 KM, Monthly 180 KM, and an inactive Student Monthly.

Things to try:
- **Reserve:** as `member@`, open *Availability* and search the next weekday 10:00–12:00, then
  reserve a desk.
- **Rules:** try a Saturday 16:00–18:00 (BR04), or cancel a reservation that starts within 2 hours
  (BR03).
- **Report:** as `manager@`, open *Report*.

---

## Database dump

`database/coworking_db.sql` is a plain-SQL `pg_dump` of the demo database: schema, constraints
and all data, as of **27 November 2026**. To use it instead of steps 4–5 of the quick start, load
it into the **empty** database that Docker creates:

```
docker compose up -d
docker compose cp database/coworking_db.sql db:/tmp/coworking_db.sql
docker compose exec db psql -U coworking -d coworking_db -f /tmp/coworking_db.sql
```

- Then set the user-secrets (step 3) and start the API (step 5). The API sees the existing data
  and adds nothing.
- The dump's dates are from November 2026, so its "upcoming" reservations will show as
  *Completed* later. The report for **1–30 November 2026** shows the demo numbers.
- For fresh dates, skip the dump and let the API seed an empty database instead.

The dump is copied through the container (instead of `psql < file`) because PowerShell has no `<`
redirection.

---

## Tests

| Part | Command | Count |
|---|---|---|
| Backend: unit and integration tests (xUnit, real PostgreSQL 18 through Testcontainers; **Docker must run**) | `dotnet test backend` | **564** |
| Frontend: component and unit tests (Vitest + Testing Library) | `npm test -- --run` in `frontend/` | **250** |

The design document's test cases are automated as integration tests against the Web API:

| Test case | Test |
|---|---|
| TC01 reservation succeeds (BR01, BR02, BR04 OK) | `Reservations/ReservationsEndpointsTests` |
| TC02 no active membership → refused (BR02) | `Reservations/ReservationsEndpointsTests` (+ domain unit test) |
| TC03 slot taken → 409 + alternative workspace / next slot (BR01, UC06) | `Reservations/ReservationsEndpointsTests` |
| TC04 touching slots 10–12 and 12–14 are allowed | `Reservations/ReservationsEndpointsTests` |
| TC05 Saturday 16–18 → outside working hours (BR04) | `Reservations/ReservationsEndpointsTests` |
| TC06 member can't cancel 1 h before the start, the receptionist can (BR03) | `Reservations/ReservationsEndpointsTests` |
| TC07 payment activates the membership (BR05) | `Memberships/MembershipsEndpointsTests` |
| TC08 a member can't open the manager report (NFR01, FR11) | `Reports/ReportsEndpointsTests` |

Every endpoint also has an **NFR01 access matrix** test: anonymous → 401, wrong role → 403, right
role → 2xx. CI (GitHub Actions) runs the backend and frontend jobs on every pull request, and
`main` accepts changes only through pull requests with both checks green.

---

## Architecture

```mermaid
flowchart LR
    Browser["Browser<br/>React + TypeScript (Mantine, i18n en/bs)"]
    subgraph API["ASP.NET Core Web API (.NET 10)"]
        Controllers["Api<br/>controllers, auth policies, ProblemDetails"]
        Application["Application<br/>use-case services, validation, DTOs"]
        Domain["Domain<br/>entities + business rules BR01–BR05"]
        Infrastructure["Infrastructure<br/>EF Core, JWT, password hashing, seeding"]
    end
    DB[("PostgreSQL 18<br/>coworking_db")]
    Browser -- "HTTP/JSON (/api)" --> Controllers
    Controllers --> Application --> Domain
    Infrastructure --> Application
    Infrastructure -- "Npgsql" --> DB
```

- **Clean layering.** The domain has no dependencies: entities keep their own rules (private
  setters, methods take the current time). Application services implement the use cases, and
  controllers stay thin.
- **Security (NFR01).** Passwords are stored only as PBKDF2 hashes. Each login gets a short JWT
  access token (15 min, kept in memory) and a rotating refresh token in an httpOnly cookie, where
  reuse revokes the whole token family. Policies per role (roles don't inherit), rate limiting on
  login, register and the public search, and ownership checks (another member's id → 404).
- **Time.** Instants are stored in UTC. Business rules run in the center's time zone
  (Europe/Sarajevo), and periods are half-open `[start, end)`.
- **Database.** Snake_case names, enums stored as text, foreign keys with `RESTRICT`, CHECK
  constraints, case-insensitive unique names, and the BR01 `EXCLUDE` constraint (`btree_gist`).
- **Decisions are recorded as ADRs:**
  [0001 architecture and stack](docs/adr/0001-architecture-and-stack.md),
  [0002 time handling](docs/adr/0002-time-handling.md),
  [0003 authentication](docs/adr/0003-authentication.md).

```
coworking-app/
├── backend/
│   ├── src/Coworking.Domain           entities, enums, business rules
│   ├── src/Coworking.Application      services per feature (Auth, Employees, Rooms, Workspaces,
│   │                                  MembershipTypes, Availability, Members, Memberships,
│   │                                  Reservations, Reports), validators, DTOs
│   ├── src/Coworking.Infrastructure   EF Core DbContext, configurations, migrations, seeding
│   ├── src/Coworking.Api              controllers, auth, error handling, Program.cs
│   └── tests/                         Coworking.UnitTests, Coworking.IntegrationTests
├── frontend/src/                      api/ (typed client), features/, pages/, components/, i18n/
├── database/coworking_db.sql          database dump
├── docs/adr/, docs/screenshots/
└── docker-compose.yml, .env.example, global.json, dotnet-tools.json
```

---

## Design document → code

The code uses English names. The design document's Bosnian names map like this:

| Design document | Code / database |
|---|---|
| Clan | `Member` / `members` |
| Clanarina | `Membership` / `memberships` |
| TipClanarine | `MembershipType` / `membership_types` |
| Uplata | `Payment` / `payments` |
| Zaposlenik | `Employee` / `employees` |
| Rezervacija | `Reservation` / `reservations` |
| RadnoMjesto | `Workspace` / `workspaces` |
| Prostorija | `Room` / `rooms` |
| StatusRezervacije: Potvrdjena, Otkazana, Iskoristena | `ReservationStatus`: Confirmed, Cancelled, Completed |
| StatusClanarine: Neplacena, Aktivna, Istekla | `MembershipStatus`: Unpaid, Active, Expired |
| TipRadnogMjesta: Fleksibilno, Fiksno, Kancelarija, Sala | `WorkspaceType`: HotDesk, FixedDesk, Office, MeetingRoom |
| NacinPlacanja: Gotovina, Kartica | `PaymentMethod`: Cash, Card |
| UlogaZaposlenika: Recepcioner, Menadzer, Administrator | `EmployeeRole`: Receptionist, Manager, Administrator |

The report is not a class or a table: as in the design, it is calculated from reservations and
payments on every request.

---

## Deviations from the design document

**Deployment**
- The deployment diagram shows Nginx (HTTPS, reverse proxy) in front of the API. In development
  the API runs on Kestrel over HTTP, and the Vite dev server plays the proxy role. The frontend
  never contains the API address, so Nginx with HTTPS can be put in front without code changes.
  HTTPS (NFR01) is therefore a deployment step, not part of this repository.
- NFR05 daily backups are not scheduled by the app; `pg_dump` (see [Database dump](#database-dump))
  is the tool for them. NFR02 (< 2 s with 50 users) was not load-tested.

**Spaces and offer**
- Rooms can be deactivated too (FR09). The class diagram has the flag only on workspaces. A
  workspace is offered only when it and its room are active.
- A room may have no workspaces for a while.
- Room and membership type names are unique regardless of letter case.
- Capacity per workspace type (not in the design): desks 1, office 1–20, meeting room 2–50.
- Limits on membership types: price 0.01–10 000 KM, duration 1–365 days.
- The availability search is public (visitors, FR02) and never offers fixed desks, because those
  are assigned, not reserved. Times are in 15-minute steps, at most 90 days ahead, and BR04 is
  already checked during the search.

**Memberships and payments**
- *Expired* (Istekla) and *Completed* (Iskorištena) are derived from the dates when read, not
  written by a scheduled job.
- Reception can also **sell** a membership at the desk. The design only has the member buying it
  (UC03); it still stays Unpaid until the payment (BR05).
- Members and payments are handled by the Receptionist **and** the Manager.
- A member has at most one unpaid membership at a time, and unpaid memberships can be deleted.
- The payment amount is always the membership price, because BR05 makes any other amount invalid.
- Extra read-only endpoint `GET /api/members/fixed-desks` for reception.

**Reservations**
- UC04 step 2 ("system shows rooms and workspaces") is the availability search; the member
  reserves from its results.
- UC06 alternatives come from a separate endpoint that the UI calls after a 409. The member
  accepts an offer by picking it.
- Reception does not create reservations, but can cancel any reservation at any time before it
  ends.
- Deactivating a workspace, room or member keeps its future reservations; reception cancels them
  by hand.
- A member may hold overlapping reservations on **different** workspaces; the design doesn't
  forbid it.

**Report (FR11)**
- Occupancy leaves out fixed desks. Available hours are the working hours (BR04) multiplied by
  the counted workspaces.
- The report is printed (or saved as PDF) through the browser; there is no file export.

**Demo data**
- The past demo activity is created by the seeder through the normal domain rules with a "now" in
  the past. The live system refuses reservations in the past.

---

## Development process

The project was built in seven short sprints (23–27 November 2026), tracked on a **Trello** board
(Backlog → To Do → In Progress → Review → Done):

| Sprint | Tickets | Result |
|---|---|---|
| 0 | – | Planning: stack, conventions, ticket templates |
| 1 | CW-1…8 | Repository, PostgreSQL in Docker, .NET solution, CI, logging, domain model, EF Core + migrations, React skeleton |
| 2 | CW-9…17 | Authentication (JWT + refresh tokens), registration, staff accounts (FR12), access matrix (NFR01), UI shell with en/bs |
| 3 | CW-18…25 | Rooms, workspaces, membership types, public offer, availability search (FR02, FR09, FR10), demo data |
| 4 | CW-26…28 | Members, memberships, payments (FR03, FR07, FR08, BR05) |
| 5 | CW-29…31 | Reservations with BR01–BR04, alternatives (UC06), my and reception reservation lists (FR04–FR06) |
| 6 | CW-32…34 | Occupancy and revenue report (FR11, TC08), documentation, database dump |

Every ticket had its own branch (`feature/CW-<n>-…`) and pull request, was squash-merged after CI
passed (backend + frontend jobs), and had acceptance criteria as a Trello checklist.

| Trello board | One ticket |
|---|---|
| ![Trello board](docs/screenshots/19-trello-board.png) | ![Trello card](docs/screenshots/20-trello-card.png) |

The code is commented throughout (XML documentation on every public C# member; the build treats
warnings as errors). AI assistance (Claude) was used during development.
