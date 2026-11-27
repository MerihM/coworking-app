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
import { Link } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import type { ApiResult } from "../../api/result";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { StatusBadge } from "../../components/StatusBadge";
import { MemberFormModal } from "../../features/members/MemberFormModal";
import {
  activateMember,
  deactivateMember,
  deleteMember,
  listMembers,
  memberName,
  type Member,
  type MemberListQuery,
} from "../../features/members/membersApi";
import { formatDate } from "../../lib/format";

const PAGE_SIZE = 10;

type StatusFilter = "all" | "active" | "inactive";
type ActionKind = "deactivate" | "activate" | "delete";

/** The confirm dialog that is open: which action, for which member. */
type PendingAction = { kind: ActionKind; member: Member };

/** Notification text after a successful action. */
const doneKeys = {
  delete: "members.notify.deleted",
  deactivate: "members.notify.deactivated",
  activate: "members.notify.activated",
} as const;

/**
 * Members at the reception (FR08, UC10): search by name, e-mail or phone, status filter,
 * register / edit (incl. the fixed desk), deactivate / activate, delete (only members without
 * history). The name opens the member's page with memberships and payments.
 */
export function MembersListPage() {
  const { t, i18n } = useTranslation();

  // ----- filters and paging -----
  const [search, setSearch] = useState("");
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);
  const [status, setStatus] = useState<StatusFilter>("active");
  const [page, setPage] = useState(1);

  const query = useMemo<MemberListQuery>(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedSearch || undefined,
      IsActive: status === "all" ? undefined : status === "active",
    }),
    [page, debouncedSearch, status],
  );
  const { data, problem, loading, reload } = useApiQuery(listMembers, query);

  // ----- dialogs -----
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Member | null>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [running, setRunning] = useState(false);

  function changeFilter(apply: () => void) {
    apply();
    setPage(1);
  }

  function openForm(member: Member | null) {
    setEditing(member);
    setFormOpen(true);
  }

  function handleSaved(saved: Member, created: boolean) {
    setFormOpen(false);
    notifications.show({
      color: "green",
      message: t(created ? "members.notify.created" : "members.notify.updated", {
        name: memberName(saved),
      }),
    });
    reload();
  }

  async function runPending() {
    if (!pending) {
      return;
    }
    const { kind, member } = pending;
    setRunning(true);
    const result = await runAction(kind, member.id);
    setRunning(false);
    setPending(null);

    if (!result.ok) {
      notifications.show({
        color: "red",
        title: t("members.notify.failed"),
        message: problemMessage(result.problem, t),
      });
      return;
    }

    notifications.show({
      color: "green",
      message: t(doneKeys[kind], { name: memberName(member) }),
    });
    if (kind === "delete" && data && data.items.length === 1 && page > 1) {
      setPage(page - 1);
    } else {
      reload();
    }
  }

  const confirmTexts = pending && {
    title: t(`members.confirm.${pending.kind}.title`),
    message: t(`members.confirm.${pending.kind}.message`, { name: memberName(pending.member) }),
    confirmLabel: t(`members.confirm.${pending.kind}.confirm`),
  };

  /** "Valid until …", "Unpaid" or "None" – what reception most often needs to know. */
  function membershipCell(member: Member) {
    return (
      <Group gap={6} wrap="nowrap">
        <Text size="sm" c={member.validThrough ? undefined : "dimmed"}>
          {member.validThrough
            ? t("members.validUntil", {
                date: formatDate(member.validThrough, i18n.resolvedLanguage),
              })
            : t("members.noMembership")}
        </Text>
        {member.hasUnpaidMembership && (
          <Badge color="yellow" variant="light">
            {t("memberships.status.Unpaid")}
          </Badge>
        )}
      </Group>
    );
  }

  const rows = data?.items.map((member) => (
    <Table.Tr key={member.id}>
      <Table.Td>
        <Anchor component={Link} to={`/reception/members/${member.id}`} size="sm" fw={500}>
          {memberName(member)}
        </Anchor>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{member.email}</Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{member.phone}</Text>
      </Table.Td>
      <Table.Td>{membershipCell(member)}</Table.Td>
      <Table.Td>
        <StatusBadge active={member.isActive} />
      </Table.Td>
      <Table.Td>
        <Menu position="bottom-end" withinPortal>
          <Menu.Target>
            <ActionIcon
              variant="subtle"
              color="gray"
              aria-label={t("members.actions.menu", { name: memberName(member) })}
            >
              ⋯
            </ActionIcon>
          </Menu.Target>
          <Menu.Dropdown>
            <Menu.Item component={Link} to={`/reception/members/${member.id}`}>
              {t("members.actions.open")}
            </Menu.Item>
            <Menu.Item onClick={() => openForm(member)}>{t("members.actions.edit")}</Menu.Item>
            {member.isActive ? (
              <Menu.Item onClick={() => setPending({ kind: "deactivate", member })}>
                {t("members.actions.deactivate")}
              </Menu.Item>
            ) : (
              <Menu.Item onClick={() => setPending({ kind: "activate", member })}>
                {t("members.actions.activate")}
              </Menu.Item>
            )}
            {/* A member with memberships or reservations is history: the API answers 409. */}
            <Menu.Item color="red" onClick={() => setPending({ kind: "delete", member })}>
              {t("members.actions.delete")}
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>{t("members.title")}</Title>
        <Button onClick={() => openForm(null)}>{t("members.add")}</Button>
      </Group>

      <Group align="flex-end" gap="sm">
        <TextInput
          label={t("members.filters.search")}
          placeholder={t("members.filters.searchPlaceholder")}
          value={search}
          onChange={(event) => changeFilter(() => setSearch(event.currentTarget.value))}
          w={{ base: "100%", sm: 300 }}
        />
        <SegmentedControl
          aria-label={t("members.filters.status")}
          value={status}
          onChange={(value) => changeFilter(() => setStatus(value as StatusFilter))}
          data={[
            { value: "active", label: t("common.active") },
            { value: "inactive", label: t("common.inactive") },
            { value: "all", label: t("members.filters.all") },
          ]}
        />
      </Group>

      {problem ? (
        <Alert color="red" title={t("members.loadFailed")} role="alert">
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
          <Table.ScrollContainer minWidth={760}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("members.columns.name")}</Table.Th>
                  <Table.Th>{t("members.columns.email")}</Table.Th>
                  <Table.Th>{t("members.columns.phone")}</Table.Th>
                  <Table.Th>{t("members.columns.membership")}</Table.Th>
                  <Table.Th>{t("members.columns.status")}</Table.Th>
                  <Table.Th w={60}>
                    <VisuallyHidden>{t("members.columns.actions")}</VisuallyHidden>
                  </Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("members.empty")}
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

      <MemberFormModal
        opened={formOpen}
        member={editing}
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
      return deleteMember(id);
    case "deactivate":
      return deactivateMember(id);
    case "activate":
      return activateMember(id);
  }
}
