import {
  Alert,
  Anchor,
  Button,
  Group,
  Paper,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { notifications } from "@mantine/notifications";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { fieldErrors, problemMessage } from "../api/problemMessages";
import { useAuth } from "../features/auth/useAuth";
import { createRegisterSchema, type RegisterFormValues } from "../features/auth/validation";

/**
 * Registration of a new member (FR01, UC01): name, surname, e-mail, phone and password.
 * On success the member is logged in at once (the API sets the refresh cookie), and the GuestOnly
 * guard moves them on to ?from=... or the start page.
 */
export function RegisterPage() {
  const { t } = useTranslation();
  const { register } = useAuth();
  const [searchParams] = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // react-i18next gives a NEW t function when the language changes, so the schema (and its
  // messages) is rebuilt in the new language.
  const schema = useMemo(() => createRegisterSchema(t), [t]);

  const form = useForm<RegisterFormValues>({
    mode: "uncontrolled",
    initialValues: {
      firstName: "",
      lastName: "",
      email: "",
      phone: "",
      password: "",
      confirmPassword: "",
    },
    validate: schemaResolver(schema, { sync: true }),
  });

  async function handleSubmit(values: RegisterFormValues) {
    setSubmitting(true);
    setError(null);

    const result = await register({
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      email: values.email.trim(),
      phone: values.phone.trim(),
      password: values.password,
    });

    if (result.ok) {
      notifications.show({ color: "teal", message: t("auth.register.success") });
      return;
    }

    const { problem } = result;
    if (problem.code === "auth.email_taken") {
      // The message belongs to one field, so show it there instead of in the alert.
      form.setFieldError("email", problemMessage(problem, t));
    } else {
      form.setErrors(fieldErrors(problem));
      setError(problemMessage(problem, t));
    }
    setSubmitting(false);
  }

  const loginLink = `/login${searchParams.size > 0 ? `?${searchParams.toString()}` : ""}`;

  return (
    <Paper maw={560} mx="auto" mt="xl" p="lg" withBorder>
      <form onSubmit={form.onSubmit((values) => void handleSubmit(values))} noValidate>
        <Stack>
          <Title order={2}>{t("auth.register.title")}</Title>
          {error && (
            <Alert color="red" role="alert">
              {error}
            </Alert>
          )}

          {/* Two columns on wider screens, one column on phones (grow + wrap). */}
          <Group grow align="flex-start" wrap="wrap">
            <TextInput
              label={t("auth.register.firstName")}
              autoComplete="given-name"
              withAsterisk
              miw={200}
              key={form.key("firstName")}
              {...form.getInputProps("firstName")}
            />
            <TextInput
              label={t("auth.register.lastName")}
              autoComplete="family-name"
              withAsterisk
              miw={200}
              key={form.key("lastName")}
              {...form.getInputProps("lastName")}
            />
          </Group>

          <TextInput
            label={t("auth.register.email")}
            type="email"
            autoComplete="email"
            withAsterisk
            key={form.key("email")}
            {...form.getInputProps("email")}
          />
          <TextInput
            label={t("auth.register.phone")}
            description={t("auth.register.phoneHint")}
            type="tel"
            autoComplete="tel"
            withAsterisk
            key={form.key("phone")}
            {...form.getInputProps("phone")}
          />
          <PasswordInput
            label={t("auth.register.password")}
            description={t("auth.register.passwordHint")}
            autoComplete="new-password"
            withAsterisk
            key={form.key("password")}
            {...form.getInputProps("password")}
          />
          <PasswordInput
            label={t("auth.register.confirmPassword")}
            autoComplete="new-password"
            withAsterisk
            key={form.key("confirmPassword")}
            {...form.getInputProps("confirmPassword")}
          />

          <Button type="submit" loading={submitting}>
            {t("auth.register.submit")}
          </Button>
          <Text size="sm" ta="center">
            {t("auth.register.haveAccount")}{" "}
            <Anchor component={Link} to={loginLink}>
              {t("auth.register.loginLink")}
            </Anchor>
          </Text>
        </Stack>
      </form>
    </Paper>
  );
}
