import {
  Alert,
  Button,
  Group,
  Modal,
  PasswordInput,
  Select,
  Stack,
  TextInput,
} from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fieldErrors, problemMessage } from "../../api/problemMessages";
import { roleLabelKeys } from "../auth/roles";
import {
  createEmployee,
  employeeRoles,
  updateEmployee,
  type Employee,
  type EmployeeRole,
} from "./employeesApi";
import { createEmployeeSchema, updateEmployeeSchema, type EmployeeFormValues } from "./validation";

type EmployeeFormModalProps = {
  opened: boolean;
  /** null = create a new account; an employee = edit that account. */
  employee: Employee | null;
  /** The administrator is editing their own account: the role can't be changed. */
  isSelf: boolean;
  onClose: () => void;
  onSaved: (employee: Employee, created: boolean) => void;
};

/** Create / edit a staff account (FR12, UC14) in a dialog. */
export function EmployeeFormModal({
  opened,
  employee,
  isSelf,
  onClose,
  onSaved,
}: EmployeeFormModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={employee ? t("staff.form.editTitle") : t("staff.form.createTitle")}
      centered
    >
      {/* key: a fresh form (with fresh initial values) for every employee that is opened. */}
      {opened && (
        <EmployeeForm
          key={employee?.id ?? "new"}
          employee={employee}
          isSelf={isSelf}
          onCancel={onClose}
          onSaved={onSaved}
        />
      )}
    </Modal>
  );
}

type EmployeeFormProps = {
  employee: Employee | null;
  isSelf: boolean;
  onCancel: () => void;
  onSaved: (employee: Employee, created: boolean) => void;
};

function EmployeeForm({ employee, isSelf, onCancel, onSaved }: EmployeeFormProps) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEdit = employee !== null;

  const schema = useMemo(
    () => (isEdit ? updateEmployeeSchema(t) : createEmployeeSchema(t)),
    [isEdit, t],
  );

  const form = useForm<EmployeeFormValues>({
    mode: "uncontrolled",
    initialValues: {
      firstName: employee?.firstName ?? "",
      lastName: employee?.lastName ?? "",
      email: employee?.email ?? "",
      role: employee?.role ?? "Receptionist",
      password: "",
    },
    validate: schemaResolver(schema, { sync: true }),
  });

  async function handleSubmit(values: EmployeeFormValues) {
    setSaving(true);
    setError(null);

    const details = {
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      email: values.email.trim(),
      role: values.role,
    };
    const result = employee
      ? await updateEmployee(employee.id, details)
      : await createEmployee({ ...details, password: values.password });

    setSaving(false);
    if (result.ok) {
      onSaved(result.data, !isEdit);
      return;
    }

    const { problem } = result;
    if (problem.code === "auth.email_taken") {
      form.setFieldError("email", problemMessage(problem, t));
    } else if (problem.errors) {
      form.setErrors(fieldErrors(problem));
    } else {
      setError(problemMessage(problem, t));
    }
  }

  const roleOptions = employeeRoles.map((role: EmployeeRole) => ({
    value: role,
    label: t(roleLabelKeys[role]),
  }));

  return (
    <form onSubmit={form.onSubmit((values) => void handleSubmit(values))} noValidate>
      <Stack>
        {error && (
          <Alert color="red" role="alert">
            {error}
          </Alert>
        )}
        <TextInput
          label={t("staff.form.firstName")}
          autoComplete="off"
          withAsterisk
          key={form.key("firstName")}
          {...form.getInputProps("firstName")}
        />
        <TextInput
          label={t("staff.form.lastName")}
          autoComplete="off"
          withAsterisk
          key={form.key("lastName")}
          {...form.getInputProps("lastName")}
        />
        <TextInput
          label={t("staff.form.email")}
          type="email"
          autoComplete="off"
          withAsterisk
          key={form.key("email")}
          {...form.getInputProps("email")}
        />
        <Select
          label={t("staff.form.role")}
          data={roleOptions}
          allowDeselect={false}
          withAsterisk
          // Same rule as the backend (employees.self_change): nobody demotes themselves by accident.
          disabled={isSelf}
          description={isSelf ? t("staff.form.ownRoleHint") : undefined}
          key={form.key("role")}
          {...form.getInputProps("role")}
        />
        {!isEdit && (
          <PasswordInput
            label={t("staff.form.password")}
            description={t("staff.form.passwordHint")}
            autoComplete="new-password"
            withAsterisk
            key={form.key("password")}
            {...form.getInputProps("password")}
          />
        )}
        <Group justify="flex-end" mt="sm">
          <Button variant="default" onClick={onCancel} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" loading={saving}>
            {isEdit ? t("common.save") : t("staff.form.create")}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
