// App-wide Mantine theme: colors, radius and fonts used by every component.
// https://mantine.dev/theming/theme-object/
import { createTheme } from "@mantine/core";

const systemFonts = 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';

export const theme = createTheme({
  // Main color for buttons, links, active navigation, focus rings.
  primaryColor: "teal",
  // Mantine 9 default; written out so the choice is visible in one place.
  defaultRadius: "md",
  fontFamily: systemFonts,
  headings: { fontFamily: systemFonts },
});
