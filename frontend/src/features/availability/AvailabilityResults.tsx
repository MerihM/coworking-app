// The answer of the availability search: free workspaces grouped by room (FR02).
// CW-30: members get a "Reserve" button per workspace (UC04 step 3); visitors get a hint to log in.
import {
  Alert,
  Anchor,
  Badge,
  Button,
  Card,
  Group,
  Paper,
  SimpleGrid,
  Stack,
  Text,
  Title,
} from "@mantine/core";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatLongDate } from "../../lib/format";
import { workspaceTypeColors, workspaceTypeLabelKeys } from "../workspaces/workspaceTypes";
import type { Availability, AvailableRoom, AvailableWorkspace } from "./availabilityApi";
import type { AvailabilitySearch } from "./searchParams";

type Props = {
  data: Availability;
  /** The search these results belong to (for the "Tuesday 1 December, 09:00–12:00" line). */
  search: AvailabilitySearch;
  /** Set for members: shows a "Reserve" button on every workspace. */
  onReserve?: (workspace: AvailableWorkspace, room: AvailableRoom) => void;
  /** Set for visitors: a login link that comes back to this search (shown above the results). */
  loginHref?: string;
};

export function AvailabilityResults({ data, search, onReserve, loginHref }: Props) {
  const { t, i18n } = useTranslation();
  const period = t("availability.period", {
    date: formatLongDate(search.date, i18n.resolvedLanguage),
    from: search.from,
    to: search.to,
  });

  return (
    <Stack gap="md">
      <Group justify="space-between" align="baseline" gap="xs">
        <Text fw={600}>{t("availability.summary", { count: data.totalFree })}</Text>
        <Text size="sm" c="dimmed">
          {period}
        </Text>
      </Group>

      {loginHref && data.rooms.length > 0 && (
        <Text size="sm">
          {t("reservations.reserve.loginHint")}{" "}
          <Anchor component={Link} to={loginHref}>
            {t("reservations.reserve.loginLink")}
          </Anchor>
        </Text>
      )}

      {data.rooms.length === 0 ? (
        <Alert color="gray">{t("availability.empty")}</Alert>
      ) : (
        data.rooms.map((room) => (
          <Card key={room.id} withBorder radius="md" padding="md">
            <Group justify="space-between" align="baseline" gap="xs">
              <Title order={2} size="h4">
                {room.name}
              </Title>
              <Text size="sm" c="dimmed">
                {t("rooms.detail.floor", { floor: room.floor })}
              </Text>
            </Group>
            {room.description && (
              <Text size="sm" c="dimmed" mt={4}>
                {room.description}
              </Text>
            )}

            <SimpleGrid cols={{ base: 1, xs: 2, md: 3 }} mt="sm" spacing="xs">
              {room.workspaces.map((workspace) => (
                <Paper key={workspace.id} withBorder p="xs" radius="sm">
                  <Group justify="space-between" wrap="nowrap" gap="xs">
                    <Text fw={600}>{workspace.code}</Text>
                    <Badge color={workspaceTypeColors[workspace.type]} variant="light">
                      {t(workspaceTypeLabelKeys[workspace.type])}
                    </Badge>
                  </Group>
                  <Group justify="space-between" align="center" gap="xs" mt={4}>
                    <Text size="sm" c="dimmed">
                      {t("availability.capacity", { count: workspace.capacity })}
                    </Text>
                    {onReserve && (
                      <Button
                        size="compact-sm"
                        aria-label={t("reservations.reserve.buttonLabel", { code: workspace.code })}
                        onClick={() => onReserve(workspace, room)}
                      >
                        {t("reservations.reserve.button")}
                      </Button>
                    )}
                  </Group>
                </Paper>
              ))}
            </SimpleGrid>
          </Card>
        ))
      )}
    </Stack>
  );
}
