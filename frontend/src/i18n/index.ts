// i18next setup: English (default) and Bosnian (UI language decision, Sprint 2).
//
// Language detection order:
//   1. localStorage "cw-language" – the user's last choice from the EN/BS switch
//   2. the browser's language (navigator.languages), e.g. "bs-BA" → "bs"
//   3. fallback "en"
// The current language is also sent to the API as Accept-Language (see api/client.ts), so
// validation messages from the backend come back in the same language.
import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";
import { bs } from "./locales/bs";
import { en } from "./locales/en";

/** Languages the app offers, in the order shown in the switch. The first one is the default. */
export const supportedLanguages = ["en", "bs"] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

/** Keeps <html lang="..."> in sync: screen readers and the browser's spell checker use it. */
function setDocumentLanguage(language: string) {
  document.documentElement.lang = language;
}

i18n.on("languageChanged", setDocumentLanguage);

/**
 * Resolves when the translations are ready. main.tsx awaits it before the first render, so
 * no component ever shows raw keys like "home.title".
 */
export const i18nReady = i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    // All texts are bundled into the app (they are small), so no loading over the network.
    resources: {
      en: { translation: en },
      bs: { translation: bs },
    },
    supportedLngs: [...supportedLanguages],
    fallbackLng: supportedLanguages[0],
    // "bs-BA" and "bs-Latn-BA" both become "bs".
    load: "languageOnly",
    detection: {
      order: ["localStorage", "navigator"],
      lookupLocalStorage: "cw-language",
      caches: ["localStorage"],
    },
    // React already escapes everything it renders, so i18next must not do it a second time
    // (it would show "&amp;" instead of "&").
    interpolation: { escapeValue: false },
    // We await i18nReady before rendering, so components never need to "suspend" while waiting.
    react: { useSuspense: false },
  })
  .then(() => setDocumentLanguage(i18n.resolvedLanguage ?? supportedLanguages[0]));

/** The language to send to the API: always one of the supported ones. */
export function currentLanguage(): SupportedLanguage {
  const language = i18n.resolvedLanguage;
  return supportedLanguages.find((supported) => supported === language) ?? supportedLanguages[0];
}

export default i18n;
