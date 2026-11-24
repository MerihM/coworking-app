# ADR 0002: Time handling

- **Status:** Accepted
- **Date:** 2026-11-24

## Context
Business rules are defined in the center's local time (BR04: weekdays 07:00–22:00),
Sarajevo uses summer time (UTC+1 / UTC+2), and the design document defines
"expiry date = start date + duration" (TC07) and "a renewal starts on the expiry date" (BR05).

## Decision
1. **Instants** (reservation start/end, payment, creation) are `DateTimeOffset` in UTC,
   stored as PostgreSQL `timestamptz`.
2. **Calendar dates** (membership validity) are `DateOnly`, stored as `date`.
3. **Rules about the clock** are evaluated in `Europe/Sarajevo` time through `CenterTimeZone`.
4. **Periods are half-open.** Reservations: `[StartsAt, EndsAt)`, so 10–12 and 12–14 don't
   overlap (TC04). Memberships: `[StartDate, EndDate)`, where `EndDate = StartDate + DurationDays`
   is the first day the membership is no longer valid and the day a renewal starts.
5. Domain methods receive `now` as a parameter instead of reading the clock.

## Consequences
- No gaps or overlaps between renewed memberships; one rule for all period comparisons,
  matching PostgreSQL's `tstzrange(..., '[)')` used by the BR01 constraint.
- The UI shows the last valid day as `EndDate - 1`.
- The server needs time zone data (tzdata), including in the Docker image.
- Every rule involving time is unit-testable with fixed moments.
