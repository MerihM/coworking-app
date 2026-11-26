import {
  ActionIcon,
  Alert,
  Anchor,
  Badge,
  Box,
  Button,
  Group,
  LoadingOverlay,
  Menu,
  SegmentedControl,
  Select,
  Stack,
  Table,
  Text,
  Title,
  VisuallyHidden,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import type { ApiResult } from "../../api/result";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { StatusBadge } from "../../components/StatusBadge";
import { RoomFormModal } from "../../features/rooms/RoomFormModal";
import {
  activateRoom,
  deactivateRoom,
  deleteRoom,
  getRoom,
  listRooms,
  type Room,
  type RoomListQuery,
} from "../../features/rooms/roomsApi";
import { WorkspaceFormModal } from "../../features/workspaces/WorkspaceFormModal";
import {
  workspaceTypeColors,
  workspaceTypeLabelKeys,
  workspaceTypes,
} from "../../features/workspaces/workspaceTypes";
import {
  activateWorkspace,
  deactivateWorkspace,
  deleteWorkspace,
  listWorkspaces,
  type Workspace,
  type WorkspaceListQuery,
  type WorkspaceType,
} from "../../features/workspaces/workspacesApi";

const PAGE_SIZE = 20;

/** Active rooms for the "move to another room" select. Module-level = a stable query object. */
const ACTIVE_ROOMS_QUERY: RoomListQuery = { IsActive: true, PageSize: 100 };

type StatusFilter = "all" | "active" | "inactive";
type ActionKind = "deactivate" | "activate" | "delete";

/** The confirm dialog that is open: an action on the room itself or on one of its workspaces. */
type PendingAction =
  | { target: "room"; kind: ActionKind; room: Room }
  | { target: "workspace"; kind: ActionKind; workspace: Workspace };

/**
 * One room and its workspaces (FR09, UC11). The room can be edited, deactivated or deleted
 * (only when empty); its workspaces can be added, edited (also moved to another room),
 * deactivated / activated and deleted (only when never reserved).
 */
export function RoomDetailPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const roomId = Number(useParams().roomId);

  // ----- data -----
  const room = useApiQuery(getRoom, roomId);
  const activeRooms = useApiQuery(listRooms, ACTIVE_ROOMS_QUERY);

  const [type, setType] = useState<WorkspaceType | null>(null);
  const [status, setStatus] = useState<StatusFilter>("all");
  const [page, setPage] = useState(1);
  const workspaceQuery = useMemo<WorkspaceListQuery>(
    () => ({
      RoomId: roomId,
      Type: type ?? undefined,
      IsActive: status === "all" ? undefined : status === "active",
      Page: page,
      PageSize: PAGE_SIZE,
    }),
    [roomId, type, status, page],
  );
  const workspaces = useApiQuery(listWorkspaces, workspaceQuery);

  /** After any change: the workspace list AND the room (its counts) may have changed. */
  function reloadAll() {
    room.reload();
    workspaces.reload();
  }

  // ----- dialogs -----
  const [roomFormOpen, setRoomFormOpen] = useState(false);
  const [workspaceFormOpen, setWorkspaceFormOpen] = useState(false);
  const [editingWorkspace, setEditingWorkspace] = useState<Workspace | null>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [running, setRunning] = useState(false);

  function changeFilter(apply: () => void) {
    apply();
    setPage(1);
  }

  function openWorkspaceForm(workspace: Workspace | null) {
    setEditingWorkspace(workspace);
    setWorkspaceFormOpen(true);
  }

  function handleRoomSaved(saved: Room) {
    setRoomFormOpen(false);
    notifications.show({
      color: "green",
      message: t("rooms.notify.updated", { name: saved.name }),
    });
    reloadAll();
  }

  function handleWorkspaceSaved(saved: Workspace, created: boolean) {
    setWorkspaceFormOpen(false);
    const key = created ? "workspaces.notify.created" : "workspaces.notify.updated";
    const moved = !created && saved.roomId !== roomId;
    notifications.show({
      color: "green",
      message: moved
        ? t("workspaces.notify.moved", { code: saved.code, room: saved.roomName })
        : t(key, { code: saved.code }),
    });
    reloadAll();
  }

  async function runPending() {
    if (!pending) {
      return;
    }
    setRunning(true);
    const result = await runAction(pending);
    setRunning(false);
    setPending(null);

    const failedKey =
      pending.target === "room" ? "rooms.notify.failed" : "workspaces.notify.failed";
    if (!result.ok) {
      notifications.show({
        color: "red",
        title: t(failedKey),
        message: problemMessage(result.problem, t),
      });
      return;
    }

    if (pending.target === "room") {
      const done = {
        delete: "rooms.notify.deleted",
        deactivate: "rooms.notify.deactivated",
        activate: "rooms.notify.activated",
      } as const;
      notifications.show({
        color: "green",
        message: t(done[pending.kind], { name: pending.room.name }),
      });
      if (pending.kind === "delete") {
        void navigate("/manage/rooms"); // the room is gone: back to the list
        return;
      }
    } else {
      const done = {
        delete: "workspaces.notify.deleted",
        deactivate: "workspaces.notify.deactivated",
        activate: "workspaces.notify.activated",
      } as const;
      notifications.show({
        color: "green",
        message: t(done[pending.kind], { code: pending.workspace.code }),
      });
    }
    reloadAll();
  }

  // ----- render -----
  const backLink = (
    <Anchor component={Link} to="/manage/rooms" size="sm">
      ← {t("rooms.detail.back")}
    </Anchor>
  );

  if (room.problem) {
    const notFound = room.problem.status === 404 || room.problem.status === 400;
    return (
      <Stack>
        {backLink}
        <Alert color="red" role="alert" title={t("rooms.loadFailed")}>
          {notFound ? t("rooms.detail.notFound") : problemMessage(room.problem, t)}
        </Alert>
      </Stack>
    );
  }

  const current = room.data;
  const confirmTexts = pending && confirmTextsFor(pending, t);

  const typeOptions = workspaceTypes.map((value) => ({
    value,
    label: t(workspaceTypeLabelKeys[value]),
  }));

  const rows = workspaces.data?.items.map((workspace) => (
    <Table.Tr key={workspace.id}>
      <Table.Td>
        <Text size="sm" fw={500} ff="monospace">
          {workspace.code}
        </Text>
      </Table.Td>
      <Table.Td>
        <Group gap="xs" wrap="nowrap">
          <Badge color={workspaceTypeColors[workspace.type]} variant="light">
            {t(workspaceTypeLabelKeys[workspace.type])}
          </Badge>
          {workspace.isAssigned && (
            <Badge color="grape" variant="outline" size="sm">
              {t("workspaces.assigned")}
            </Badge>
          )}
        </Group>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{workspace.capacity}</Text>
      </Table.Td>
      <Table.Td>
        <StatusBadge active={workspace.isActive} />
      </Table.Td>
      <Table.Td>
        <Menu position="bottom-end" withinPortal>
          <Menu.Target>
            <ActionIcon
              variant="subtle"
              color="gray"
              aria-label={t("workspaces.actions.menu", { code: workspace.code })}
            >
              ⋯
            </ActionIcon>
          </Menu.Target>
          <Menu.Dropdown>
            <Menu.Item onClick={() => openWorkspaceForm(workspace)}>
              {t("workspaces.actions.edit")}
            </Menu.Item>
            {workspace.isActive ? (
              <Menu.Item
                disabled={workspace.isAssigned}
                onClick={() => setPending({ target: "workspace", kind: "deactivate", workspace })}
              >
                {t("workspaces.actions.deactivate")}
              </Menu.Item>
            ) : (
              <Menu.Item
                onClick={() => setPending({ target: "workspace", kind: "activate", workspace })}
              >
                {t("workspaces.actions.activate")}
              </Menu.Item>
            )}
            <Menu.Item
              color="red"
              disabled={workspace.isAssigned}
              onClick={() => setPending({ target: "workspace", kind: "delete", workspace })}
            >
              {t("workspaces.actions.delete")}
            </Menu.Item>
            {/* Same rule as the backend (workspaces.fixed_assigned). */}
            {workspace.isAssigned && (
              <Menu.Label>{t("workspaces.actions.assignedHint")}</Menu.Label>
            )}
          </Menu.Dropdown>
        </Menu>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack>
      {backLink}

      <Box pos="relative" mih={60}>
        <LoadingOverlay visible={!current} zIndex={1} overlayProps={{ blur: 1 }} />
        {current && (
          <Stack gap="xs">
            <Group justify="space-between" align="flex-start">
              <Stack gap={4}>
                <Group gap="sm">
                  <Title order={2}>{current.name}</Title>
                  <StatusBadge active={current.isActive} />
                </Group>
                <Text size="sm" c="dimmed">
                  {t("rooms.detail.floor", { floor: current.floor })} ·{" "}
                  {t("rooms.workspaceCount", {
                    active: current.activeWorkspaceCount,
                    total: current.workspaceCount,
                  })}
                </Text>
                {current.description && <Text size="sm">{current.description}</Text>}
              </Stack>
              <Menu position="bottom-end" withinPortal>
                <Menu.Target>
                  <Button variant="default">{t("rooms.detail.roomActions")}</Button>
                </Menu.Target>
                <Menu.Dropdown>
                  <Menu.Item onClick={() => setRoomFormOpen(true)}>
                    {t("rooms.actions.edit")}
                  </Menu.Item>
                  {current.isActive ? (
                    <Menu.Item
                      onClick={() =>
                        setPending({ target: "room", kind: "deactivate", room: current })
                      }
                    >
                      {t("rooms.actions.deactivate")}
                    </Menu.Item>
                  ) : (
                    <Menu.Item
                      onClick={() =>
                        setPending({ target: "room", kind: "activate", room: current })
                      }
                    >
                      {t("rooms.actions.activate")}
                    </Menu.Item>
                  )}
                  <Menu.Item
                    color="red"
                    disabled={current.workspaceCount > 0}
                    onClick={() => setPending({ target: "room", kind: "delete", room: current })}
                  >
                    {t("rooms.actions.delete")}
                  </Menu.Item>
                  {current.workspaceCount > 0 && (
                    <Menu.Label>{t("rooms.actions.deleteHint")}</Menu.Label>
                  )}
                </Menu.Dropdown>
              </Menu>
            </Group>

            {!current.isActive && (
              <Alert color="yellow" title={t("rooms.detail.inactiveTitle")}>
                {t("rooms.detail.inactiveText")}
              </Alert>
            )}
          </Stack>
        )}
      </Box>

      <Group justify="space-between" mt="md">
        <Title order={3}>{t("workspaces.title")}</Title>
        <Button
          onClick={() => openWorkspaceForm(null)}
          // The backend refuses (workspaces.room_inactive); say why before the click.
          disabled={!current?.isActive}
          title={current && !current.isActive ? t("workspaces.addDisabled") : undefined}
        >
          {t("workspaces.add")}
        </Button>
      </Group>

      <Group align="flex-end" gap="sm">
        <Select
          label={t("workspaces.filters.type")}
          placeholder={t("workspaces.filters.allTypes")}
          data={typeOptions}
          value={type}
          onChange={(value) => changeFilter(() => setType(value as WorkspaceType | null))}
          clearable
          w={{ base: "100%", sm: 200 }}
        />
        <SegmentedControl
          aria-label={t("workspaces.filters.status")}
          value={status}
          onChange={(value) => changeFilter(() => setStatus(value as StatusFilter))}
          data={[
            { value: "all", label: t("rooms.filters.all") },
            { value: "active", label: t("common.active") },
            { value: "inactive", label: t("common.inactive") },
          ]}
        />
      </Group>

      {workspaces.problem ? (
        <Alert color="red" title={t("workspaces.loadFailed")} role="alert">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">{problemMessage(workspaces.problem, t)}</Text>
            <Button size="xs" variant="light" color="red" onClick={workspaces.reload}>
              {t("common.retry")}
            </Button>
          </Stack>
        </Alert>
      ) : (
        <Box pos="relative" mih={120}>
          <LoadingOverlay visible={workspaces.loading} zIndex={1} overlayProps={{ blur: 1 }} />
          <Table.ScrollContainer minWidth={560}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("workspaces.columns.code")}</Table.Th>
                  <Table.Th>{t("workspaces.columns.type")}</Table.Th>
                  <Table.Th>{t("workspaces.columns.capacity")}</Table.Th>
                  <Table.Th>{t("workspaces.columns.status")}</Table.Th>
                  <Table.Th w={60}>
                    <VisuallyHidden>{t("workspaces.columns.actions")}</VisuallyHidden>
                  </Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {workspaces.data && workspaces.data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("workspaces.empty")}
            </Text>
          )}
          {workspaces.data && (
            <PaginationBar
              page={workspaces.data.page}
              pageSize={workspaces.data.pageSize}
              totalCount={workspaces.data.totalCount}
              totalPages={Math.ceil(workspaces.data.totalCount / workspaces.data.pageSize)}
              onChange={setPage}
            />
          )}
        </Box>
      )}

      <RoomFormModal
        opened={roomFormOpen}
        room={current ?? null}
        onClose={() => setRoomFormOpen(false)}
        onSaved={handleRoomSaved}
      />
      <WorkspaceFormModal
        opened={workspaceFormOpen}
        workspace={editingWorkspace}
        roomId={roomId}
        rooms={activeRooms.data?.items ?? []}
        onClose={() => setWorkspaceFormOpen(false)}
        onSaved={handleWorkspaceSaved}
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

/** Sends the confirmed action to the API. */
function runAction(action: PendingAction): Promise<ApiResult<void>> {
  if (action.target === "room") {
    const id = action.room.id;
    return action.kind === "delete"
      ? deleteRoom(id)
      : action.kind === "deactivate"
        ? deactivateRoom(id)
        : activateRoom(id);
  }
  const id = action.workspace.id;
  return action.kind === "delete"
    ? deleteWorkspace(id)
    : action.kind === "deactivate"
      ? deactivateWorkspace(id)
      : activateWorkspace(id);
}

/** Title, message and button label of the confirm dialog. */
function confirmTextsFor(action: PendingAction, t: ReturnType<typeof useTranslation>["t"]) {
  if (action.target === "room") {
    return {
      title: t(`rooms.confirm.${action.kind}.title`),
      message: t(`rooms.confirm.${action.kind}.message`, { name: action.room.name }),
      confirmLabel: t(`rooms.confirm.${action.kind}.confirm`),
    };
  }
  return {
    title: t(`workspaces.confirm.${action.kind}.title`),
    message: t(`workspaces.confirm.${action.kind}.message`, { code: action.workspace.code }),
    confirmLabel: t(`workspaces.confirm.${action.kind}.confirm`),
  };
}
