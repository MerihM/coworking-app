import { Anchor, Stack, Text, Title } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";

/** Shown when a logged-in user opens a page their role may not use (RequireRole). */
export function ForbiddenPage() {
  const { t } = useTranslation();

  return (
    <Stack gap="sm">
      <Title order={1}>{t("forbidden.title")}</Title>
      <Text>{t("forbidden.text")}</Text>
      <Anchor component={Link} to="/">
        {t("forbidden.back")}
      </Anchor>
    </Stack>
  );
}
