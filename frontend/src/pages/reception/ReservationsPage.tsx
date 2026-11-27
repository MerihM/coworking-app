// /reception/reservations – every reservation, by day (default: today), for the Receptionist and
// the Manager. Reception cancels on behalf of a member at any time before the reservation ends
// (FR05, UC07, BR03 – also after the member's own 2-hour deadline, TC06).
import {
  Alert,
  Anchor,
  Box,
  Button,
  Group,
  LoadingOverlay,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { DatePickerInput } from "@mantine/dates";
import { useDebouncedValue } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { centerNow } from "../../features/availability/workingHours";
import {
  formatReservationWhen,
  reservationStatuses,
  reservationStatusLabelKeys,
} from "../../features/reservations/reservationDisplay";
import {
  cancelReservation,
  listReservations,
  type Reservation,
  type ReservationListQuery,
  type ReservationStatus,
} from "../../features/reservations/reservationsApi";
import { ReservationStatusBadge } from "../../features/reservations/ReservationStatusBadge";

const PAGE_SIZE = 20;

const isStatus = (value: string | null): value is ReservationStatus =>
  reservationStatuses.some((status) => status === value);

export function ReservationsPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;

  // "Who is coming today" is the reception's usual question. Cleared = all dates.
  const [date, setDate] = useState<string | null>(() => centerNow().date);
  const [status, setStatus] = useState<ReservationStatus | null>(null);
  const [search, setSearch] = useState("");
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);
  const [page, setPage] = useState(1);

  const query = useMemo<ReservationListQuery>(
    () => ({
      Date: date ?? undefined,
      Status: status ?? undefined,
      Search: debouncedSearch || undefined,
      Page: page,
      PageSize: PAGE_SIZE,
    }),
    [date, status, debouncedSearch, page],
  );
  const { data, problem, loading, reload } = useApiQuery(listReservations, query);

  const [cancelling, setCancelling] = useState<Reservation | null>(null);
  const [running, setRunning] = useState(false);

  const when = (reservation: Reservation) => formatReservationWhen(reservation, t, language);

  async function runCancel() {
    if (!cancelling) {
      return;
    }
    setRunning(true);
    const result = await cancelReservation(cancelling.id);
    setRunning(false);
    const cancelled = cancelling;
    setCancelling(null);

    if (result.ok) {
      notifications.show({
        color: "green",
        message: t("reservations.reception.cancelled", { name: cancelled.memberName }),
      });
    } else {
      // E.g. not_cancellable: the reservation ended or was cancelled meanwhile.
      notifications.show({
        color: "red",
        title: t("reservations.cancel.failed"),
        message: problemMessage(result.problem, t),
      });
    }
    reload();
  }

  const statusOptions = reservationStatuses.map((value) => ({
    value,
    label: t(reservationStatusLabelKeys[value]),
  }));

  const rows = data?.items.map((reservation) => (
    <Table.Tr key={reservation.id}>
      <Table.Td>
        <Text size="sm">{when(reservation)}</Text>
      </Table.Td>
      <Table.Td>
        <Anchor
          component={Link}
          to={`/reception/members/${reservation.memberId}`}
          size="sm"
          fw={500}
        >
          {reservation.memberName}
        </Anchor>
        <Text size="xs" c="dimmed">
          {reservation.memberEmail}
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm" fw={500}>
          {reservation.workspaceCode}
        </Text>
        <Text size="xs" c="dimmed">
          {reservation.roomName}
        </Text>
      </Table.Td>
      <Table.Td>
        <ReservationStatusBadge status={reservation.status} />
        {reservation.status === "Cancelled" && reservation.cancelledByName && (
          <Text size="xs" c="dimmed" mt={4}>
            {t("reservations.reception.cancelledBy", { name: reservation.cancelledByName })}
          </Text>
        )}
      </Table.Td>
      <Table.Td>
        {reservation.status === "Confirmed" && (
          <Button
            size="xs"
            variant="light"
            color="red"
            onClick={() => setCancelling(reservation)}
            aria-label={t("reservations.reception.cancelFor", {
              name: reservation.memberName,
              code: reservation.workspaceCode,
            })}
          >
            {t("reservations.cancel.button")}
          </Button>
        )}
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack>
      <div>
        <Title order={2}>{t("reservations.reception.title")}</Title>
        <Text c="dimmed">{t("reservations.reception.intro")}</Text>
      </div>

      <Group align="flex-end" gap="sm" wrap="wrap">
        <DatePickerInput
          label={t("reservations.reception.date")}
          placeholder={t("reservations.reception.allDates")}
          value={date}
          onChange={(value) => {
            setDate(value);
            setPage(1);
          }}
          clearable
          valueFormat="ddd, D MMM YYYY"
          w={{ base: "100%", sm: 220 }}
        />
        <Select
          label={t("reservations.reception.status")}
          placeholder={t("reservations.reception.allStatuses")}
          data={statusOptions}
          value={status}
          onChange={(value) => {
            setStatus(isStatus(value) ? value : null);
            setPage(1);
          }}
          clearable
          w={{ base: "100%", sm: 200 }}
        />
        <TextInput
          label={t("reservations.reception.search")}
          placeholder={t("reservations.reception.searchPlaceholder")}
          value={search}
          onChange={(event) => {
            setSearch(event.currentTarget.value);
            setPage(1);
          }}
          w={{ base: "100%", sm: 280 }}
        />
      </Group>

      {problem ? (
        <Alert color="red" title={t("reservations.reception.loadFailed")} role="alert">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">{problemMessage(problem, t)}</Text>
            <Button size="xs" variant="light" color="red" onClick={reload}>
              {t("common.retry")}
            </Button>
          </Stack>
        </Alert>
      ) : (
        <Box pos="relative" mih={120}>
          <LoadingOverlay visible={loading} zIndex={1} overlayProps={{ blur: 1 }} />
          <Table.ScrollContainer minWidth={720}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("reservations.columns.when")}</Table.Th>
                  <Table.Th>{t("reservations.columns.member")}</Table.Th>
                  <Table.Th>{t("reservations.columns.workspace")}</Table.Th>
                  <Table.Th>{t("reservations.columns.status")}</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("reservations.reception.empty")}
            </Text>
          )}
          {data && (
            <PaginationBar
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={Math.ceil(data.totalCount / data.pageSize)}
              onChange={setPage}
            />
          )}
        </Box>
      )}

      <ConfirmModal
        opened={cancelling !== null}
        title={t("reservations.cancel.title")}
        message={
          cancelling
            ? t("reservations.reception.cancelMessage", {
                name: cancelling.memberName,
                code: cancelling.workspaceCode,
                when: when(cancelling),
              })
            : ""
        }
        confirmLabel={t("reservations.cancel.confirm")}
        danger
        loading={running}
        onConfirm={() => void runCancel()}
        onClose={() => setCancelling(null)}
      />
    </Stack>
  );
}
