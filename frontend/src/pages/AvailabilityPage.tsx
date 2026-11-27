// /availability – public search for free workspaces (FR02). No login needed: visitors can see
// what is free before they register. The search is stored in the URL (see searchParams.ts).
// CW-30: a member reserves straight from the results (UC04: search → Reserve → confirm → done,
// NFR03); a visitor gets a login link that returns to the same search.
import { Alert, Center, Loader, Stack, Text, Title } from "@mantine/core";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { fieldErrors, problemMessage } from "../api/problemMessages";
import type { ApiResult } from "../api/result";
import { useApiQuery } from "../api/useApiQuery";
import {
  searchAvailability,
  type Availability,
  type AvailabilityApiQuery,
} from "../features/availability/availabilityApi";
import { AvailabilityResults } from "../features/availability/AvailabilityResults";
import { AvailabilitySearchForm } from "../features/availability/AvailabilitySearchForm";
import {
  defaultSearch,
  readSearch,
  toApiQuery,
  writeSearch,
  type AvailabilitySearch,
} from "../features/availability/searchParams";
import { centerNow } from "../features/availability/workingHours";
import { Roles } from "../features/auth/roles";
import { useAuth } from "../features/auth/useAuth";
import { ReserveModal } from "../features/reservations/ReserveModal";
import type { ReservationSlot } from "../features/reservations/slot";

/**
 * Module level, so useApiQuery gets a stable loader. No search in the URL yet → no request.
 */
async function loadAvailability(
  query: AvailabilityApiQuery | null,
): Promise<ApiResult<Availability | null>> {
  return query ? searchAvailability(query) : { ok: true, data: null };
}

export function AvailabilityPage() {
  const { t } = useTranslation();
  const { status, user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  // The slot the member clicked "Reserve" on; null = the dialog is closed.
  const [reserving, setReserving] = useState<ReservationSlot | null>(null);

  // Read the clock once, when the page opens (a lazy initial state may do that; render may not).
  const [now] = useState(() => centerNow());

  // useSearchParams returns a new object on every navigation; the string is a stable dependency.
  const paramsKey = searchParams.toString();
  const search = useMemo(() => readSearch(new URLSearchParams(paramsKey)), [paramsKey]);
  const apiQuery = useMemo(() => (search ? toApiQuery(search) : null), [search]);
  const { data, problem, loading, reload } = useApiQuery(loadAvailability, apiQuery);

  function handleSearch(next: AvailabilitySearch) {
    const nextParams = writeSearch(next);
    if (nextParams.toString() === paramsKey) {
      reload(); // the same search again (e.g. a minute later) – the URL doesn't change
    } else {
      setSearchParams(nextParams);
    }
  }

  function renderResults() {
    if (search === null) {
      return <Text c="dimmed">{t("availability.start")}</Text>;
    }
    if (loading) {
      return (
        <Center py="xl">
          <Loader />
        </Center>
      );
    }
    if (problem) {
      return (
        <Alert color="red" title={t("availability.failed")}>
          <Stack gap={4}>
            <Text size="sm">{problemMessage(problem, t)}</Text>
            {Object.values(fieldErrors(problem)).map((message) => (
              <Text key={message} size="sm">
                {message}
              </Text>
            ))}
          </Stack>
        </Alert>
      );
    }
    if (!data) {
      return null;
    }
    // Only members reserve (UC04). Employees just look; visitors are invited to log in and come
    // back to exactly this search (GuestOnly sends them to ?from= after the login).
    const isMember = status === "authenticated" && user?.role === Roles.Member;
    const loginHref =
      status === "anonymous"
        ? `/login?from=${encodeURIComponent(`/availability?${paramsKey}`)}`
        : undefined;
    const currentSearch = search;
    return (
      <AvailabilityResults
        data={data}
        search={currentSearch}
        loginHref={loginHref}
        onReserve={
          isMember
            ? (workspace, room) =>
                setReserving({
                  workspace: { ...workspace, roomName: room.name },
                  date: currentSearch.date,
                  from: currentSearch.from,
                  to: currentSearch.to,
                })
            : undefined
        }
      />
    );
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>{t("availability.title")}</Title>
        <Text c="dimmed">{t("availability.intro")}</Text>
      </div>

      {/* key: a new URL (Back button, a shared link) starts the form again from that URL. */}
      <AvailabilitySearchForm
        key={paramsKey}
        initial={search ?? defaultSearch(now)}
        now={now}
        loading={search !== null && loading}
        onSearch={handleSearch}
      />

      {renderResults()}

      {/* Mounted only while open, so every opening starts on the confirm step. After a successful
          reservation the search reloads in the background: the workspace is no longer free. */}
      {reserving && (
        <ReserveModal slot={reserving} onClose={() => setReserving(null)} onReserved={reload} />
      )}
    </Stack>
  );
}
