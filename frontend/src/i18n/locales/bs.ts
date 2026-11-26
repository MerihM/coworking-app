// Bosnian texts. The type annotation `typeof en` makes the build FAIL when a key is missing here,
// or when a key exists here but not in en.ts – the two languages can't drift apart.
import type { en } from "./en";

export const bs: typeof en = {
  app: {
    name: "Coworking",
    tagline: "Rezervacije radnih mjesta, članarine i uplate na jednom mjestu.",
  },
  nav: {
    home: "Početna",
    openMenu: "Otvori navigaciju",
    closeMenu: "Zatvori navigaciju",
  },
  language: {
    label: "Jezik",
  },
  home: {
    title: "Coworking centar",
  },
  health: {
    label: "API",
    checking: "provjera…",
    healthy: "Ispravan",
    degraded: "Djelimično ispravan",
    unhealthy: "Neispravan (baza podataka nije dostupna)",
    unreachable: "nedostupan (da li je API pokrenut?)",
  },
  notFound: {
    title: "Stranica nije pronađena",
    text: "Stranica koju tražite ne postoji.",
    back: "Nazad na početnu stranicu",
  },
};
