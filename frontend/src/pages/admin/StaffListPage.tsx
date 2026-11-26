import {
  ActionIcon,
  Alert,
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
  TextInput,
  Title,
  VisuallyHidden,
} from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { problemMessage } from "../../api/problemMessages";
import type { ApiProblem } from "../../api/problem";
import { ConfirmModal } from "../../components/ConfirmModal";
import { PaginationBar } from "../../components/PaginationBar";
import { refreshSession } from "../../features/auth/refresh";
import { roleLabelKeys } from "../../features/auth/roles";
import { useAuth } from "../../features/auth/useAuth";
import { EmployeeFormModal } from "../../features/employees/EmployeeFormModal";
import { ResetPasswordModal } from "../../features/employees/ResetPasswordModal";
import {
  activateEmployee,
  deactivateEmployee,
  deleteEmployee,
  employeeRoles,
  fullName,
  listEmployees,
  type Employee,
  type EmployeeListQuery,
  type EmployeePage,
  type EmployeeRole,
} from "../../features/employees/employeesApi";

const PAGE_SIZE = 10;

type StatusFilter = "all" | "active" | "inactive";

/** The confirm dialog that is open: which action, for whom. */
type PendingAction = { kind: "deactivate" | "activate" | "delete"; employee: Employee };

/** Result of the last finished request. `key` says which query it belongs to. */
type Loaded = { key: string; page?: EmployeePage; problem?: ApiProblem };

/**
 * Staff management for administrators (FR12, UC14): list with search, role and status filters,
 * create / edit, reset password, deactivate / activate and delete.
 */
export function StaffListPage() {
  const { t } = useTranslation();
  const { user } = useAuth();

  // ----- filters and paging -----
  const [search, setSearch] = useState("");
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300); // no request on every keystroke
  const [role, setRole] = useState<EmployeeRole | null>(null);
  const [status, setStatus] = useState<StatusFilter>("all");
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0); // +1 = load the same page again after a change

  const query = useMemo<EmployeeListQuery>(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedSearch || undefined,
      Role: role ?? undefined,
      IsActive: status === "all" ? undefined : status === "active",
    }),
    [page, debouncedSearch, role, status],
  );
  const queryKey = `${JSON.stringify(query)}#${reloadKey}`;

  // ----- loading -----
  // State is only set when the answer arrives (never synchronously in the effect), and an answer
  // for an older query is ignored – fast typing can't show stale rows.
  const [loaded, setLoaded] = useState<Loaded | null>(null);

  useEffect(() => {
    let current = true;
    void listEmployees(query).then((result) => {
      if (current) {
        setLoaded(
          result.ok
            ? { key: queryKey, page: result.data }
            : { key: queryKey, problem: result.problem },
        );
      }
    });
    return () => {
      current = false;
    };
  }, [query, queryKey]);

  const loading = loaded?.key !== queryKey;
  const data = loaded?.page;
  const reload = () => setReloadKey((key) => key + 1);

  // ----- dialogs -----
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Employee | null>(null);
  const [resetting, setResetting] = useState<Employee | null>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [running, setRunning] = useState(false);

  const isSelf = (employee: Employee) => user?.userType === "employee" && user.id === employee.id;

  function changeFilter(apply: () => void) {
    apply();
    setPage(1); // a new filter starts at the first page
  }

  function openCreate() {
    setEditing(null);
    setFormOpen(true);
  }

  function openEdit(employee: Employee) {
    setEditing(employee);
    setFormOpen(true);
  }

  function handleSaved(employee: Employee, created: boolean) {
    setFormOpen(false);
    notifications.show({
      color: "green",
      message: t(created ? "staff.notify.created" : "staff.notify.updated", {
        name: fullName(employee),
      }),
    });
    if (isSelf(employee)) {
      // Name or e-mail of the logged-in admin changed: refresh the header/user menu.
      void refreshSession();
    }
    reload();
  }

  function handleResetDone(employee: Employee) {
    setResetting(null);
    notifications.show({
      color: "green",
      message: t("staff.notify.passwordReset", { name: fullName(employee) }),
    });
  }

  async function runPending() {
    if (!pending) {
      return;
    }
    const { kind, employee } = pending;
    setRunning(true);
    const result =
      kind === "delete"
        ? await deleteEmployee(employee.id)
        : kind === "deactivate"
          ? await deactivateEmployee(employee.id)
          : await activateEmployee(employee.id);
    setRunning(false);
    setPending(null);

    if (!result.ok) {
      notifications.show({
        color: "red",
        title: t("staff.notify.failed"),
        message: problemMessage(result.problem, t),
      });
      return;
    }

    const messageKey =
      kind === "delete"
        ? "staff.notify.deleted"
        : kind === "deactivate"
          ? "staff.notify.deactivated"
          : "staff.notify.activated";
    notifications.show({ color: "green", message: t(messageKey, { name: fullName(employee) }) });

    // The last row of a page was deleted: go one page back instead of showing an empty page.
    if (kind === "delete" && data && data.items.length === 1 && page > 1) {
      setPage(page - 1);
    } else {
      reload();
    }
  }

  const confirmTexts = pending && {
    title: t(`staff.confirm.${pending.kind}.title`),
    message: t(`staff.confirm.${pending.kind}.message`, { name: fullName(pending.employee) }),
    confirmLabel: t(`staff.confirm.${pending.kind}.confirm`),
  };

  const roleOptions = employeeRoles.map((value) => ({ value, label: t(roleLabelKeys[value]) }));
  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0;

  const rows = data?.items.map((employee) => {
    const self = isSelf(employee);
    return (
      <Table.Tr key={employee.id}>
        <Table.Td>
          <Group gap="xs" wrap="nowrap">
            <Text size="sm" fw={500}>
              {fullName(employee)}
            </Text>
            {self && (
              <Badge size="xs" variant="outline">
                {t("staff.you")}
              </Badge>
            )}
          </Group>
        </Table.Td>
        <Table.Td>
          <Text size="sm">{employee.email}</Text>
        </Table.Td>
        <Table.Td>
          <Text size="sm">{t(roleLabelKeys[employee.role])}</Text>
        </Table.Td>
        <Table.Td>
          <Badge color={employee.isActive ? "green" : "gray"} variant="light">
            {employee.isActive ? t("staff.status.active") : t("staff.status.inactive")}
          </Badge>
        </Table.Td>
        <Table.Td>
          <Menu position="bottom-end" withinPortal>
            <Menu.Target>
              <ActionIcon
                variant="subtle"
                color="gray"
                aria-label={t("staff.actions.menu", { name: fullName(employee) })}
              >
                ⋯
              </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Item onClick={() => openEdit(employee)}>{t("staff.actions.edit")}</Menu.Item>
              <Menu.Item onClick={() => setResetting(employee)}>
                {t("staff.actions.resetPassword")}
              </Menu.Item>
              {employee.isActive ? (
                <Menu.Item
                  disabled={self}
                  onClick={() => setPending({ kind: "deactivate", employee })}
                >
                  {t("staff.actions.deactivate")}
                </Menu.Item>
              ) : (
                <Menu.Item onClick={() => setPending({ kind: "activate", employee })}>
                  {t("staff.actions.activate")}
                </Menu.Item>
              )}
              <Menu.Item
                color="red"
                disabled={self}
                onClick={() => setPending({ kind: "delete", employee })}
              >
                {t("staff.actions.delete")}
              </Menu.Item>
              {self && <Menu.Label>{t("staff.actions.ownAccount")}</Menu.Label>}
            </Menu.Dropdown>
          </Menu>
        </Table.Td>
      </Table.Tr>
    );
  });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>{t("staff.title")}</Title>
        <Button onClick={openCreate}>{t("staff.add")}</Button>
      </Group>

      <Group align="flex-end" gap="sm">
        <TextInput
          label={t("staff.filters.search")}
          placeholder={t("staff.filters.searchPlaceholder")}
          value={search}
          onChange={(event) => changeFilter(() => setSearch(event.currentTarget.value))}
          w={{ base: "100%", sm: 260 }}
        />
        <Select
          label={t("staff.filters.role")}
          placeholder={t("staff.filters.allRoles")}
          data={roleOptions}
          value={role}
          onChange={(value) => changeFilter(() => setRole(value as EmployeeRole | null))}
          clearable
          w={{ base: "100%", sm: 200 }}
        />
        <SegmentedControl
          aria-label={t("staff.filters.status")}
          value={status}
          onChange={(value) => changeFilter(() => setStatus(value as StatusFilter))}
          data={[
            { value: "all", label: t("staff.filters.all") },
            { value: "active", label: t("staff.status.active") },
            { value: "inactive", label: t("staff.status.inactive") },
          ]}
        />
      </Group>

      {loaded?.problem && !loading ? (
        <Alert color="red" title={t("staff.loadFailed")} role="alert">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">{problemMessage(loaded.problem, t)}</Text>
            <Button size="xs" variant="light" color="red" onClick={reload}>
              {t("staff.retry")}
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
                  <Table.Th>{t("staff.columns.name")}</Table.Th>
                  <Table.Th>{t("staff.columns.email")}</Table.Th>
                  <Table.Th>{t("staff.columns.role")}</Table.Th>
                  <Table.Th>{t("staff.columns.status")}</Table.Th>
                  <Table.Th w={60}>
                    <VisuallyHidden>{t("staff.columns.actions")}</VisuallyHidden>
                  </Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("staff.empty")}
            </Text>
          )}
          {data && (
            <PaginationBar
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              totalPages={totalPages}
              onChange={setPage}
            />
          )}
        </Box>
      )}

      <EmployeeFormModal
        opened={formOpen}
        employee={editing}
        isSelf={editing !== null && isSelf(editing)}
        onClose={() => setFormOpen(false)}
        onSaved={handleSaved}
      />
      <ResetPasswordModal
        employee={resetting}
        onClose={() => setResetting(null)}
        onDone={handleResetDone}
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
