// /manage/reports – the occupancy and revenue report for the Manager (FR11, UC13 "Generiši
// izvještaj o zauzetosti i prihodima"). The API calculates everything from the reservations and
// payments when the page asks; this page only picks the period and lays the numbers out.
// "Print" uses the browser's print dialog (also "Save as PDF"); the print CSS below hides the
// navigation and the controls, so the paper shows only the report.
import {
  Alert,
  Box,
  Button,
  Group,
  LoadingOverlay,
  Paper,
  Progress,
  SegmentedControl,
  SimpleGrid,
  Stack,
  Table,
  Text,
  Title,
} from "@mantine/core";
import { DatePickerInput } from "@mantine/dates";
import { useMemo, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { centerNow } from "../../features/availability/workingHours";
import {
  isReportPreset,
  MAX_REPORT_DAYS,
  periodDays,
  presetPeriod,
  reportPresetLabelKeys,
  reportPresets,
  type ReportPeriod,
  type ReportPreset,
} from "../../features/reports/reportPeriod";
import {
  getOccupancyRevenueReport,
  type OccupancyRevenueReport,
  type ReportQuery,
} from "../../features/reports/reportsApi";
import { workspaceTypeLabelKeys } from "../../features/workspaces/workspaceTypes";
import { formatDate, formatNumber, formatPercent, formatPrice } from "../../lib/format";

/** A range in the picker: either day may still be missing while the manager is choosing. */
type PickedRange = [string | null, string | null];

/** Translation key per payment method (texts shared with the reception's payment dialog). */
const paymentMethodLabelKeys = {
  Cash: "payments.methods.Cash",
  Card: "payments.methods.Card",
} as const;

/** How many workspaces the "most used" table shows. */
const TOP_WORKSPACES = 10;

/** Hides the app chrome and the controls when printing (Mantine's static class names). */
const PRINT_CSS = `
@media print {
  .mantine-AppShell-header, .mantine-AppShell-navbar, .report-no-print { display: none !important; }
  .mantine-AppShell-main { padding: 0 !important; }
}
`;

export function ReportPage() {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;
  const today = centerNow().date;

  const [preset, setPreset] = useState<ReportPreset>("thisMonth");
  // The period that is loaded. A custom range replaces it only once BOTH days are picked.
  const [period, setPeriod] = useState<ReportPeriod>(() => presetPeriod("thisMonth", today));
  const [range, setRange] = useState<PickedRange>([period.from, period.to]);

  const query = useMemo<ReportQuery>(() => ({ From: period.from, To: period.to }), [period]);
  const { data, problem, loading, reload } = useApiQuery(getOccupancyRevenueReport, query);

  function choosePreset(value: string) {
    if (!isReportPreset(value)) {
      return;
    }
    setPreset(value);
    if (value === "custom") {
      setRange([period.from, period.to]); // start editing from what is shown now
      return;
    }
    const next = presetPeriod(value, today);
    setPeriod(next);
    setRange([next.from, next.to]);
  }

  function chooseRange(value: PickedRange) {
    setRange(value);
    const [from, to] = value;
    if (from && to) {
      setPeriod({ from, to });
    }
  }

  const periodLabel = `${formatDate(period.from, language)} – ${formatDate(period.to, language)}`;

  return (
    <Stack>
      <style>{PRINT_CSS}</style>

      <Group justify="space-between" align="flex-start" wrap="wrap">
        <div>
          <Title order={2}>{t("reports.title")}</Title>
          <Text c="dimmed">{t("reports.intro")}</Text>
        </div>
        <Button
          className="report-no-print"
          variant="default"
          onClick={() => window.print()}
          disabled={!data}
        >
          {t("reports.print")}
        </Button>
      </Group>

      <Stack gap="xs" className="report-no-print">
        <SegmentedControl
          aria-label={t("reports.period")}
          value={preset}
          onChange={choosePreset}
          data={reportPresets.map((value) => ({ value, label: t(reportPresetLabelKeys[value]) }))}
          fullWidth
          maw={560}
        />
        {preset === "custom" && (
          <DatePickerInput
            type="range"
            label={t("reports.period")}
            placeholder={t("reports.periodPlaceholder")}
            value={range}
            onChange={chooseRange}
            allowSingleDateInRange
            valueFormat="D MMM YYYY"
            w={{ base: "100%", sm: 320 }}
          />
        )}
      </Stack>

      <Text fw={500}>
        {t("reports.periodLabel", { period: periodLabel, count: periodDays(period) })}
      </Text>

      {problem ? (
        <Alert color="red" title={t("reports.loadFailed")} role="alert">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">{problemMessage(problem, t)}</Text>
            {periodDays(period) > MAX_REPORT_DAYS && (
              <Text size="sm">{t("reports.tooLong", { max: MAX_REPORT_DAYS })}</Text>
            )}
            <Button size="xs" variant="light" color="red" onClick={reload}>
              {t("common.retry")}
            </Button>
          </Stack>
        </Alert>
      ) : (
        <Box pos="relative" mih={160}>
          <LoadingOverlay visible={loading} zIndex={1} overlayProps={{ blur: 1 }} />
          {data && <ReportContent report={data} language={language} />}
        </Box>
      )}
    </Stack>
  );
}

/** Everything below the period: stat cards, occupancy tables, revenue tables. */
function ReportContent({
  report,
  language,
}: {
  report: OccupancyRevenueReport;
  language: string | undefined;
}) {
  const { t } = useTranslation();
  const { occupancy, revenue } = report;
  const hours = (value: number) => t("reports.hours", { value: formatNumber(value, language) });
  const percent = (rate: number) => formatPercent(rate, language);
  const price = (value: number) => formatPrice(value, language);

  if (occupancy.reservationCount === 0 && revenue.paymentCount === 0) {
    return (
      <Alert color="gray" title={t("reports.emptyTitle")}>
        {t("reports.empty")}
      </Alert>
    );
  }

  return (
    <Stack gap="lg">
      <SimpleGrid cols={{ base: 1, xs: 2, lg: 3 }}>
        <Stat
          label={t("reports.stats.occupancy")}
          value={percent(occupancy.occupancyRate)}
          hint={t("reports.stats.occupancyHint", {
            reserved: hours(occupancy.reservedHours),
            available: hours(occupancy.availableHours),
          })}
        />
        <Stat
          label={t("reports.stats.reservations")}
          value={String(occupancy.reservationCount)}
          hint={t("reports.stats.cancelled", { count: occupancy.cancelledCount })}
        />
        <Stat
          label={t("reports.stats.revenue")}
          value={price(revenue.total)}
          hint={t("reports.stats.payments", { count: revenue.paymentCount })}
        />
      </SimpleGrid>

      <Section title={t("reports.sections.byType")}>
        <Table.ScrollContainer minWidth={560}>
          <Table striped verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t("reports.columns.type")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.workspaces")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.reserved")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.available")}</Table.Th>
                <Table.Th>{t("reports.columns.occupancy")}</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {occupancy.byType.map((row) => (
                <Table.Tr key={row.type}>
                  <Table.Td>{t(workspaceTypeLabelKeys[row.type])}</Table.Td>
                  <Table.Td ta="right">{row.workspaceCount}</Table.Td>
                  <Table.Td ta="right">{hours(row.reservedHours)}</Table.Td>
                  <Table.Td ta="right">{hours(row.availableHours)}</Table.Td>
                  <Table.Td>
                    <RateBar rate={row.occupancyRate} label={percent(row.occupancyRate)} />
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Section>

      <Section title={t("reports.sections.byRoom")}>
        <Table.ScrollContainer minWidth={560}>
          <Table striped verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t("reports.columns.room")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.workspaces")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.reservations")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.reserved")}</Table.Th>
                <Table.Th>{t("reports.columns.occupancy")}</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {occupancy.byRoom.map((row) => (
                <Table.Tr key={row.roomId}>
                  <Table.Td>{row.roomName}</Table.Td>
                  <Table.Td ta="right">{row.workspaceCount}</Table.Td>
                  <Table.Td ta="right">{row.reservationCount}</Table.Td>
                  <Table.Td ta="right">{hours(row.reservedHours)}</Table.Td>
                  <Table.Td>
                    <RateBar rate={row.occupancyRate} label={percent(row.occupancyRate)} />
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Section>

      <Section
        title={t("reports.sections.topWorkspaces")}
        hint={t("reports.topHint", { count: TOP_WORKSPACES })}
      >
        <Table.ScrollContainer minWidth={560}>
          <Table striped verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t("reports.columns.workspace")}</Table.Th>
                <Table.Th>{t("reports.columns.room")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.reservations")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.reserved")}</Table.Th>
                <Table.Th>{t("reports.columns.occupancy")}</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {occupancy.workspaces.slice(0, TOP_WORKSPACES).map((row) => (
                <Table.Tr key={row.workspaceId}>
                  <Table.Td>
                    <Text size="sm" fw={500}>
                      {row.code}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {t(workspaceTypeLabelKeys[row.type])}
                    </Text>
                  </Table.Td>
                  <Table.Td>{row.roomName}</Table.Td>
                  <Table.Td ta="right">{row.reservationCount}</Table.Td>
                  <Table.Td ta="right">{hours(row.reservedHours)}</Table.Td>
                  <Table.Td>
                    <RateBar rate={row.occupancyRate} label={percent(row.occupancyRate)} />
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Section>

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Section title={t("reports.sections.revenueByType")}>
          {revenue.byMembershipType.length === 0 ? (
            <Text c="dimmed" size="sm">
              {t("reports.noRevenue")}
            </Text>
          ) : (
            <Table striped verticalSpacing="xs">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("reports.columns.membershipType")}</Table.Th>
                  <Table.Th ta="right">{t("reports.columns.payments")}</Table.Th>
                  <Table.Th ta="right">{t("reports.columns.total")}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {revenue.byMembershipType.map((row) => (
                  <Table.Tr key={row.membershipTypeId}>
                    <Table.Td>{row.name}</Table.Td>
                    <Table.Td ta="right">{row.paymentCount}</Table.Td>
                    <Table.Td ta="right">{price(row.total)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Section>

        <Section title={t("reports.sections.revenueByMethod")}>
          <Table striped verticalSpacing="xs">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t("reports.columns.method")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.payments")}</Table.Th>
                <Table.Th ta="right">{t("reports.columns.total")}</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {revenue.byMethod.map((row) => (
                <Table.Tr key={row.method}>
                  <Table.Td>{t(paymentMethodLabelKeys[row.method])}</Table.Td>
                  <Table.Td ta="right">{row.paymentCount}</Table.Td>
                  <Table.Td ta="right">{price(row.total)}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Section>
      </SimpleGrid>

      {revenue.byDay.length > 0 && (
        <Section title={t("reports.sections.revenueByDay")}>
          <Table.ScrollContainer minWidth={360}>
            <Table striped verticalSpacing="xs">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t("reports.columns.day")}</Table.Th>
                  <Table.Th ta="right">{t("reports.columns.payments")}</Table.Th>
                  <Table.Th ta="right">{t("reports.columns.total")}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {revenue.byDay.map((row) => (
                  <Table.Tr key={row.date}>
                    <Table.Td>{formatDate(row.date, language)}</Table.Td>
                    <Table.Td ta="right">{row.paymentCount}</Table.Td>
                    <Table.Td ta="right">{price(row.total)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Section>
      )}
    </Stack>
  );
}

/** One big number with a label and a small explanation. */
function Stat({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <Paper withBorder p="md" radius="md">
      <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
        {label}
      </Text>
      <Text fz={28} fw={700} lh={1.3}>
        {value}
      </Text>
      <Text size="sm" c="dimmed">
        {hint}
      </Text>
    </Paper>
  );
}

/** A titled block of the report. */
function Section({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <Paper withBorder p="md" radius="md">
      <Title order={4}>{title}</Title>
      {hint && (
        <Text size="sm" c="dimmed">
          {hint}
        </Text>
      )}
      <Box mt="sm">{children}</Box>
    </Paper>
  );
}

/** Occupancy as a bar plus the percentage (the bar is decoration, the text carries the value). */
function RateBar({ rate, label }: { rate: number; label: string }) {
  return (
    <Group gap="xs" wrap="nowrap">
      <Progress value={Math.min(rate, 1) * 100} w={120} aria-hidden />
      <Text size="sm">{label}</Text>
    </Group>
  );
}
