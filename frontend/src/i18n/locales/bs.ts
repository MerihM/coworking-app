// Bosnian texts. The type annotation `typeof en` makes the build FAIL when a key is missing here,
// or when a key exists here but not in en.ts – the two languages can't drift apart.
import type { en } from "./en";

export const bs: typeof en = {
  app: {
    name: "Coworking",
    tagline: "Rezervacije radnih mjesta, članarine i uplate na jednom mjestu.",
  },
  common: {
    loading: "Učitavanje…",
  },
  nav: {
    home: "Početna",
    account: "Moj račun",
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
  userMenu: {
    label: "Meni korisničkog računa",
  },
  auth: {
    loginLink: "Prijava",
    registerLink: "Registracija",
    logout: "Odjava",
    login: {
      title: "Prijava",
      email: "E-mail",
      password: "Lozinka",
      submit: "Prijavi se",
      noAccount: "Nemate račun?",
      registerLink: "Registrujte se",
    },
    register: {
      title: "Kreiranje računa",
      firstName: "Ime",
      lastName: "Prezime",
      email: "E-mail",
      phone: "Telefon",
      phoneHint: "npr. +387 61 123 456",
      password: "Lozinka",
      passwordHint: "Najmanje 8 znakova, sa slovom i cifrom.",
      confirmPassword: "Ponovite lozinku",
      submit: "Registruj se",
      haveAccount: "Već imate račun?",
      loginLink: "Prijavite se",
      success: "Dobro došli! Vaš račun je kreiran.",
    },
  },
  validation: {
    required: "Ovo polje je obavezno.",
    tooLong: "Najviše {{max}} znakova.",
    email: "Unesite ispravnu e-mail adresu.",
    phone: "Dozvoljene su samo cifre, razmaci i + ( ) / -.",
    passwordLength: "Najmanje {{min}} znakova.",
    passwordLetter: "Lozinka mora sadržavati najmanje jedno slovo.",
    passwordDigit: "Lozinka mora sadržavati najmanje jednu cifru.",
    passwordsDiffer: "Lozinke se ne podudaraju.",
  },
  errors: {
    invalidCredentials: "Pogrešan e-mail ili lozinka.",
    emailTaken: "Račun sa ovom e-mail adresom već postoji.",
    tooManyRequests: "Previše pokušaja. Sačekajte minut i pokušajte ponovo.",
    sessionExpired: "Vaša sesija je istekla. Prijavite se ponovo.",
    forbidden: "Nemate dozvolu za ovu radnju.",
    duplicate: "Ova vrijednost je već u upotrebi.",
    inUse:
      "Ovaj zapis još koriste drugi podaci i ne može se obrisati. Umjesto toga ga deaktivirajte.",
    selfChange: "Ovo ne možete uraditi sa vlastitim računom.",
    lastAdmin: "Mora ostati barem jedan aktivni administrator.",
    network: "Server nije dostupan. Provjerite vezu i pokušajte ponovo.",
    server: "Došlo je do greške na serveru. Pokušajte ponovo kasnije.",
    unknown: "Nešto nije u redu. Pokušajte ponovo.",
  },
  account: {
    title: "Moj račun",
    name: "Ime i prezime",
    email: "E-mail",
    role: "Uloga",
    loadFailed: "Učitavanje računa nije uspjelo.",
  },
  roles: {
    member: "Član",
    receptionist: "Recepcioner",
    manager: "Menadžer",
    administrator: "Administrator",
  },
  forbidden: {
    title: "Pristup odbijen",
    text: "Vaša uloga ne dozvoljava otvaranje ove stranice.",
    back: "Nazad na početnu stranicu",
  },
  notFound: {
    title: "Stranica nije pronađena",
    text: "Stranica koju tražite ne postoji.",
    back: "Nazad na početnu stranicu",
  },
};
