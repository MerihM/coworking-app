// /reservations – the member's own reservations (FR06, UC08: "active and previous") and
// cancelling one (FR05, UC07). BR03: a member may cancel at the latest 2 hours before the start;
// later, the button is replaced by a hint to contact the reception (the API refuses it too).
import {
  Alert,
  Box,
  Button,
  Group,
  LoadingOverlay,
  Stack,
  Table,
  Tabs,
  Text,
  Title,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { formatReservationWhen } from "../../features/reservations/reservationDisplay";
import {
  cancelMyReservation,
  listMyReservations,
  type MyReservationsQuery,
  type Reservation,
  type ReservationScope,
} from "../../features/reservations/reservationsApi";
import { ReservationStatusBadge } from "../../features/reservations/ReservationStatusBadge";

const PAGE_SIZE = 20;

const isScope = (value: string | null): value is ReservationScope =>
  value === "Upcoming" || value === "Past";

export function MyReservationsPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;

  const [scope, setScope] = useState<ReservationScope>("Upcoming");
  const [page, setPage] = useState(1);
  const query = useMemo<MyReservationsQuery>(
    () => ({ Scope: scope, Page: page, PageSize: PAGE_SIZE }),
    [scope, page],
  );
  const { data, problem, loading, reload } = useApiQuery(listMyReservations, query);

  const [cancelling, setCancelling] = useState<Reservation | null>(null);
  const [running, setRunning] = useState(false);

  const when = (reservation: Reservation) => formatReservationWhen(reservation, t, language);

  function changeScope(value: string | null) {
    if (isScope(value)) {
      setScope(value);
      setPage(1);
    }
  }

  async function runCancel() {
    if (!cancelling) {
      return;
    }
    setRunning(true);
    const result = await cancelMyReservation(cancelling.id);
    setRunning(false);
    setCancelling(null);

    if (result.ok) {
      notifications.show({ color: "green", message: t("reservations.cancel.done") });
    } else {
      // E.g. BR03 when the dialog stayed open past the 2-hour deadline.
      notifications.show({
        color: "red",
        title: t("reservations.cancel.failed"),
        message: problemMessage(result.problem, t),
      });
    }
    reload();
  }

  function renderAction(reservation: Reservation) {
    if (reservation.status !== "Confirmed") {
      return reservation.status === "Cancelled" && reservation.cancelledByName ? (
        <Text size="xs" c="dimmed">
          {t("reservations.mine.cancelledByReception")}
        </Text>
      ) : null;
    }
    if (!reservation.canMemberCancel) {
      return (
        <Text size="xs" c="dimmed" maw={220}>
          {t("reservations.mine.tooLate")}
        </Text>
      );
    }
    return (
      <Button
        size="xs"
        variant="light"
        color="red"
        onClick={() => setCancelling(reservation)}
        aria-label={t("reservations.cancel.buttonLabel", {
          code: reservation.workspaceCode,
          when: when(reservation),
        })}
      >
        {t("reservations.cancel.button")}
      </Button>
    );
  }

  const rows = data?.items.map((reservation) => (
    <Table.Tr key={reservation.id}>
      <Table.Td>
        <Text size="sm">{when(reservation)}</Text>
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
      </Table.Td>
      <Table.Td>{renderAction(reservation)}</Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack>
      <Group justify="space-between" align="flex-end" gap="sm">
        <div>
          <Title order={2}>{t("reservations.mine.title")}</Title>
          <Text c="dimmed">{t("reservations.mine.intro")}</Text>
        </div>
        <Button component={Link} to="/availability">
          {t("reservations.mine.reserve")}
        </Button>
      </Group>

      <Tabs value={scope} onChange={changeScope}>
        <Tabs.List>
          <Tabs.Tab value="Upcoming">{t("reservations.mine.upcoming")}</Tabs.Tab>
          <Tabs.Tab value="Past">{t("reservations.mine.past")}</Tabs.Tab>
        </Tabs.List>
      </Tabs>

      {problem ? (
        <Alert color="red" title={t("reservations.mine.loadFailed")} role="alert">
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
          <Table.ScrollContainer minWidth={560}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("reservations.columns.when")}</Table.Th>
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
              {scope === "Upcoming"
                ? t("reservations.mine.emptyUpcoming")
                : t("reservations.mine.emptyPast")}
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
            ? t("reservations.cancel.message", {
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
