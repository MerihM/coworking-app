// Choices of the member form's "Fixed desk" Select (CW-28). Pure, so it is easy to unit-test.
import type { FixedDeskOption, Member } from "./membersApi";

/**
 * The choices of the "Fixed desk" Select. A desk that belongs to someone else is shown but
 * disabled (with the holder's name), so reception sees why it can't be chosen. The member's own
 * desk is always an option – even when its room was deactivated in the meantime and the API no
 * longer lists it – otherwise saving the form would silently remove the assignment.
 */
export function fixedDeskOptions(desks: FixedDeskOption[], member: Member | null) {
  const options = desks.map((desk) => {
    const takenByOther = desk.assignedMemberId != null && desk.assignedMemberId !== member?.id;
    return {
      value: String(desk.id),
      label: takenByOther
        ? `${desk.code} – ${desk.roomName} (${desk.assignedMemberName ?? ""})`
        : `${desk.code} – ${desk.roomName}`,
      disabled: takenByOther,
    };
  });

  const own = member?.fixedDesk;
  if (own && !desks.some((desk) => desk.id === own.id)) {
    options.unshift({ value: String(own.id), label: own.code, disabled: false });
  }
  return options;
}
