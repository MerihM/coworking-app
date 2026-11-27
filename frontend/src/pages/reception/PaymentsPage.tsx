import {
  Alert,
  Anchor,
  Box,
  Button,
  LoadingOverlay,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { PaginationBar } from "../../components/PaginationBar";
import { centerNow } from "../../features/availability/workingHours";
import { RecordPaymentModal } from "../../features/memberships/RecordPaymentModal";
import {
  listMemberships,
  type Membership,
  type MembershipListQuery,
} from "../../features/memberships/membershipsApi";
import { formatDate, formatPrice } from "../../lib/format";

const PAGE_SIZE = 20;

/**
 * "Waiting for payment" at the reception (FR07, UC09): every unpaid membership, oldest order
 * first (the API's order for Status=Unpaid), with a "Record payment" button on each row.
 */
export function PaymentsPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;

  const [search, setSearch] = useState("");
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);
  const [page, setPage] = useState(1);

  const query = useMemo<MembershipListQuery>(
    () => ({
      Status: "Unpaid",
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedSearch || undefined,
    }),
    [page, debouncedSearch],
  );
  const { data, problem, loading, reload } = useApiQuery(listMemberships, query);
  const [paying, setPaying] = useState<Membership | null>(null);

  function handlePaid(paid: Membership) {
    setPaying(null);
    notifications.show({
      color: "green",
      message: t("payments.notify.paid", {
        name: paid.memberName,
        date: paid.lastDay ? formatDate(paid.lastDay, language) : "",
      }),
    });
    // The paid row leaves this list; step back if it was the last one on the page.
    if (data && data.items.length === 1 && page > 1) {
      setPage(page - 1);
    } else {
      reload();
    }
  }

  const rows = data?.items.map((membership) => (
    <Table.Tr key={membership.id}>
      <Table.Td>
        <Anchor
          component={Link}
          to={`/reception/members/${membership.memberId}`}
          size="sm"
          fw={500}
        >
          {membership.memberName}
        </Anchor>
        <Text size="xs" c="dimmed">
          {membership.memberEmail}
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{membership.typeName}</Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm" style={{ fontVariantNumeric: "tabular-nums" }}>
          {formatPrice(membership.price, language)}
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">
          {formatDate(centerNow(new Date(membership.purchasedAt)).date, language)}
        </Text>
      </Table.Td>
      <Table.Td>
        <Button
          size="xs"
          onClick={() => setPaying(membership)}
          aria-label={t("payments.recordFor", { name: membership.memberName })}
        >
          {t("payments.record")}
        </Button>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack>
      <div>
        <Title order={2}>{t("payments.title")}</Title>
        <Text c="dimmed">{t("payments.intro")}</Text>
      </div>

      <TextInput
        label={t("payments.search")}
        placeholder={t("members.filters.searchPlaceholder")}
        value={search}
        onChange={(event) => {
          setSearch(event.currentTarget.value);
          setPage(1);
        }}
        w={{ base: "100%", sm: 300 }}
      />

      {problem ? (
        <Alert color="red" title={t("payments.loadFailed")} role="alert">
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
                  <Table.Th>{t("payments.columns.member")}</Table.Th>
                  <Table.Th>{t("payments.columns.type")}</Table.Th>
                  <Table.Th>{t("payments.columns.amount")}</Table.Th>
                  <Table.Th>{t("payments.columns.ordered")}</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {data && data.totalCount === 0 && (
            <Text c="dimmed" ta="center" py="lg">
              {t("payments.empty")}
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

      {/* The member's current membership isn't known here, so the dialog explains BR05 in general. */}
      <RecordPaymentModal membership={paying} onClose={() => setPaying(null)} onPaid={handlePaid} />
    </Stack>
  );
}
