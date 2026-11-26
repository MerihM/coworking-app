import { Anchor, Stack, Text, Title } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";

/** Shown for any URL that doesn't match a route. */
export function NotFoundPage() {
  const { t } = useTranslation();

  return (
    <Stack gap="sm">
      <Title order={1}>{t("notFound.title")}</Title>
      <Text>{t("notFound.text")}</Text>
      <Anchor component={Link} to="/">
        {t("notFound.back")}
      </Anchor>
    </Stack>
  );
}
