# Coworking App

[![CI](https://github.com/MerihM/coworking-app/actions/workflows/ci.yml/badge.svg)](https://github.com/MerihM/coworking-app/actions/workflows/ci.yml)

Information system for managing a coworking center: members, memberships,
payments, rooms, workspaces and reservations.

**Stack:** ASP.NET Core (C#, .NET 10) · React + TypeScript · PostgreSQL 18 · Docker

> 🚧 Work in progress – full documentation coming soon.

## Demo data (development)

When the API starts in the Development environment with an empty database, it creates a demo
center modelled on the design document: an open space with 30 desks (24 flexible, 6 fixed),
three private offices, two meeting rooms, and a price list (Daily, Weekly, Monthly and an
inactive Student Monthly). Tables that already contain data are left alone.

Demo accounts (one per role):

| Role          | E-mail                      |
|---------------|-----------------------------|
| Administrator | from `Seed:Admin:Email`     |
| Manager       | `manager@coworking.ba`      |
| Receptionist  | `receptionist@coworking.ba` |
| Member        | `member@coworking.ba`       |

The demo accounts share the password from `Seed:DemoPassword` (dotnet user-secrets); without it
only the accounts are skipped. Final demo credentials will be listed here at release.
