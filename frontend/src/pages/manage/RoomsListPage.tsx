import {
  ActionIcon,
  Alert,
  Anchor,
  Box,
  Button,
  Group,
  LoadingOverlay,
  Menu,
  SegmentedControl,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
  VisuallyHidden,
} from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { StatusBadge } from "../../components/StatusBadge";
import { RoomFormModal } from "../../features/rooms/RoomFormModal";
import {
  activateRoom,
  deactivateRoom,
  deleteRoom,
  listRooms,
  type Room,
  type RoomListQuery,
} from "../../features/rooms/roomsApi";

const PAGE_SIZE = 10;

type StatusFilter = "all" | "active" | "inactive";

/** The confirm dialog that is open: which action, for which room. */
type PendingAction = { kind: "deactivate" | "activate" | "delete"; room: Room };

/**
 * Rooms for the manager (FR09, UC11): list with search and status filter, create / edit,
 * deactivate / activate, delete. A room's workspaces are managed on its detail page.
 */
export function RoomsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  // ----- filters and paging -----
  const [search, setSearch] = useState("");
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);
  const [status, setStatus] = useState<StatusFilter>("all");
  const [page, setPage] = useState(1);

  const query = useMemo<RoomListQuery>(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedSearch || undefined,
      IsActive: status === "all" ? undefined : status === "active",
    }),
    [page, debouncedSearch, status],
  );
  const { data, problem, loading, reload } = useApiQuery(listRooms, query);

  // ----- dialogs -----
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Room | null>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [running, setRunning] = useState(false);

  function changeFilter(apply: () => void) {
    apply();
    setPage(1); // a new filter starts at the first page
  }

  function openForm(room: Room | null) {
    setEditing(room);
    setFormOpen(true);
  }

  function handleSaved(room: Room, created: boolean) {
    setFormOpen(false);
    notifications.show({
      color: "green",
      message: t(created ? "rooms.notify.created" : "rooms.notify.updated", { name: room.name }),
    });
    if (created) {
      // A new room has no workspaces yet: go where they are added.
      void navigate(`/manage/rooms/${room.id}`);
    } else {
      reload();
    }
  }

  async function runPending() {
    if (!pending) {
      return;
    }
    const { kind, room } = pending;
    setRunning(true);
    const result =
      kind === "delete"
        ? await deleteRoom(room.id)
        : kind === "deactivate"
          ? await deactivateRoom(room.id)
          : await activateRoom(room.id);
    setRunning(false);
    setPending(null);

    if (!result.ok) {
      notifications.show({
        color: "red",
        title: t("rooms.notify.failed"),
        message: problemMessage(result.problem, t),
      });
      return;
    }

    const messageKey = {
      delete: "rooms.notify.deleted",
      deactivate: "rooms.notify.deactivated",
      activate: "rooms.notify.activated",
    } as const;
    notifications.show({ color: "green", message: t(messageKey[kind], { name: room.name }) });

    // The last row of a page was deleted: go one page back instead of showing an empty page.
    if (kind === "delete" && data && data.items.length === 1 && page > 1) {
      setPage(page - 1);
    } else {
      reload();
    }
  }

  const confirmTexts = pending && {
    title: t(`rooms.confirm.${pending.kind}.title`),
    message: t(`rooms.confirm.${pending.kind}.message`, { name: pending.room.name }),
    confirmLabel: t(`rooms.confirm.${pending.kind}.confirm`),
  };

  const rows = data?.items.map((room) => (
    <Table.Tr key={room.id}>
      <Table.Td>
        <Anchor component={Link} to={`/manage/rooms/${room.id}`} fw={500} size="sm">
          {room.name}
        </Anchor>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{room.floor}</Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">
          {t("rooms.workspaceCount", {
            active: room.activeWorkspaceCount,
            total: room.workspaceCount,
          })}
        </Text>
      </Table.Td>
      <Table.Td>
        <StatusBadge active={room.isActive} />
      </Table.Td>
      <Table.Td>
        <Menu position="bottom-end" withinPortal>
          <Menu.Target>
            <ActionIcon
              variant="subtle"
              color="gray"
              aria-label={t("rooms.actions.menu", { name: room.name })}
            >
              ⋯
            </ActionIcon>
          </Menu.Target>
          <Menu.Dropdown>
            <Menu.Item component={Link} to={`/manage/rooms/${room.id}`}>
              {t("rooms.actions.open")}
            </Menu.Item>
            <Menu.Item onClick={() => openForm(room)}>{t("rooms.actions.edit")}</Menu.Item>
            {room.isActive ? (
              <Menu.Item onClick={() => setPending({ kind: "deactivate", room })}>
                {t("rooms.actions.deactivate")}
              </Menu.Item>
            ) : (
              <Menu.Item onClick={() => setPending({ kind: "activate", room })}>
                {t("rooms.actions.activate")}
              </Menu.Item>
            )}
            {/* The API would answer 409 conflict.in_use; disabling it up front saves a click. */}
            <Menu.Item
              color="red"
              disabled={room.workspaceCount > 0}
              onClick={() => setPending({ kind: "delete", room })}
            >
              {t("rooms.actions.delete")}
            </Menu.Item>
            {room.workspaceCount > 0 && <Menu.Label>{t("rooms.actions.deleteHint")}</Menu.Label>}
          </Menu.Dropdown>
        </Menu>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>{t("rooms.title")}</Title>
        <Button onClick={() => openForm(null)}>{t("rooms.add")}</Button>
      </Group>

      <Group align="flex-end" gap="sm">
        <TextInput
          label={t("rooms.filters.search")}
          placeholder={t("rooms.filters.searchPlaceholder")}
          value={search}
          onChange={(event) => changeFilter(() => setSearch(event.currentTarget.value))}
          w={{ base: "100%", sm: 260 }}
        />
        <SegmentedControl
          aria-label={t("rooms.filters.status")}
          value={status}
          onChange={(value) => changeFilter(() => setStatus(value as StatusFilter))}
          data={[
            { value: "all", label: t("rooms.filters.all") },
            { value: "active", label: t("common.active") },
            { value: "inactive", label: t("common.inactive") },
          ]}
        />
      </Group>

      {problem ? (
        <Alert color="red" title={t("rooms.loadFailed")} role="alert">
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
          <Table.ScrollContainer minWidth={640}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("rooms.columns.name")}</Table.Th>
                  <Table.Th>{t("rooms.columns.floor")}</Table.Th>
                  <Table.Th>{t("rooms.columns.workspaces")}</Table.Th>
                  <Table.Th>{t("rooms.columns.status")}</Table.Th>
                  <Table.Th w={60}>
                    <VisuallyHidden>{t("rooms.columns.actions")}</VisuallyHidden>
                  </Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("rooms.empty")}
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

      <RoomFormModal
        opened={formOpen}
        room={editing}
        onClose={() => setFormOpen(false)}
        onSaved={handleSaved}
      />
      <ConfirmModal
        opened={pending !== null}
        title={confirmTexts?.title ?? ""}
        message={confirmTexts?.message ?? ""}
        confirmLabel={confirmTexts?.confirmLabel ?? ""}
        danger={pending?.kind !== "activate"}
        loading={running}
        onConfirm={() => void runPending()}
        onClose={() => setPending(null)}
      />
    </Stack>
  );
}
