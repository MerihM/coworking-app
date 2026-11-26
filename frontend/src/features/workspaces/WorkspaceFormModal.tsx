import { Alert, Button, Group, Modal, NumberInput, Select, Stack, TextInput } from "@mantine/core";
import { schemaResolver, useForm } from "@mantine/form";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { fieldErrors, problemMessage } from "../../api/problemMessages";
import { workspaceSchema, type WorkspaceFormValues } from "./validation";
import {
  capacityRange,
  clampCapacity,
  isDesk,
  normalizeCode,
  workspaceTypeLabelKeys,
  workspaceTypes,
} from "./workspaceTypes";
import {
  createWorkspace,
  updateWorkspace,
  type Workspace,
  type WorkspaceType,
} from "./workspacesApi";

/** A room the workspace can be moved to (id + name is all the select needs). */
export type RoomOption = { id: number; name: string };

type WorkspaceFormModalProps = {
  opened: boolean;
  /** null = create a new workspace in `roomId`; a workspace = edit it. */
  workspace: Workspace | null;
  /** The room a NEW workspace goes into (the room detail page that opened the dialog). */
  roomId: number;
  /** Active rooms for the "move to room" select (edit only). */
  rooms: readonly RoomOption[];
  onClose: () => void;
  onSaved: (workspace: Workspace, created: boolean) => void;
};

/** Create / edit a workspace (FR09, UC11). Capacity follows the type: desks are always for 1. */
export function WorkspaceFormModal({
  opened,
  workspace,
  roomId,
  rooms,
  onClose,
  onSaved,
}: WorkspaceFormModalProps) {
  const { t } = useTranslation();

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={workspace ? t("workspaces.form.editTitle") : t("workspaces.form.createTitle")}
      centered
    >
      {opened && (
        <WorkspaceForm
          key={workspace?.id ?? "new"}
          workspace={workspace}
          roomId={roomId}
          rooms={rooms}
          onCancel={onClose}
          onSaved={onSaved}
        />
      )}
    </Modal>
  );
}

type WorkspaceFormProps = Omit<WorkspaceFormModalProps, "opened" | "onClose"> & {
  onCancel: () => void;
};

function WorkspaceForm({ workspace, roomId, rooms, onCancel, onSaved }: WorkspaceFormProps) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const schema = useMemo(() => workspaceSchema(t), [t]);

  // "controlled" (unlike the other forms): the capacity field must re-render when the TYPE
  // changes (min/max, locked for desks). The form is small, so re-rendering on every key is fine.
  const form = useForm<WorkspaceFormValues>({
    mode: "controlled",
    initialValues: {
      roomId: workspace?.roomId ?? roomId,
      code: workspace?.code ?? "",
      type: workspace?.type ?? "HotDesk",
      capacity: workspace?.capacity ?? 1,
    },
    validate: schemaResolver(schema, { sync: true }),
  });

  const type = form.values.type;
  const range = capacityRange(type);
  const desk = isDesk(type);
  // Same rule as the backend (workspaces.fixed_assigned): an assigned fixed desk keeps its type.
  const typeLocked = workspace?.isAssigned === true && workspace.type === "FixedDesk";

  function changeType(value: string | null) {
    if (!value) {
      return;
    }
    const next = value as WorkspaceType;
    const capacity = form.values.capacity;
    form.setFieldValue("type", next);
    // Office for 6 → hot desk: capacity becomes 1; hot desk → meeting room: capacity becomes 2.
    form.setFieldValue(
      "capacity",
      clampCapacity(next, typeof capacity === "number" ? capacity : capacityRange(next).min),
    );
  }

  async function handleSubmit(values: WorkspaceFormValues) {
    setSaving(true);
    setError(null);

    const body = {
      roomId: values.roomId,
      code: normalizeCode(values.code),
      type: values.type,
      capacity: values.capacity,
    };
    const result = workspace
      ? await updateWorkspace(workspace.id, body)
      : await createWorkspace(body);

    setSaving(false);
    if (result.ok) {
      onSaved(result.data, workspace === null);
      return;
    }

    const { problem } = result;
    if (problem.code === "workspaces.code_taken") {
      form.setFieldError("code", problemMessage(problem, t));
    } else if (problem.errors) {
      form.setErrors(fieldErrors(problem)); // keys are camelCase = the field names
    } else {
      // room_inactive, fixed_assigned, network... – a message above the form.
      setError(problemMessage(problem, t));
    }
  }

  const typeOptions = workspaceTypes.map((value) => ({
    value,
    label: t(workspaceTypeLabelKeys[value]),
  }));

  // The current room is always an option, even when it is deactivated (then it isn't in `rooms`).
  const roomOptions = [
    ...(workspace && !rooms.some((room) => room.id === workspace.roomId)
      ? [{ value: String(workspace.roomId), label: workspace.roomName }]
      : []),
    ...rooms.map((room) => ({ value: String(room.id), label: room.name })),
  ];

  return (
    <form onSubmit={form.onSubmit((values) => void handleSubmit(values))} noValidate>
      <Stack>
        {error && (
          <Alert color="red" role="alert">
            {error}
          </Alert>
        )}
        {workspace && (
          <Select
            label={t("workspaces.form.room")}
            description={t("workspaces.form.roomHint")}
            data={roomOptions}
            allowDeselect={false}
            withAsterisk
            value={String(form.values.roomId)}
            onChange={(value) => value && form.setFieldValue("roomId", Number(value))}
            error={form.errors.roomId}
          />
        )}
        <TextInput
          label={t("workspaces.form.code")}
          description={t("workspaces.form.codeHint")}
          autoComplete="off"
          withAsterisk
          {...form.getInputProps("code")}
          // Show what will be stored: "os-01" becomes "OS-01" when the field is left.
          onBlur={(event) => form.setFieldValue("code", normalizeCode(event.currentTarget.value))}
        />
        <Select
          label={t("workspaces.form.type")}
          data={typeOptions}
          allowDeselect={false}
          withAsterisk
          disabled={typeLocked}
          description={typeLocked ? t("workspaces.form.typeLocked") : undefined}
          {...form.getInputProps("type")}
          onChange={changeType}
        />
        <NumberInput
          label={t("workspaces.form.capacity")}
          description={
            desk
              ? t("workspaces.form.capacityDesk")
              : t("workspaces.form.capacityRange", { min: range.min, max: range.max })
          }
          min={range.min}
          max={range.max}
          allowDecimal={false}
          withAsterisk
          disabled={desk}
          {...form.getInputProps("capacity")}
        />
        <Group justify="flex-end" mt="sm">
          <Button variant="default" onClick={onCancel} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" loading={saving}>
            {workspace ? t("common.save") : t("workspaces.form.create")}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
