import { Alert, Button, Group, Modal, Select, Stack, Text } from "@mantine/core";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { problemMessage } from "../../api/problemMessages";
import { useApiQuery } from "../../api/useApiQuery";
import { formatDuration, formatPrice } from "../../lib/format";
import { getMembershipOffer } from "../membershipTypes/membershipTypesApi";
import { sellMembership, type Member } from "../members/membersApi";
import type { Membership } from "./membershipsApi";

type SellMembershipModalProps = {
  /** The member buying at the desk; null = closed. */
  member: Member | null;
  onClose: () => void;
  onSold: (membership: Membership) => void;
};

/**
 * Reception sells a membership from the current offer (UC03 at the desk). It is created Unpaid,
 * like an online purchase; the payment is a separate step (UC09), usually right after.
 */
export function SellMembershipModal({ member, onClose, onSold }: SellMembershipModalProps) {
  const { t } = useTranslation();

  return (
    <Modal opened={member !== null} onClose={onClose} title={t("members.sell.title")} centered>
      {member && <SellForm key={member.id} member={member} onCancel={onClose} onSold={onSold} />}
    </Modal>
  );
}

type SellFormProps = {
  member: Member;
  onCancel: () => void;
  onSold: (membership: Membership) => void;
};

function SellForm({ member, onCancel, onSold }: SellFormProps) {
  const { t, i18n } = useTranslation();
  const offer = useApiQuery(getMembershipOffer, undefined);
  const [typeId, setTypeId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const options = (offer.data ?? []).map((type) => ({
    value: String(type.id),
    label: `${type.name} – ${formatPrice(type.price, i18n.resolvedLanguage)} (${formatDuration(type.durationDays, t)})`,
  }));

  // A failed sale, or an offer that couldn't be loaded.
  const alertText = error ?? (offer.problem ? problemMessage(offer.problem, t) : null);

  async function sell() {
    if (typeId === null) {
      return;
    }
    setSaving(true);
    setError(null);
    const result = await sellMembership(member.id, Number(typeId));
    setSaving(false);

    if (result.ok) {
      onSold(result.data);
    } else {
      setError(problemMessage(result.problem, t));
    }
  }

  return (
    <Stack>
      {alertText && (
        <Alert color="red" role="alert">
          {alertText}
        </Alert>
      )}
      <Text size="sm">
        {t("members.sell.for", { name: `${member.firstName} ${member.lastName}` })}
      </Text>
      <Select
        label={t("members.sell.type")}
        placeholder={t("members.sell.typePlaceholder")}
        data={options}
        value={typeId}
        onChange={setTypeId}
        allowDeselect={false}
        disabled={offer.loading}
      />
      <Text size="sm" c="dimmed">
        {t("members.sell.hint")}
      </Text>
      <Group justify="flex-end" mt="sm">
        <Button variant="default" onClick={onCancel} disabled={saving}>
          {t("common.cancel")}
        </Button>
        <Button loading={saving} disabled={typeId === null} onClick={() => void sell()}>
          {t("members.sell.confirm")}
        </Button>
      </Group>
    </Stack>
  );
}
