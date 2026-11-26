import {
  ActionIcon,
  Alert,
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
import { problemMessage } from "../../api/problemMessages";
import type { ApiResult } from "../../api/result";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { StatusBadge } from "../../components/StatusBadge";
import { MembershipTypeFormModal } from "../../features/membershipTypes/MembershipTypeFormModal";
import {
  activateMembershipType,
  deactivateMembershipType,
  deleteMembershipType,
  listMembershipTypes,
  type MembershipType,
  type MembershipTypeListQuery,
} from "../../features/membershipTypes/membershipTypesApi";
import { formatDuration, formatPrice } from "../../lib/format";

const PAGE_SIZE = 10;

type StatusFilter = "all" | "active" | "inactive";
type ActionKind = "deactivate" | "activate" | "delete";

/** The confirm dialog that is open: which action, for which type. */
type PendingAction = { kind: ActionKind; membershipType: MembershipType };

/** Notification text after a successful action. */
const doneKeys = {
  delete: "membershipTypes.notify.deleted",
  deactivate: "membershipTypes.notify.deactivated",
  activate: "membershipTypes.notify.activated",
} as const;

/**
 * The membership price list for the manager (FR10, UC12): list with search and status filter,
 * create / edit, deactivate / activate, delete (only types nobody has bought).
 */
export function MembershipTypesPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;

  // ----- filters and paging -----
  const [search, setSearch] = useState("");
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);
  const [status, setStatus] = useState<StatusFilter>("all");
  const [page, setPage] = useState(1);

  const query = useMemo<MembershipTypeListQuery>(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedSearch || undefined,
      IsActive: status === "all" ? undefined : status === "active",
    }),
    [page, debouncedSearch, status],
  );
  const { data, problem, loading, reload } = useApiQuery(listMembershipTypes, query);

  // ----- dialogs -----
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<MembershipType | null>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [running, setRunning] = useState(false);

  function changeFilter(apply: () => void) {
    apply();
    setPage(1);
  }

  function openForm(membershipType: MembershipType | null) {
    setEditing(membershipType);
    setFormOpen(true);
  }

  function handleSaved(saved: MembershipType, created: boolean) {
    setFormOpen(false);
    notifications.show({
      color: "green",
      message: t(created ? "membershipTypes.notify.created" : "membershipTypes.notify.updated", {
        name: saved.name,
      }),
    });
    reload();
  }

  async function runPending() {
    if (!pending) {
      return;
    }
    const { kind, membershipType } = pending;
    setRunning(true);
    const result = await runAction(kind, membershipType.id);
    setRunning(false);
    setPending(null);

    if (!result.ok) {
      notifications.show({
        color: "red",
        title: t("membershipTypes.notify.failed"),
        message: problemMessage(result.problem, t),
      });
      return;
    }

    notifications.show({
      color: "green",
      message: t(doneKeys[kind], { name: membershipType.name }),
    });
    if (kind === "delete" && data && data.items.length === 1 && page > 1) {
      setPage(page - 1);
    } else {
      reload();
    }
  }

  const confirmTexts = pending && {
    title: t(`membershipTypes.confirm.${pending.kind}.title`),
    message: t(`membershipTypes.confirm.${pending.kind}.message`, {
      name: pending.membershipType.name,
    }),
    confirmLabel: t(`membershipTypes.confirm.${pending.kind}.confirm`),
  };

  const rows = data?.items.map((membershipType) => {
    const sold = membershipType.membershipCount > 0;
    return (
      <Table.Tr key={membershipType.id}>
        <Table.Td>
          <Text size="sm" fw={500}>
            {membershipType.name}
          </Text>
        </Table.Td>
        <Table.Td>
          <Text size="sm">{formatDuration(membershipType.durationDays, t)}</Text>
        </Table.Td>
        <Table.Td>
          {/* tabular numbers line the prices up under each other */}
          <Text size="sm" style={{ fontVariantNumeric: "tabular-nums" }}>
            {formatPrice(membershipType.price, language)}
          </Text>
        </Table.Td>
        <Table.Td>
          <Text size="sm">{membershipType.membershipCount}</Text>
        </Table.Td>
        <Table.Td>
          <StatusBadge active={membershipType.isActive} />
        </Table.Td>
        <Table.Td>
          <Menu position="bottom-end" withinPortal>
            <Menu.Target>
              <ActionIcon
                variant="subtle"
                color="gray"
                aria-label={t("membershipTypes.actions.menu", { name: membershipType.name })}
              >
                ⋯
              </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Item onClick={() => openForm(membershipType)}>
                {t("membershipTypes.actions.edit")}
              </Menu.Item>
              {membershipType.isActive ? (
                <Menu.Item onClick={() => setPending({ kind: "deactivate", membershipType })}>
                  {t("membershipTypes.actions.deactivate")}
                </Menu.Item>
              ) : (
                <Menu.Item onClick={() => setPending({ kind: "activate", membershipType })}>
                  {t("membershipTypes.actions.activate")}
                </Menu.Item>
              )}
              {/* A bought type is history (memberships, payments): the API answers 409. */}
              <Menu.Item
                color="red"
                disabled={sold}
                onClick={() => setPending({ kind: "delete", membershipType })}
              >
                {t("membershipTypes.actions.delete")}
              </Menu.Item>
              {sold && <Menu.Label>{t("membershipTypes.actions.deleteHint")}</Menu.Label>}
            </Menu.Dropdown>
          </Menu>
        </Table.Td>
      </Table.Tr>
    );
  });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>{t("membershipTypes.title")}</Title>
        <Button onClick={() => openForm(null)}>{t("membershipTypes.add")}</Button>
      </Group>

      <Group align="flex-end" gap="sm">
        <TextInput
          label={t("membershipTypes.filters.search")}
          placeholder={t("membershipTypes.filters.searchPlaceholder")}
          value={search}
          onChange={(event) => changeFilter(() => setSearch(event.currentTarget.value))}
          w={{ base: "100%", sm: 260 }}
        />
        <SegmentedControl
          aria-label={t("membershipTypes.filters.status")}
          value={status}
          onChange={(value) => changeFilter(() => setStatus(value as StatusFilter))}
          data={[
            { value: "all", label: t("membershipTypes.filters.all") },
            { value: "active", label: t("common.active") },
            { value: "inactive", label: t("common.inactive") },
          ]}
        />
      </Group>

      {problem ? (
        <Alert color="red" title={t("membershipTypes.loadFailed")} role="alert">
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
                  <Table.Th>{t("membershipTypes.columns.name")}</Table.Th>
                  <Table.Th>{t("membershipTypes.columns.duration")}</Table.Th>
                  <Table.Th>{t("membershipTypes.columns.price")}</Table.Th>
                  <Table.Th>{t("membershipTypes.columns.sold")}</Table.Th>
                  <Table.Th>{t("membershipTypes.columns.status")}</Table.Th>
                  <Table.Th w={60}>
                    <VisuallyHidden>{t("membershipTypes.columns.actions")}</VisuallyHidden>
                  </Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("membershipTypes.empty")}
            </Text>
          )}
          {data && (
            <PaginationBar
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              // totalPages is optional in the generated client (computed on the backend).
              totalPages={Math.ceil(data.totalCount / data.pageSize)}
              onChange={setPage}
            />
          )}
        </Box>
      )}

      <MembershipTypeFormModal
        opened={formOpen}
        membershipType={editing}
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

/** Sends the confirmed action to the API. */
function runAction(kind: ActionKind, id: number): Promise<ApiResult<void>> {
  switch (kind) {
    case "delete":
      return deleteMembershipType(id);
    case "deactivate":
      return deactivateMembershipType(id);
    case "activate":
      return activateMembershipType(id);
  }
}
