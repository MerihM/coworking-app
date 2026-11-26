import {
  Alert,
  Button,
  Group,
  Modal,
  NumberInput,
  Stack,
  Textarea,
  TextInput,
} from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fieldErrors, problemMessage } from "../../api/problemMessages";
import { createRoom, updateRoom, type Room } from "./roomsApi";
import { roomLimits, roomSchema, type RoomFormValues } from "./validation";

type RoomFormModalProps = {
  opened: boolean;
  /** null = create a new room; a room = edit it. */
  room: Room | null;
  onClose: () => void;
  onSaved: (room: Room, created: boolean) => void;
};

/** Create / edit a room (FR09, UC11) in a dialog. */
export function RoomFormModal({ opened, room, onClose, onSaved }: RoomFormModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={room ? t("rooms.form.editTitle") : t("rooms.form.createTitle")}
      centered
    >
      {/* key: a fresh form (with fresh initial values) for every room that is opened. */}
      {opened && (
        <RoomForm key={room?.id ?? "new"} room={room} onCancel={onClose} onSaved={onSaved} />
      )}
    </Modal>
  );
}

type RoomFormProps = {
  room: Room | null;
  onCancel: () => void;
  onSaved: (room: Room, created: boolean) => void;
};

function RoomForm({ room, onCancel, onSaved }: RoomFormProps) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const schema = useMemo(() => roomSchema(t), [t]);

  const form = useForm<RoomFormValues>({
    mode: "uncontrolled",
    initialValues: {
      name: room?.name ?? "",
      floor: room?.floor ?? 1,
      description: room?.description ?? "",
    },
    validate: schemaResolver(schema, { sync: true }),
  });

  async function handleSubmit(values: RoomFormValues) {
    setSaving(true);
    setError(null);

    const description = values.description.trim();
    const body = {
      name: values.name.trim(),
      floor: values.floor,
      // An empty text box means "no description" – stored as NULL, not as "".
      description: description === "" ? null : description,
    };
    const result = room ? await updateRoom(room.id, body) : await createRoom(body);

    setSaving(false);
    if (result.ok) {
      onSaved(result.data, room === null);
      return;
    }

    const { problem } = result;
    if (problem.code === "rooms.name_taken") {
      form.setFieldError("name", problemMessage(problem, t));
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
          label={t("rooms.form.name")}
          autoComplete="off"
          withAsterisk
          key={form.key("name")}
          {...form.getInputProps("name")}
        />
        <NumberInput
          label={t("rooms.form.floor")}
          description={t("rooms.form.floorHint")}
          min={roomLimits.floorMin}
          max={roomLimits.floorMax}
          allowDecimal={false}
          withAsterisk
          key={form.key("floor")}
          {...form.getInputProps("floor")}
        />
        <Textarea
          label={t("rooms.form.description")}
          autosize
          minRows={2}
          maxRows={6}
          key={form.key("description")}
          {...form.getInputProps("description")}
        />
        <Group justify="flex-end" mt="sm">
          <Button variant="default" onClick={onCancel} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" loading={saving}>
            {room ? t("common.save") : t("rooms.form.create")}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
