import { Button, Card, Center, Group, Loader, SimpleGrid, Stack, Text, Title } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useApiQuery } from "../api/useApiQuery";
import { Roles } from "../features/auth/roles";
import { useAuth } from "../features/auth/useAuth";
import { ApiStatus } from "../features/health/ApiStatus";
import { getMembershipOffer } from "../features/membershipTypes/membershipTypesApi";
import { formatDuration, formatPrice } from "../lib/format";

/**
 * Start page, public (CW-25): what the center offers, the membership price list (FR10) and the
 * way to the availability search (FR02). Visitors also get a "Create an account" button;
 * members get a "Buy" button on each offer card that leads to "My membership" (CW-27).
 */
export function HomePage() {
  const { t, i18n } = useTranslation();
  const { status, user } = useAuth();
  // Members can buy straight from the offer (CW-27); the purchase itself is on /memberships.
  const isMember = user?.role === Roles.Member;

  // getMembershipOffer takes no query – `undefined` is a stable "query" for useApiQuery.
  const offer = useApiQuery(getMembershipOffer, undefined);

  function renderOffer() {
    if (offer.loading) {
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
    return (
      <SimpleGrid cols={{ base: 1, xs: 2, md: 4 }}>
        {offer.data.map((item) => (
          <Card key={item.id} withBorder radius="md" padding="lg">
            <Text fw={600}>{item.name}</Text>
            <Text fz={28} fw={700} mt="xs">
              {formatPrice(item.price, i18n.resolvedLanguage)}
            </Text>
            <Text size="sm" c="dimmed">
              {formatDuration(item.durationDays, t)}
            </Text>
            {isMember && (
              <Button
                component={Link}
                to="/memberships"
                variant="light"
                mt="md"
                aria-label={t("memberships.buy.buttonFor", { name: item.name })}
              >
                {t("home.buy")}
              </Button>
            )}
          </Card>
        ))}
      </SimpleGrid>
    );
  }

  return (
    <Stack gap="xl">
      <Stack gap="md">
        <Title order={1}>{t("home.title")}</Title>
        <Text c="dimmed">{t("app.tagline")}</Text>
        <Group>
          <Button component={Link} to="/availability">
            {t("home.findWorkspace")}
          </Button>
          {status === "anonymous" && (
            <Button component={Link} to="/register" variant="default">
              {t("home.register")}
            </Button>
          )}
        </Group>
      </Stack>

      <Stack gap="sm">
        <div>
          <Title order={2}>{t("home.offerTitle")}</Title>
          <Text c="dimmed">{t("home.offerIntro")}</Text>
        </div>
        {renderOffer()}
      </Stack>

      <Group>
        <ApiStatus />
      </Group>
    </Stack>
  );
}
