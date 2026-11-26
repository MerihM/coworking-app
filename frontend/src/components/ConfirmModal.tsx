import { Button, Group, Modal, Text } from "@mantine/core";
import { useTranslation } from "react-i18next";

type ConfirmModalProps = {
  opened: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  /** Red confirm button for destructive actions (delete, deactivate). */
  danger?: boolean;
  /** Shows a spinner on the confirm button while the action runs. */
  loading?: boolean;
  onConfirm: () => void;
  onClose: () => void;
};

/** "Are you sure?" dialog. Reused for every confirmation in the app. */
export function ConfirmModal({
  opened,
  title,
  message,
  confirmLabel,
  danger = false,
  loading = false,
  onConfirm,
  onClose,
}: ConfirmModalProps) {
  const { t } = useTranslation();

  return (
    <Modal opened={opened} onClose={onClose} title={title} centered>
      <Text>{message}</Text>
      <Group justify="flex-end" mt="lg">
        <Button variant="default" onClick={onClose} disabled={loading}>
          {t("common.cancel")}
        </Button>
        <Button color={danger ? "red" : undefined} loading={loading} onClick={onConfirm}>
          {confirmLabel}
        </Button>
      </Group>
    </Modal>
  );
}
