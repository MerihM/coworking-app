import { AppShell, Box, Burger, Button, Container, Group, NavLink, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useTranslation } from "react-i18next";
import { Link, Outlet, useLocation } from "react-router";
import { LanguageSwitch } from "./LanguageSwitch";

/**
 * Navigation entries. `labelKey` is a translation key, so the menu follows the language switch.
 * CW-15 filters this list by role (e.g. "Staff" only for administrators).
 */
const navItems = [{ to: "/", labelKey: "nav.home" }] as const;

/**
 * The frame around every page (NFR03/NFR04: desktop and mobile).
 *
 * - Desktop (≥ 48em, Mantine's "sm" breakpoint): links sit in the header, no side panel.
 * - Mobile: a burger button opens a side panel with the same links; tapping a link closes it.
 *
 * Mantine's AppShell positions header, side panel and main area and handles the breakpoints.
 */
export function AppLayout() {
  const { t } = useTranslation();
  const { pathname } = useLocation();

  // opened = mobile side panel visible. useDisclosure gives the boolean plus toggle/close helpers.
  const [opened, { toggle, close }] = useDisclosure();

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{
        width: 260,
        breakpoint: "sm",
        // On desktop the panel is never shown; on mobile only when the burger is open.
        collapsed: { desktop: true, mobile: !opened },
      }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap="sm" wrap="nowrap">
            <Burger
              opened={opened}
              onClick={toggle}
              hiddenFrom="sm"
              size="sm"
              aria-label={opened ? t("nav.closeMenu") : t("nav.openMenu")}
            />

            <Text component={Link} to="/" fw={700} size="lg" c="inherit" td="none">
              {t("app.name")}
            </Text>

            <Box component="nav" visibleFrom="sm">
              <Group gap={4}>
                {navItems.map((item) => (
                  <Button
                    key={item.to}
                    component={Link}
                    to={item.to}
                    variant={pathname === item.to ? "light" : "subtle"}
                    size="compact-md"
                  >
                    {t(item.labelKey)}
                  </Button>
                ))}
              </Group>
            </Box>
          </Group>

          {/* CW-16 adds the user menu (login / name + logout) next to the switch. */}
          <LanguageSwitch />
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="md">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            component={Link}
            to={item.to}
            label={t(item.labelKey)}
            active={pathname === item.to}
            onClick={close}
          />
        ))}
      </AppShell.Navbar>

      <AppShell.Main>
        <Container size="lg">
          {/* The matched child route (HomePage, NotFoundPage...) renders here. */}
          <Outlet />
        </Container>
      </AppShell.Main>
    </AppShell>
  );
}
