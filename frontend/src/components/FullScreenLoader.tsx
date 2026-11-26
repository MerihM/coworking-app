import { Center, Loader } from "@mantine/core";
import { useTranslation } from "react-i18next";

/** Spinner shown while the app checks whether someone is logged in (silent login on start). */
export function FullScreenLoader() {
  const { t } = useTranslation();

  return (
    <Center h="50vh" role="status" aria-label={t("common.loading")}>
      <Loader />
    </Center>
  );
}
