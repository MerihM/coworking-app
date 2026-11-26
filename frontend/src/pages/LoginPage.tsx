import {
  Alert,
  Anchor,
  Button,
  Paper,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { fieldErrors, problemMessage } from "../api/problemMessages";
import { useAuth } from "../features/auth/useAuth";
import { createLoginSchema, type LoginFormValues } from "../features/auth/validation";

/**
 * Login for members and staff (one form, the backend finds out who it is).
 *
 * After a successful login there is no navigate() here: the session changes, the GuestOnly guard
 * around this route notices it and redirects to ?from=... (or the start page).
 */
export function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const [searchParams] = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // react-i18next gives a NEW t function when the language changes, so the schema (and its
  // messages) is rebuilt in the new language.
  const schema = useMemo(() => createLoginSchema(t), [t]);

  // "uncontrolled": inputs keep their own value; the form reads them on submit. Fewer re-renders
  // than controlled mode, and the Mantine default since v7.
  const form = useForm<LoginFormValues>({
    mode: "uncontrolled",
    initialValues: { email: "", password: "" },
    validate: schemaResolver(schema, { sync: true }),
  });

  async function handleSubmit(values: LoginFormValues) {
    setSubmitting(true);
    setError(null);

    const result = await login({ email: values.email.trim(), password: values.password });
    if (!result.ok) {
      form.setErrors(fieldErrors(result.problem));
      setError(problemMessage(result.problem, t));
      setSubmitting(false);
    }
  }

  // Keep the "?from=" so that registering instead of logging in still ends on the wanted page.
  const registerLink = `/register${searchParams.size > 0 ? `?${searchParams.toString()}` : ""}`;

  return (
    <Paper maw={420} mx="auto" mt="xl" p="lg" withBorder>
      <form onSubmit={form.onSubmit((values) => void handleSubmit(values))} noValidate>
        <Stack>
          <Title order={2}>{t("auth.login.title")}</Title>
          {error && (
            <Alert color="red" role="alert">
              {error}
            </Alert>
          )}
          <TextInput
            label={t("auth.login.email")}
            type="email"
            autoComplete="email"
            withAsterisk
            key={form.key("email")}
            {...form.getInputProps("email")}
          />
          <PasswordInput
            label={t("auth.login.password")}
            autoComplete="current-password"
            withAsterisk
            key={form.key("password")}
            {...form.getInputProps("password")}
          />
          <Button type="submit" loading={submitting}>
            {t("auth.login.submit")}
          </Button>
          <Text size="sm" ta="center">
            {t("auth.login.noAccount")}{" "}
            <Anchor component={Link} to={registerLink}>
              {t("auth.login.registerLink")}
            </Anchor>
          </Text>
        </Stack>
      </form>
    </Paper>
  );
}
