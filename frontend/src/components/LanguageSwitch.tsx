import { SegmentedControl } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { currentLanguage, supportedLanguages } from "../i18n";

/**
 * EN | BS switch in the header. The choice is saved in localStorage by i18next's language
 * detector, so it survives a page reload.
 */
export function LanguageSwitch() {
  // useTranslation re-renders this component when the language changes.
  const { t, i18n } = useTranslation();

  return (
    <SegmentedControl
      size="xs"
      aria-label={t("language.label")}
      value={currentLanguage()}
      onChange={(language) => void i18n.changeLanguage(language)}
      data={supportedLanguages.map((language) => ({
        value: language,
        label: language.toUpperCase(),
      }))}
    />
  );
}
