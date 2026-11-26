import { Alert, Button, Group, Modal, NumberInput, Stack, Text, TextInput } from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fieldErrors, problemMessage } from "../../api/problemMessages";
import { formatDuration } from "../../lib/format";
import {
  createMembershipType,
  updateMembershipType,
  type MembershipType,
} from "./membershipTypesApi";
import {
  membershipTypeLimits,
  membershipTypeSchema,
  type MembershipTypeFormValues,
} from "./validation";

type MembershipTypeFormModalProps = {
  opened: boolean;
  /** null = create a new type; a type = edit it. */
  membershipType: MembershipType | null;
  onClose: () => void;
  onSaved: (membershipType: MembershipType, created: boolean) => void;
};

/** Create / edit a membership type – name, duration and price (FR10, UC12). */
export function MembershipTypeFormModal({
  opened,
  membershipType,
  onClose,
  onSaved,
}: MembershipTypeFormModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={
        membershipType ? t("membershipTypes.form.editTitle") : t("membershipTypes.form.createTitle")
      }
      centered
    >
      {opened && (
        <MembershipTypeForm
          key={membershipType?.id ?? "new"}
          membershipType={membershipType}
          onCancel={onClose}
          onSaved={onSaved}
        />
      )}
    </Modal>
  );
}

type MembershipTypeFormProps = {
  membershipType: MembershipType | null;
  onCancel: () => void;
  onSaved: (membershipType: MembershipType, created: boolean) => void;
};

function MembershipTypeForm({ membershipType, onCancel, onSaved }: MembershipTypeFormProps) {
  const { t, i18n } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const schema = useMemo(() => membershipTypeSchema(t), [t]);
  const bosnian = i18n.resolvedLanguage === "bs";

  // "controlled": the "= 30 days" preview under the duration re-renders while typing.
  const form = useForm<MembershipTypeFormValues>({
    mode: "controlled",
    initialValues: {
      name: membershipType?.name ?? "",
      durationDays: membershipType?.durationDays ?? 30,
      price: membershipType?.price ?? 0,
    },
    validate: schemaResolver(schema, { sync: true }),
  });

  const duration = form.values.durationDays;

  async function handleSubmit(values: MembershipTypeFormValues) {
    setSaving(true);
    setError(null);

    const body = {
      name: values.name.trim(),
      durationDays: values.durationDays,
      price: values.price, // a JSON number, never a string (CW-13 JSON contract)
    };
    const result = membershipType
      ? await updateMembershipType(membershipType.id, body)
      : await createMembershipType(body);

    setSaving(false);
    if (result.ok) {
      onSaved(result.data, membershipType === null);
      return;
    }

    const { problem } = result;
    if (problem.code === "membership_types.name_taken") {
      form.setFieldError("name", problemMessage(problem, t));
    } else if (problem.errors) {
      form.setErrors(fieldErrors(problem)); // keys are camelCase = the field names
    } else {
      setError(problemMessage(problem, t));
    }
  }

  return (
    <form onSubmit={form.onSubmit((values) => void handleSubmit(values))} noValidate>
      <Stack>
        {error && (
          <Alert color="red" role="alert">
            {error}
          </Alert>
        )}
        {membershipType && membershipType.membershipCount > 0 && (
          // Changing the price is safe, but the manager should know who is (not) affected.
          <Alert color="blue">
            {t("membershipTypes.form.soldHint", { count: membershipType.membershipCount })}
          </Alert>
        )}
        <TextInput
          label={t("membershipTypes.form.name")}
          autoComplete="off"
          withAsterisk
          {...form.getInputProps("name")}
        />
        <NumberInput
          label={t("membershipTypes.form.duration")}
          description={t("membershipTypes.form.durationHint")}
          min={membershipTypeLimits.durationMin}
          max={membershipTypeLimits.durationMax}
          allowDecimal={false}
          allowNegative={false}
          withAsterisk
          {...form.getInputProps("durationDays")}
        />
        {typeof duration === "number" && duration >= 1 && (
          <Text size="xs" c="dimmed" mt={-8}>
            = {formatDuration(duration, t)}
          </Text>
        )}
        <NumberInput
          label={t("membershipTypes.form.price")}
          // The decimal separator follows the UI language: "15,50" in Bosnian, "15.50" in English.
          decimalSeparator={bosnian ? "," : "."}
          decimalScale={2}
          fixedDecimalScale
          allowNegative={false}
          // The currency is a label NEXT TO the number, not part of the typed text (a `suffix`
          // would be: then editing "15.50 BAM" by hand, or pasting a number, can drop the
          // value – the CW-24 test caught exactly that).
          rightSection={
            <Text size="xs" c="dimmed">
              {bosnian ? "KM" : "BAM"}
            </Text>
          }
          rightSectionWidth={48}
          rightSectionPointerEvents="none"
          min={membershipTypeLimits.priceMin}
          max={membershipTypeLimits.priceMax}
          withAsterisk
          {...form.getInputProps("price")}
        />
        <Group justify="flex-end" mt="sm">
          <Button variant="default" onClick={onCancel} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" loading={saving}>
            {membershipType ? t("common.save") : t("membershipTypes.form.create")}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
