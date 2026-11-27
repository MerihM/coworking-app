import { notifications } from "@mantine/notifications";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { centerNow } from "../../features/availability/workingHours";
import {
  cancelReservation,
  listReservations,
  type Reservation,
  type ReservationPage,
} from "../../features/reservations/reservationsApi";
import { testReservation, testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { ReservationsPage } from "./ReservationsPage";

vi.mock("../../features/reservations/reservationsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/reservations/reservationsApi")>()),
  listReservations: vi.fn(),
  cancelReservation: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const receptionAuth = fakeAuth({
  status: "authenticated",
  user: testUser({ userType: "employee", role: "Receptionist", fullName: "Emir Kovač" }),
});

const pageOf = (items: Reservation[]): ReservationPage => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
});

const confirmed = testReservation();
const cancelledByReception = testReservation({
  id: 101,
  memberId: 2,
  memberName: "Haris Delić",
  memberEmail: "haris@example.com",
  workspaceCode: "MR-A",
  roomName: "Meeting Room A",
  status: "Cancelled",
  cancelledAt: "2026-11-27T09:00:00Z",
  cancelledByName: "Emir Kovač",
});

/** The table row that contains `text`. */
function rowWith(text: string): HTMLElement {
  const row = screen.getByText(text).closest("tr");
  if (!row) {
    throw new Error(`No table row contains "${text}".`);
  }
  return row;
}

describe("ReservationsPage", () => {
  beforeEach(() => {
    vi.mocked(notifications.show).mockReset();
    vi.mocked(listReservations)
      .mockReset()
      .mockResolvedValue({ ok: true, data: pageOf([confirmed, cancelledByReception]) });
    vi.mocked(cancelReservation).mockReset().mockResolvedValue({ ok: true, data: undefined });
  });

  it("lists today's reservations by default, with the member and who cancelled", async () => {
    renderPage(<ReservationsPage />, receptionAuth, "/reception/reservations");

    expect(await screen.findByText("Amra Hodžić")).toBeInTheDocument();
    expect(listReservations).toHaveBeenCalledWith({
      Date: centerNow().date,
      Page: 1,
      PageSize: 20,
    });
    expect(screen.getByRole("link", { name: "Amra Hodžić" })).toHaveAttribute(
      "href",
      "/reception/members/1",
    );

    // Only a confirmed reservation can be cancelled; a cancelled one says by whom.
    const cancelledRow = rowWith("Haris Delić");
    expect(within(cancelledRow).getByText("Cancelled by Emir Kovač")).toBeInTheDocument();
    expect(within(cancelledRow).queryByRole("button")).not.toBeInTheDocument();
    expect(
      within(rowWith("Amra Hodžić")).getByRole("button", {
        name: "Cancel the reservation of Amra Hodžić (OS-01)",
      }),
    ).toBeInTheDocument();
  });

  it("cancels on behalf of the member at any time (BR03), then reloads", async () => {
    // Too late for the member – the reception may still cancel (TC06).
    vi.mocked(listReservations).mockResolvedValue({
      ok: true,
      data: pageOf([testReservation({ canMemberCancel: false })]),
    });
    renderPage(<ReservationsPage />, receptionAuth, "/reception/reservations");

    fireEvent.click(
      await screen.findByRole("button", { name: "Cancel the reservation of Amra Hodžić (OS-01)" }),
    );
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Yes, cancel it" }),
    );

    await waitFor(() => expect(cancelReservation).toHaveBeenCalledWith(100));
    expect(notifications.show).toHaveBeenCalledWith(
      expect.objectContaining({
        color: "green",
        message: "The reservation of Amra Hodžić was cancelled.",
      }),
    );
    await waitFor(() => expect(listReservations).toHaveBeenCalledTimes(2));
  });
});
