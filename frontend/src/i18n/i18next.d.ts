// Type-safe translations: tells i18next's TypeScript types what our resources look like.
// Result: t("home.title") autocompletes in Neovim, and t("home.titel") is a COMPILE error,
// not an untranslated key on screen.
// https://www.i18next.com/overview/typescript
import "i18next";
import type { en } from "./locales/en";

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "translation";
    resources: {
      translation: typeof en;
    };
  }
}
