import { Alert, Button, Group, Modal, SegmentedControl, Stack, Text } from "@mantine/core";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { problemMessage } from "../../api/problemMessages";
import { formatDate, formatPrice } from "../../lib/format";
import { centerNow } from "../availability/workingHours";
import {
  paymentMethods,
  recordPayment,
  type Membership,
  type PaymentMethod,
} from "./membershipsApi";
import { paymentStartDate } from "./membershipStatus";

type RecordPaymentModalProps = {
  /** The unpaid membership to pay; null = closed. */
  membership: Membership | null;
  /**
   * The member's last valid day (MemberResponse.validThrough), when known: the dialog then
   * shows the exact start date (BR05). Unknown (payments list) → a general sentence instead.
   */
  validThrough?: string | null;
  onClose: () => void;
  onPaid: (membership: Membership) => void;
};

/**
 * Records a payment at the reception (FR07, UC09). The amount is NOT editable: it must equal the
 * membership's price (BR05), so the dialog shows it and only asks for the method.
 */
export function RecordPaymentModal({
  membership,
  validThrough,
  onClose,
  onPaid,
}: RecordPaymentModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={membership !== null}
      onClose={onClose}
      title={t("payments.dialog.title")}
      centered
    >
      {membership && (
        <PaymentForm
          key={membership.id}
          membership={membership}
          validThrough={validThrough}
          onCancel={onClose}
          onPaid={onPaid}
        />
      )}
    </Modal>
  );
}

type PaymentFormProps = {
  membership: Membership;
  validThrough: string | null | undefined;
  onCancel: () => void;
  onPaid: (membership: Membership) => void;
};

function PaymentForm({ membership, validThrough, onCancel, onPaid }: PaymentFormProps) {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage;
  const [method, setMethod] = useState<PaymentMethod>("Cash");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const today = centerNow().date;
  const start = paymentStartDate(validThrough, today);

  async function pay() {
    setSaving(true);
    setError(null);
    const result = await recordPayment(membership.id, { amount: membership.price, method });
    setSaving(false);

    if (result.ok) {
      onPaid(result.data);
    } else {
      setError(problemMessage(result.problem, t));
    }
  }

  return (
    <Stack>
      {error && (
        <Alert color="red" role="alert">
          {error}
        </Alert>
      )}
      <div>
        <Text size="sm" c="dimmed">
          {membership.memberName} · {membership.typeName}
        </Text>
        <Text fz={28} fw={700}>
          {formatPrice(membership.price, language)}
        </Text>
      </div>

      <div>
        <Text size="sm" fw={500} mb={4}>
          {t("payments.dialog.method")}
        </Text>
        <SegmentedControl
          fullWidth
          aria-label={t("payments.dialog.method")}
          value={method}
          onChange={(value) => setMethod(value as PaymentMethod)}
          data={paymentMethods.map((value) => ({
            value,
            label: t(`payments.methods.${value}`),
          }))}
        />
      </div>

      <Text size="sm">
        {validThrough === undefined
          ? t("payments.dialog.startsGeneral")
          : start === today
            ? t("payments.dialog.startsToday")
            : t("payments.dialog.startsOn", { date: formatDate(start, language) })}
      </Text>

      <Group justify="flex-end" mt="sm">
        <Button variant="default" onClick={onCancel} disabled={saving}>
          {t("common.cancel")}
        </Button>
        <Button loading={saving} onClick={() => void pay()}>
          {t("payments.dialog.confirm", { price: formatPrice(membership.price, language) })}
        </Button>
      </Group>
    </Stack>
  );
}
