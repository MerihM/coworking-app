import { Alert, Button, Paper, PasswordInput, Stack, TextInput, Title } from "@mantine/core";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../features/auth/useAuth";

/**
 * Minimal login form (CW-15), enough to use the app. CW-16 replaces it with the full version:
 * @mantine/form + zod validation, translated server errors per field, link to registration.
 *
 * After a successful login there is no navigate() here: the session changes, the GuestOnly guard
 * around this route notices it and redirects to ?from=... (or the start page).
 */
export function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    const result = await login({ email, password });
    if (!result.ok) {
      setError(result.problem.status === 429 ? t("auth.login.tooMany") : t("auth.login.failed"));
      setSubmitting(false);
    }
  }

  return (
    <Paper maw={420} mx="auto" mt="xl" p="lg" withBorder>
      <form onSubmit={(event) => void handleSubmit(event)}>
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
            required
            value={email}
            onChange={(event) => setEmail(event.currentTarget.value)}
          />
          <PasswordInput
            label={t("auth.login.password")}
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.currentTarget.value)}
          />
          <Button type="submit" loading={submitting}>
            {t("auth.login.submit")}
          </Button>
        </Stack>
      </form>
    </Paper>
  );
}
