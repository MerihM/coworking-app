import { Alert, Loader, Table, Title } from "@mantine/core";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import { isRole, roleLabelKeys } from "../features/auth/roles";
import type { CurrentUser } from "../features/auth/sessionStore";

/**
 * "My account": loads GET /api/auth/me (protected). It is also the easiest way to SEE the
 * automatic refresh: open this page more than 15 minutes after logging in, and the expired access
 * token is refreshed and the request repeated without the user noticing.
 */
export function AccountPage() {
  const { t } = useTranslation();
  const [me, setMe] = useState<CurrentUser | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    // Ignore the answer if the user left the page before it arrived.
    let active = true;
    void api.GET("/api/auth/me").then(({ data }) => {
      if (!active) {
        return;
      }
      if (data) {
        setMe(data);
      } else {
        setFailed(true);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  if (failed) {
    return <Alert color="red">{t("account.loadFailed")}</Alert>;
  }

  if (!me) {
    return <Loader />;
  }

  return (
    <>
      <Title order={1} mb="md">
        {t("account.title")}
      </Title>
      <Table variant="vertical" withTableBorder maw={560}>
        <Table.Tbody>
          <Table.Tr>
            <Table.Th w={160}>{t("account.name")}</Table.Th>
            <Table.Td>{me.fullName}</Table.Td>
          </Table.Tr>
          <Table.Tr>
            <Table.Th>{t("account.email")}</Table.Th>
            <Table.Td>{me.email}</Table.Td>
          </Table.Tr>
          <Table.Tr>
            <Table.Th>{t("account.role")}</Table.Th>
            <Table.Td>{isRole(me.role) ? t(roleLabelKeys[me.role]) : me.role}</Table.Td>
          </Table.Tr>
        </Table.Tbody>
      </Table>
    </>
  );
}
