// The reservation dialog (UC04 steps 3–6, with the alternative flows of the Activity diagram):
//   confirm      → the member checks workspace, date and time and clicks "Reserve"
//   alternatives → 409 BR01: the slot was taken; offer another workspace or another time (UC06).
//                  Picking an offer reserves it at once ("ponuda prihvaćena"); "Give up" ends.
//   done         → the confirmation (UC04 step 6) with a link to "My reservations".
// BR04 / in the past / BR02 refusals stay on the confirm step with a message; for BR02 the
// message offers buying a membership (UC05 flow 4a). Search → Reserve → Reserve → confirmation
// = 4 steps (NFR03).
import { Alert, Badge, Button, Center, Group, Loader, Modal, Stack, Text } from "@mantine/core";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { ApiProblem } from "../../api/problem";
import { problemMessage } from "../../api/problemMessages";
import { formatLongDate } from "../../lib/format";
import { workspaceTypeColors, workspaceTypeLabelKeys } from "../workspaces/workspaceTypes";
import {
  createReservation,
  getReservationAlternatives,
  SLOT_TAKEN,
  type Reservation,
  type ReservationAlternatives,
} from "./reservationsApi";
import {
  slotAtTime,
  slotWithWorkspace,
  toAlternativesQuery,
  toCreateRequest,
  type ReservationSlot,
} from "./slot";

type Step = "confirm" | "alternatives" | "done";

type Props = {
  /** What the member clicked "Reserve" on. Render with a `key`, so every opening starts fresh. */
  slot: ReservationSlot;
  onClose: () => void;
  /** Called once a reservation exists – the page reloads its search (the workspace is no longer free). */
  onReserved: (reservation: Reservation) => void;
};

export function ReserveModal({ slot, onClose, onReserved }: Props) {
  const { t, i18n } = useTranslation();
  const [current, setCurrent] = useState(slot);
  const [step, setStep] = useState<Step>("confirm");
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [alternatives, setAlternatives] = useState<ReservationAlternatives | null>(null);
  const [created, setCreated] = useState<Reservation | null>(null);

  /** "Monday 30 November, 13:00–15:00" */
  const when = (target: ReservationSlot) =>
    t("availability.period", {
      date: formatLongDate(target.date, i18n.resolvedLanguage),
      from: target.from,
      to: target.to,
    });

  async function loadAlternatives(target: ReservationSlot) {
    setAlternatives(null);
    const result = await getReservationAlternatives(toAlternativesQuery(target));
    if (result.ok) {
      setAlternatives(result.data);
    } else {
      setProblem(result.problem);
    }
  }

  async function reserve(target: ReservationSlot) {
    setCurrent(target);
    setSaving(true);
    setProblem(null);
    const result = await createReservation(toCreateRequest(target));
    setSaving(false);

    if (result.ok) {
      setCreated(result.data);
      setStep("done");
      onReserved(result.data);
      return;
    }

    if (result.problem.code === SLOT_TAKEN) {
      // BR01 → UC06. Also when an offer was taken by someone else meanwhile: fresh offers.
      setStep("alternatives");
      await loadAlternatives(target);
      return;
    }

    setStep("confirm");
    setProblem(result.problem);
  }

  function renderProblem() {
    if (!problem) {
      return null;
    }
    if (problem.ruleCode === "BR02") {
      // Activity diagram: "Prikaži poruku i ponudi kupovinu članarine".
      return (
        <Alert color="yellow" role="alert">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">{problemMessage(problem, t)}</Text>
            <Button component={Link} to="/memberships" size="xs" variant="light" color="yellow">
              {t("reservations.reserve.buyMembership")}
            </Button>
          </Stack>
        </Alert>
      );
    }
    return (
      <Alert color="red" role="alert">
        <Text size="sm">{problemMessage(problem, t)}</Text>
      </Alert>
    );
  }

  function renderConfirm() {
    const { workspace } = current;
    return (
      <Stack gap="md">
        <Stack gap={4}>
          <Group gap="xs">
            <Text fw={600}>{workspace.code}</Text>
            <Badge color={workspaceTypeColors[workspace.type]} variant="light">
              {t(workspaceTypeLabelKeys[workspace.type])}
            </Badge>
          </Group>
          <Text size="sm" c="dimmed">
            {workspace.roomName} · {t("availability.capacity", { count: workspace.capacity })}
          </Text>
          <Text>{when(current)}</Text>
        </Stack>
        {renderProblem()}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button loading={saving} onClick={() => void reserve(current)}>
            {t("reservations.reserve.confirm")}
          </Button>
        </Group>
      </Stack>
    );
  }

  function renderAlternatives() {
    const nothing =
      alternatives !== null &&
      alternatives.otherWorkspaces.length === 0 &&
      alternatives.nextSlots.length === 0;

    return (
      <Stack gap="md">
        <Text size="sm">{t("reservations.reserve.taken", { code: current.workspace.code })}</Text>
        {renderProblem()}

        {alternatives === null && !problem && (
          <Center py="sm">
            <Loader size="sm" aria-label={t("reservations.reserve.loadingAlternatives")} />
          </Center>
        )}

        {nothing && <Alert color="gray">{t("reservations.reserve.noAlternatives")}</Alert>}

        {alternatives && alternatives.otherWorkspaces.length > 0 && (
          <Stack gap="xs">
            <Text fw={600} size="sm">
              {t("reservations.reserve.otherWorkspaces", { when: when(current) })}
            </Text>
            {alternatives.otherWorkspaces.map((other) => (
              <Button
                key={other.id}
                variant="light"
                justify="space-between"
                fullWidth
                loading={saving && current.workspace.id === other.id}
                disabled={saving}
                onClick={() => void reserve(slotWithWorkspace(current, other))}
                rightSection={<Text size="xs">{other.roomName}</Text>}
              >
                {other.code}
              </Button>
            ))}
          </Stack>
        )}

        {alternatives && alternatives.nextSlots.length > 0 && (
          <Stack gap="xs">
            <Text fw={600} size="sm">
              {t("reservations.reserve.otherTimes", { code: current.workspace.code })}
            </Text>
            {alternatives.nextSlots.map((other) => {
              const offer = slotAtTime(current, other);
              return (
                <Button
                  key={other.startsAt}
                  variant="light"
                  fullWidth
                  disabled={saving}
                  onClick={() => void reserve(offer)}
                >
                  {when(offer)}
                </Button>
              );
            })}
          </Stack>
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={saving}>
            {t("reservations.reserve.giveUp")}
          </Button>
        </Group>
      </Stack>
    );
  }

  function renderDone() {
    return (
      <Stack gap="md">
        <Alert color="green" role="status">
          <Text size="sm">
            {t("reservations.reserve.done", {
              code: created?.workspaceCode ?? current.workspace.code,
              when: when(current),
            })}
          </Text>
        </Alert>
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            {t("reservations.reserve.close")}
          </Button>
          <Button component={Link} to="/reservations">
            {t("reservations.reserve.myReservations")}
          </Button>
        </Group>
      </Stack>
    );
  }

  const titles: Record<Step, string> = {
    confirm: t("reservations.reserve.title"),
    alternatives: t("reservations.reserve.takenTitle"),
    done: t("reservations.reserve.doneTitle"),
  };

  return (
    <Modal opened onClose={saving ? () => undefined : onClose} title={titles[step]} centered>
      {step === "confirm" && renderConfirm()}
      {step === "alternatives" && renderAlternatives()}
      {step === "done" && renderDone()}
    </Modal>
  );
}
