import {
  Alert,
  Anchor,
  Box,
  Button,
  Card,
  Center,
  Group,
  Loader,
  LoadingOverlay,
  SimpleGrid,
  Stack,
  Table,
  Text,
  Title,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { StatusBadge } from "../../components/StatusBadge";
import { centerNow } from "../../features/availability/workingHours";
import { MemberFormModal } from "../../features/members/MemberFormModal";
import { getMember, memberName, type Member } from "../../features/members/membersApi";
import { MembershipStatusBadge } from "../../features/memberships/MembershipStatusBadge";
import { RecordPaymentModal } from "../../features/memberships/RecordPaymentModal";
import { SellMembershipModal } from "../../features/memberships/SellMembershipModal";
import {
  deleteMembership,
  listMemberships,
  type Membership,
  type MembershipListQuery,
} from "../../features/memberships/membershipsApi";
import { formatDate, formatPrice } from "../../lib/format";

/**
 * One member at the reception (FR07, FR08): details, fixed desk, current membership, all
 * memberships with their payments, and the desk actions – sell a membership, record a payment
 * (BR05), delete an unpaid order, edit the member.
 */
export function MemberDetailPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;
  const memberId = Number(useParams().memberId);

  const member = useApiQuery(getMember, memberId);
  const historyQuery = useMemo<MembershipListQuery>(
    () => ({ MemberId: memberId, Page: 1, PageSize: 50 }),
    [memberId],
  );
  const memberships = useApiQuery(listMemberships, historyQuery);

  const [editOpen, setEditOpen] = useState(false);
  const [selling, setSelling] = useState<Member | null>(null);
  const [paying, setPaying] = useState<Membership | null>(null);
  const [deleting, setDeleting] = useState<Membership | null>(null);
  const [running, setRunning] = useState(false);

  const date = (isoDate: string) => formatDate(isoDate, language);
  const price = (value: number) => formatPrice(value, language);

  function reloadAll() {
    member.reload();
    memberships.reload();
  }

  async function confirmDelete() {
    if (!deleting) {
      return;
    }
    setRunning(true);
    const result = await deleteMembership(deleting.id);
    setRunning(false);
    setDeleting(null);

    notifications.show(
      result.ok
        ? { color: "green", message: t("memberships.notify.cancelled") }
        : {
            color: "red",
            title: t("memberships.notify.failed"),
            message: problemMessage(result.problem, t),
          },
    );
    reloadAll();
  }

  if (member.problem) {
    return (
      <Alert color="red" title={t("members.detail.loadFailed")} role="alert">
        <Stack gap="xs" align="flex-start">
          <Text size="sm">{problemMessage(member.problem, t)}</Text>
          <Anchor component={Link} to="/reception/members" size="sm">
            {t("members.detail.back")}
          </Anchor>
        </Stack>
      </Alert>
    );
  }
  if (!member.data) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    );
  }

  const current = member.data;
  const canSell = current.isActive && !current.hasUnpaidMembership;

  const rows = (memberships.data?.items ?? []).map((membership) => (
    <Table.Tr key={membership.id}>
      <Table.Td>
        <Text size="sm" fw={500}>
          {membership.typeName}
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm" style={{ fontVariantNumeric: "tabular-nums" }}>
          {price(membership.price)}
        </Text>
      </Table.Td>
      <Table.Td>
        <MembershipStatusBadge status={membership.status} />
      </Table.Td>
      <Table.Td>
        <Text size="sm">
          {membership.startDate && membership.lastDay
            ? `${date(membership.startDate)} – ${date(membership.lastDay)}`
            : "—"}
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">
          {membership.payment
            ? t("members.detail.paidBy", {
                date: date(centerNow(new Date(membership.payment.paidAt)).date),
                method: t(`payments.methods.${membership.payment.method}`),
                name: membership.payment.recordedByName,
              })
            : "—"}
        </Text>
      </Table.Td>
      <Table.Td>
        {membership.status === "Unpaid" && (
          <Group gap="xs" wrap="nowrap">
            <Button size="xs" onClick={() => setPaying(membership)}>
              {t("payments.record")}
            </Button>
            <Button size="xs" variant="subtle" color="red" onClick={() => setDeleting(membership)}>
              {t("members.detail.deleteOrder")}
            </Button>
          </Group>
        )}
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Stack gap="lg">
      <Anchor component={Link} to="/reception/members" size="sm">
        ← {t("members.detail.back")}
      </Anchor>

      <Group justify="space-between" align="flex-start">
        <Group gap="sm">
          <Title order={2}>{memberName(current)}</Title>
          <StatusBadge active={current.isActive} />
        </Group>
        <Group gap="sm">
          <Button variant="default" onClick={() => setEditOpen(true)}>
            {t("members.actions.edit")}
          </Button>
          <Button disabled={!canSell} onClick={() => setSelling(current)}>
            {t("members.sell.button")}
          </Button>
        </Group>
      </Group>
      {!current.isActive && <Alert color="gray">{t("members.detail.inactiveHint")}</Alert>}
      {current.isActive && current.hasUnpaidMembership && (
        <Alert color="yellow">{t("members.detail.unpaidHint")}</Alert>
      )}

      <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }}>
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed">
            {t("members.form.email")}
          </Text>
          <Text size="sm">{current.email}</Text>
        </Card>
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed">
            {t("members.form.phone")}
          </Text>
          <Text size="sm">{current.phone}</Text>
        </Card>
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed">
            {t("members.form.fixedDesk")}
          </Text>
          <Text size="sm">{current.fixedDesk?.code ?? "—"}</Text>
        </Card>
        <Card withBorder padding="md">
          <Text size="xs" c="dimmed">
            {t("members.columns.membership")}
          </Text>
          <Text size="sm">
            {current.validThrough
              ? t("members.validUntil", { date: date(current.validThrough) })
              : t("members.noMembership")}
          </Text>
        </Card>
      </SimpleGrid>

      <Stack gap="sm">
        <Title order={3}>{t("members.detail.memberships")}</Title>
        {memberships.problem ? (
          <Alert color="red" role="alert">
            {problemMessage(memberships.problem, t)}
          </Alert>
        ) : rows.length === 0 && !memberships.loading ? (
          <Text c="dimmed">{t("members.detail.noMemberships")}</Text>
        ) : (
          <Box pos="relative">
            <LoadingOverlay visible={memberships.loading} zIndex={1} overlayProps={{ blur: 1 }} />
            <Table.ScrollContainer minWidth={760}>
              <Table striped verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>{t("memberships.history.type")}</Table.Th>
                    <Table.Th>{t("memberships.history.price")}</Table.Th>
                    <Table.Th>{t("memberships.history.status")}</Table.Th>
                    <Table.Th>{t("memberships.history.period")}</Table.Th>
                    <Table.Th>{t("members.detail.payment")}</Table.Th>
                    <Table.Th />
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>{rows}</Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Box>
        )}
      </Stack>

      <MemberFormModal
        opened={editOpen}
        member={current}
        onClose={() => setEditOpen(false)}
        onSaved={(saved) => {
          setEditOpen(false);
          notifications.show({
            color: "green",
            message: t("members.notify.updated", { name: memberName(saved) }),
          });
          reloadAll();
        }}
      />
      <SellMembershipModal
        member={selling}
        onClose={() => setSelling(null)}
        onSold={(sold) => {
          setSelling(null);
          notifications.show({
            color: "green",
            message: t("members.notify.sold", { type: sold.typeName, price: price(sold.price) }),
          });
          reloadAll();
          // Usually the member pays right away: open the payment dialog for the new order.
          setPaying(sold);
        }}
      />
      <RecordPaymentModal
        membership={paying}
        validThrough={current.validThrough}
        onClose={() => setPaying(null)}
        onPaid={(paid) => {
          setPaying(null);
          notifications.show({
            color: "green",
            message: t("payments.notify.paid", {
              name: paid.memberName,
              date: paid.lastDay ? date(paid.lastDay) : "",
            }),
          });
          reloadAll();
        }}
      />
      <ConfirmModal
        opened={deleting !== null}
        title={t("memberships.confirm.cancel.title")}
        message={t("memberships.confirm.cancel.message", { type: deleting?.typeName ?? "" })}
        confirmLabel={t("memberships.confirm.cancel.confirm")}
        danger
        loading={running}
        onConfirm={() => void confirmDelete()}
        onClose={() => setDeleting(null)}
      />
    </Stack>
  );
}
