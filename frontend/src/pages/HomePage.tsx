import { Group, Stack, Text, Title } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { ApiStatus } from "../features/health/ApiStatus";

/** Start page. For now it shows what the app is and whether the frontend can reach the API. */
export function HomePage() {
  const { t } = useTranslation();

  return (
    <Stack gap="md">
      <Title order={1}>{t("home.title")}</Title>
      <Text c="dimmed">{t("app.tagline")}</Text>
      <Group>
        <ApiStatus />
      </Group>
    </Stack>
  );
}
