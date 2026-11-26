import { Alert, Button, Group, Modal, PasswordInput, Stack, Text } from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fieldErrors, problemMessage } from "../../api/problemMessages";
import { fullName, resetEmployeePassword, type Employee } from "./employeesApi";
import { resetPasswordSchema, type ResetPasswordFormValues } from "./validation";

type ResetPasswordModalProps = {
  /** The account whose password is reset; null = dialog closed. */
  employee: Employee | null;
  onClose: () => void;
  onDone: (employee: Employee) => void;
};

/**
 * Sets a new password for a staff account. The backend also ends all of that employee's
 * sessions, so whoever knew the old password is logged out everywhere.
 */
export function ResetPasswordModal({ employee, onClose, onDone }: ResetPasswordModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={employee !== null}
      onClose={onClose}
      title={employee ? t("staff.reset.title", { name: fullName(employee) }) : ""}
      centered
    >
      {employee && (
        <ResetPasswordForm
          key={employee.id}
          employee={employee}
          onCancel={onClose}
          onDone={onDone}
        />
      )}
    </Modal>
  );
}

type ResetPasswordFormProps = {
  employee: Employee;
  onCancel: () => void;
  onDone: (employee: Employee) => void;
};

function ResetPasswordForm({ employee, onCancel, onDone }: ResetPasswordFormProps) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const schema = useMemo(() => resetPasswordSchema(t), [t]);

  const form = useForm<ResetPasswordFormValues>({
    mode: "uncontrolled",
    initialValues: { newPassword: "", confirmPassword: "" },
    validate: schemaResolver(schema, { sync: true }),
  });

  async function handleSubmit(values: ResetPasswordFormValues) {
    setSaving(true);
    setError(null);
    const result = await resetEmployeePassword(employee.id, { newPassword: values.newPassword });
    setSaving(false);

    if (result.ok) {
      onDone(employee);
    } else if (result.problem.errors) {
      form.setErrors(fieldErrors(result.problem));
    } else {
      setError(problemMessage(result.problem, t));
    }
  }

  return (
    <form onSubmit={form.onSubmit((values) => void handleSubmit(values))} noValidate>
      <Stack>
        <Text size="sm" c="dimmed">
          {t("staff.reset.hint")}
        </Text>
        {error && (
          <Alert color="red" role="alert">
            {error}
          </Alert>
        )}
        <PasswordInput
          label={t("staff.reset.newPassword")}
          description={t("staff.form.passwordHint")}
          autoComplete="new-password"
          withAsterisk
          key={form.key("newPassword")}
          {...form.getInputProps("newPassword")}
        />
        <PasswordInput
          label={t("staff.reset.confirmPassword")}
          autoComplete="new-password"
          withAsterisk
          key={form.key("confirmPassword")}
          {...form.getInputProps("confirmPassword")}
        />
        <Group justify="flex-end" mt="sm">
          <Button variant="default" onClick={onCancel} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" loading={saving}>
            {t("staff.reset.submit")}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
