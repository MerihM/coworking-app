import { Avatar, Button, Group, Menu, Text, UnstyledButton } from "@mantine/core";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { isRole, roleLabelKeys } from "../features/auth/roles";
import { useAuth } from "../features/auth/useAuth";

/** "Amra Hodžić" → "AH" for the avatar circle. */
function initials(fullName: string): string {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

/**
 * Right side of the header:
 * - visitor: "Log in" and "Register" buttons
 * - logged in: avatar + name; the dropdown shows the role, "My account" and "Log out"
 * - while the app checks the session on start: nothing (avoids a flicker of the wrong buttons)
 */
export function UserMenu() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();

  if (status === "loading") {
    return null;
  }

  if (status === "anonymous" || !user) {
    return (
      <Group gap="xs" wrap="nowrap">
        <Button size="compact-sm" variant="default" component={Link} to="/login">
          {t("auth.loginLink")}
        </Button>
        <Button size="compact-sm" component={Link} to="/register" visibleFrom="xs">
          {t("auth.registerLink")}
        </Button>
      </Group>
    );
  }

  async function handleLogout() {
    await logout();
    navigate("/");
  }

  return (
    <Menu position="bottom-end" width={220} withinPortal>
      <Menu.Target>
        <UnstyledButton aria-label={t("userMenu.label")}>
          <Group gap="xs" wrap="nowrap">
            <Avatar size="sm" radius="xl" color="teal">
              {initials(user.fullName)}
            </Avatar>
            <Text size="sm" fw={500} visibleFrom="sm">
              {user.fullName}
            </Text>
          </Group>
        </UnstyledButton>
      </Menu.Target>

      <Menu.Dropdown>
        <Menu.Label>
          {user.fullName}
          <br />
          {isRole(user.role) ? t(roleLabelKeys[user.role]) : user.role}
        </Menu.Label>
        <Menu.Item component={Link} to="/account">
          {t("nav.account")}
        </Menu.Item>
        <Menu.Divider />
        <Menu.Item color="red" onClick={() => void handleLogout()}>
          {t("auth.logout")}
        </Menu.Item>
      </Menu.Dropdown>
    </Menu>
  );
}
