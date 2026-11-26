import { AppShell, Box, Burger, Button, Container, Group, NavLink, Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useTranslation } from "react-i18next";
import { Link, Outlet, useLocation } from "react-router";
import type { Role } from "../features/auth/roles";
import { useAuth } from "../features/auth/useAuth";
import { LanguageSwitch } from "./LanguageSwitch";
import { UserMenu } from "./UserMenu";

type NavItem = {
  to: string;
  labelKey: "nav.home" | "nav.account";
  /** Show only to logged-in users. */
  requiresAuth?: boolean;
  /** Show only to these roles (implies requiresAuth). */
  roles?: readonly Role[];
};

/**
 * Navigation entries. `labelKey` is a translation key, so the menu follows the language switch.
 * Hiding a link is only convenience – the route guards and the API enforce the same rules.
 */
const navItems: readonly NavItem[] = [
  { to: "/", labelKey: "nav.home" },
  { to: "/account", labelKey: "nav.account", requiresAuth: true },
  // CW-17: { to: "/admin/staff", labelKey: "nav.staff", roles: [Roles.Administrator] },
];

/**
 * The frame around every page (NFR03/NFR04: desktop and mobile).
 *
 * - Desktop (≥ 48em, Mantine's "sm" breakpoint): links sit in the header, no side panel.
 * - Mobile: a burger button opens a side panel with the same links; tapping a link closes it.
 */
export function AppLayout() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const { status, user } = useAuth();

  // opened = mobile side panel visible. useDisclosure gives the boolean plus toggle/close helpers.
  const [opened, { toggle, close }] = useDisclosure();

  const visibleItems = navItems.filter((item) => {
    if (item.roles) {
      return user !== null && item.roles.some((role) => role === user.role);
    }
    return !item.requiresAuth || status === "authenticated";
  });

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
                {visibleItems.map((item) => (
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

          <Group gap="sm" wrap="nowrap">
            <LanguageSwitch />

            {/* Log in / Register for visitors, avatar menu for logged-in users (CW-16). */}
            <UserMenu />
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="md">
        {visibleItems.map((item) => (
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
          {/* The matched child route (HomePage, AccountPage...) renders here. */}
          <Outlet />
        </Container>
      </AppShell.Main>
    </AppShell>
  );
}
