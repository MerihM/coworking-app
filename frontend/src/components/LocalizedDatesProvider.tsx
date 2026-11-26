// Date pickers (@mantine/dates) in the UI language: month and day names, and weeks that start on
// Monday, as is usual in Bosnia and Herzegovina. Mantine formats dates with dayjs, which needs the
// Bosnian locale loaded once (English is built in).
import "dayjs/locale/bs";
import { DatesProvider } from "@mantine/dates";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

export function LocalizedDatesProvider({ children }: { children: ReactNode }) {
  const { i18n } = useTranslation();
  const locale = i18n.resolvedLanguage === "bs" ? "bs" : "en";

  return <DatesProvider settings={{ locale, firstDayOfWeek: 1 }}>{children}</DatesProvider>;
}
