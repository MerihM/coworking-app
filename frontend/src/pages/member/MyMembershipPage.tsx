import {
  Alert,
  Box,
  Button,
  Card,
  Center,
  Loader,
  LoadingOverlay,
  SimpleGrid,
  Stack,
  Table,
  Text,
  Title,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { ConfirmModal } from "../../components/ConfirmModal";
import { centerNow } from "../../features/availability/workingHours";
import {
  getMembershipOffer,
  type MembershipTypeOfferItem,
} from "../../features/membershipTypes/membershipTypesApi";
import { MembershipStatusBadge } from "../../features/memberships/MembershipStatusBadge";
import {
  buyMembership,
  cancelMyMembership,
  listMyMemberships,
  type Membership,
} from "../../features/memberships/membershipsApi";
import { summarizeMemberships } from "../../features/memberships/membershipStatus";
import { formatDate, formatDuration, formatPrice } from "../../lib/format";

/** The confirm dialog that is open: buy a type, or cancel the unpaid order. */
type PendingAction =
  { kind: "buy"; type: MembershipTypeOfferItem } | { kind: "cancel"; membership: Membership };

/**
 * "My membership" for members (FR03, UC03): where I stand today, buying from the offer (the
 * order stays Unpaid until it is paid at the reception, BR05), cancelling an unpaid order, and
 * the history of all my memberships.
 */
export function MyMembershipPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;

  // Both loaders take no query; `undefined` is a stable query for useApiQuery.
  const mine = useApiQuery(listMyMemberships, undefined);
  const offer = useApiQuery(getMembershipOffer, undefined);

  const [pending, setPending] = useState<PendingAction | null>(null);
  const [running, setRunning] = useState(false);

  // Sarajevo date, whatever the visitor's computer time zone is (same as the availability page).
  const today = centerNow().date;
  const summary = summarizeMemberships(mine.data ?? [], today);
  const date = (isoDate: string) => formatDate(isoDate, language);
  const price = (value: number) => formatPrice(value, language);

  async function runPending() {
    if (!pending) {
      return;
    }
    setRunning(true);
    const result =
      pending.kind === "buy"
        ? await buyMembership(pending.type.id)
        : await cancelMyMembership(pending.membership.id);
    setRunning(false);
    setPending(null);

    if (!result.ok) {
      notifications.show({
        color: "red",
        title: t("memberships.notify.failed"),
        message: problemMessage(result.problem, t),
      });
      // E.g. memberships.unpaid_exists from another tab: show the current state.
      mine.reload();
      return;
    }

    notifications.show({
      color: "green",
      message:
        pending.kind === "buy"
          ? t("memberships.notify.bought", { price: price(pending.type.price) })
          : t("memberships.notify.cancelled"),
    });
    mine.reload();
  }

  function renderStatus() {
    if (mine.loading && !mine.data) {
      return (
        <Center py="md">
          <Loader size="sm" />
        </Center>
      );
    }
    if (mine.problem) {
      return (
        <Alert color="red" title={t("memberships.loadFailed")} role="alert">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">{problemMessage(mine.problem, t)}</Text>
            <Button size="xs" variant="light" color="red" onClick={mine.reload}>
              {t("common.retry")}
            </Button>
          </Stack>
        </Alert>
      );
    }

    const { current, validThrough, upcoming, unpaid } = summary;
    return (
      <Stack gap="sm">
        {current ? (
          <Alert color="green" title={t("memberships.current.activeTitle")}>
            <Text size="sm">
              {t("memberships.current.active", {
                type: current.typeName,
                date: date(current.lastDay ?? today),
              })}
            </Text>
            {upcoming && validThrough && (
              <Text size="sm" mt={4}>
                {t("memberships.current.renewed", {
                  start: date(upcoming.startDate ?? today),
                  date: date(validThrough),
                })}
              </Text>
            )}
          </Alert>
        ) : upcoming && validThrough ? (
          // Paid, but starting later (bought as a renewal of a membership that has just ended).
          <Alert color="blue" title={t("memberships.current.upcomingTitle")}>
            <Text size="sm">
              {t("memberships.current.upcoming", {
                type: upcoming.typeName,
                start: date(upcoming.startDate ?? today),
                date: date(validThrough),
              })}
            </Text>
          </Alert>
        ) : (
          <Alert color="gray" title={t("memberships.current.noneTitle")}>
            <Text size="sm">{t("memberships.current.none")}</Text>
          </Alert>
        )}

        {unpaid && (
          <Alert color="yellow" title={t("memberships.unpaid.title")}>
            <Stack gap="xs" align="flex-start">
              <Text size="sm">
                {t("memberships.unpaid.message", {
                  type: unpaid.typeName,
                  price: price(unpaid.price),
                })}
              </Text>
              <Button
                size="xs"
                variant="light"
                color="yellow"
                onClick={() => setPending({ kind: "cancel", membership: unpaid })}
              >
                {t("memberships.unpaid.cancel")}
              </Button>
            </Stack>
          </Alert>
        )}
      </Stack>
    );
  }

  function renderOffer() {
    if (offer.loading && !offer.data) {
      return (
        <Center py="md">
          <Loader size="sm" />
        </Center>
      );
    }
    if (offer.problem || !offer.data) {
      return <Text c="dimmed">{t("home.offerFailed")}</Text>;
    }
    if (offer.data.length === 0) {
      return <Text c="dimmed">{t("home.offerEmpty")}</Text>;
    }

    // One order at a time (API: memberships.unpaid_exists) – say why the buttons are off.
    const blocked = summary.unpaid !== undefined;
    return (
      <Stack gap="xs">
        {blocked && (
          <Text size="sm" c="dimmed">
            {t("memberships.buy.blockedHint")}
          </Text>
        )}
        {!blocked && summary.validThrough !== undefined && (
          <Text size="sm" c="dimmed">
            {t("memberships.buy.renewalHint")}
          </Text>
        )}
        <SimpleGrid cols={{ base: 1, xs: 2, md: 4 }}>
          {offer.data.map((type) => (
            <Card key={type.id} withBorder radius="md" padding="lg">
              <Text fw={600}>{type.name}</Text>
              <Text fz={24} fw={700} mt="xs">
                {price(type.price)}
              </Text>
              <Text size="sm" c="dimmed">
                {formatDuration(type.durationDays, t)}
              </Text>
              <Button
                mt="md"
                disabled={blocked || mine.loading}
                onClick={() => setPending({ kind: "buy", type })}
                aria-label={t("memberships.buy.buttonFor", { name: type.name })}
              >
                {t("memberships.buy.button")}
              </Button>
            </Card>
          ))}
        </SimpleGrid>
      </Stack>
    );
  }

  const history = mine.data ?? [];
  const rows = history.map((membership) => (
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
          {/* paidAt is an instant: convert it to the Sarajevo date before formatting */}
          {membership.payment ? date(centerNow(new Date(membership.payment.paidAt)).date) : "—"}
        </Text>
      </Table.Td>
    </Table.Tr>
  ));

  const confirmTexts =
    pending &&
    (pending.kind === "buy"
      ? {
          title: t("memberships.confirm.buy.title", { name: pending.type.name }),
          message: t(
            summary.current || summary.upcoming
              ? "memberships.confirm.buy.messageRenewal"
              : "memberships.confirm.buy.message",
            {
              duration: formatDuration(pending.type.durationDays, t),
              price: price(pending.type.price),
            },
          ),
          confirmLabel: t("memberships.confirm.buy.confirm"),
          danger: false,
        }
      : {
          title: t("memberships.confirm.cancel.title"),
          message: t("memberships.confirm.cancel.message", { type: pending.membership.typeName }),
          confirmLabel: t("memberships.confirm.cancel.confirm"),
          danger: true,
        });

  return (
    <Stack gap="xl">
      <Stack gap="sm">
        <Title order={2}>{t("memberships.title")}</Title>
        {renderStatus()}
      </Stack>

      <Stack gap="sm">
        <Title order={3}>{t("memberships.buy.title")}</Title>
        {renderOffer()}
      </Stack>

      <Stack gap="sm">
        <Title order={3}>{t("memberships.history.title")}</Title>
        {history.length === 0 && !mine.loading ? (
          <Text c="dimmed">{t("memberships.history.empty")}</Text>
        ) : (
          <Box pos="relative">
            <LoadingOverlay visible={mine.loading} zIndex={1} overlayProps={{ blur: 1 }} />
            <Table.ScrollContainer minWidth={560}>
              <Table striped verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>{t("memberships.history.type")}</Table.Th>
                    <Table.Th>{t("memberships.history.price")}</Table.Th>
                    <Table.Th>{t("memberships.history.status")}</Table.Th>
                    <Table.Th>{t("memberships.history.period")}</Table.Th>
                    <Table.Th>{t("memberships.history.paidOn")}</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>{rows}</Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Box>
        )}
      </Stack>

      <ConfirmModal
        opened={pending !== null}
        title={confirmTexts?.title ?? ""}
        message={confirmTexts?.message ?? ""}
        confirmLabel={confirmTexts?.confirmLabel ?? ""}
        danger={confirmTexts?.danger ?? false}
        loading={running}
        onConfirm={() => void runPending()}
        onClose={() => setPending(null)}
      />
    </Stack>
  );
}
