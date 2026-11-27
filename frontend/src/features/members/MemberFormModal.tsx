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
import { useApiQuery } from "../../api/useApiQuery";
import { createMember, listFixedDesks, updateMember, type Member } from "./membersApi";
import { fixedDeskOptions } from "./fixedDeskOptions";
import { createMemberSchema, updateMemberSchema, type MemberFormValues } from "./validation";

type MemberFormModalProps = {
  opened: boolean;
  /** null = register a new member; a member = edit them. */
  member: Member | null;
  onClose: () => void;
  onSaved: (member: Member, created: boolean) => void;
};

/** Create / edit a member at the reception (FR08, UC10), including the fixed desk. */
export function MemberFormModal({ opened, member, onClose, onSaved }: MemberFormModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={member ? t("members.form.editTitle") : t("members.form.createTitle")}
      centered
    >
      {/* key: a fresh form (with fresh initial values) for every member that is opened. */}
      {opened && (
        <MemberForm
          key={member?.id ?? "new"}
          member={member}
          onCancel={onClose}
          onSaved={onSaved}
        />
      )}
    </Modal>
  );
}

type MemberFormProps = {
  member: Member | null;
  onCancel: () => void;
  onSaved: (member: Member, created: boolean) => void;
};

function MemberForm({ member, onCancel, onSaved }: MemberFormProps) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEdit = member !== null;

  const desks = useApiQuery(listFixedDesks, undefined);
  const deskOptions = useMemo(
    () => fixedDeskOptions(desks.data ?? [], member),
    [desks.data, member],
  );

  const schema = useMemo(
    () => (isEdit ? updateMemberSchema(t) : createMemberSchema(t)),
    [isEdit, t],
  );

  const form = useForm<MemberFormValues>({
    mode: "uncontrolled",
    initialValues: {
      firstName: member?.firstName ?? "",
      lastName: member?.lastName ?? "",
      email: member?.email ?? "",
      phone: member?.phone ?? "",
      fixedDeskId: member?.fixedDesk ? String(member.fixedDesk.id) : null,
      password: "",
    },
    validate: schemaResolver(schema, { sync: true }),
  });

  async function handleSubmit(values: MemberFormValues) {
    setSaving(true);
    setError(null);

    const details = {
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      email: values.email.trim(),
      phone: values.phone.trim(),
      fixedWorkspaceId: values.fixedDeskId ? Number(values.fixedDeskId) : null,
    };
    const result = member
      ? await updateMember(member.id, details)
      : await createMember({ ...details, password: values.password });

    setSaving(false);
    if (result.ok) {
      onSaved(result.data, !isEdit);
      return;
    }

    const { problem } = result;
    if (problem.code === "auth.email_taken") {
      form.setFieldError("email", problemMessage(problem, t));
    } else if (
      problem.code === "members.fixed_desk_taken" ||
      problem.code === "members.fixed_desk_invalid"
    ) {
      form.setFieldError("fixedDeskId", problemMessage(problem, t));
    } else if (problem.errors) {
      form.setErrors(fieldErrors(problem));
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
        <TextInput
          label={t("members.form.firstName")}
          autoComplete="off"
          withAsterisk
          key={form.key("firstName")}
          {...form.getInputProps("firstName")}
        />
        <TextInput
          label={t("members.form.lastName")}
          autoComplete="off"
          withAsterisk
          key={form.key("lastName")}
          {...form.getInputProps("lastName")}
        />
        <TextInput
          label={t("members.form.email")}
          type="email"
          autoComplete="off"
          withAsterisk
          key={form.key("email")}
          {...form.getInputProps("email")}
        />
        <TextInput
          label={t("members.form.phone")}
          type="tel"
          autoComplete="off"
          withAsterisk
          key={form.key("phone")}
          {...form.getInputProps("phone")}
        />
        <Select
          label={t("members.form.fixedDesk")}
          description={t("members.form.fixedDeskHint")}
          placeholder={t("members.form.noFixedDesk")}
          data={deskOptions}
          clearable
          searchable
          nothingFoundMessage={t("members.form.noFixedDesks")}
          key={form.key("fixedDeskId")}
          {...form.getInputProps("fixedDeskId")}
        />
        {!isEdit && (
          <PasswordInput
            label={t("members.form.password")}
            description={t("members.form.passwordHint")}
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
            {isEdit ? t("common.save") : t("members.form.create")}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
