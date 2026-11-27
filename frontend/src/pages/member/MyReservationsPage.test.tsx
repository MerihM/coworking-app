import { notifications } from "@mantine/notifications";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  cancelMyReservation,
  listMyReservations,
  type Reservation,
  type ReservationPage,
} from "../../features/reservations/reservationsApi";
import { testReservation, testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { MyReservationsPage } from "./MyReservationsPage";

vi.mock("../../features/reservations/reservationsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/reservations/reservationsApi")>()),
  listMyReservations: vi.fn(),
  cancelMyReservation: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const memberAuth = fakeAuth({ status: "authenticated", user: testUser() });

const pageOf = (items: Reservation[]): ReservationPage => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
});

/** The cancel button of the default test reservation (OS-01, Monday 30 November 13–17). */
const cancelButtonName = "Cancel OS-01, Monday 30 November, 13:00–17:00";

describe("MyReservationsPage", () => {
  beforeEach(() => {
    vi.mocked(notifications.show).mockReset();
    vi.mocked(listMyReservations)
      .mockReset()
      .mockResolvedValue({ ok: true, data: pageOf([testReservation()]) });
    vi.mocked(cancelMyReservation).mockReset().mockResolvedValue({ ok: true, data: undefined });
  });

  it("shows the upcoming reservations first, and the past ones on the second tab", async () => {
    renderPage(<MyReservationsPage />, memberAuth, "/reservations");

    expect(await screen.findByText("Monday 30 November, 13:00–17:00")).toBeInTheDocument();
    expect(listMyReservations).toHaveBeenCalledWith({ Scope: "Upcoming", Page: 1, PageSize: 20 });
    const row = screen.getByText("OS-01").closest("tr");
    expect(row).not.toBeNull();
    if (row) {
      expect(within(row).getByText("Open Space")).toBeInTheDocument();
      expect(within(row).getByText("Confirmed")).toBeInTheDocument();
    }

    fireEvent.click(screen.getByRole("tab", { name: "Past" }));

    await waitFor(() =>
      expect(listMyReservations).toHaveBeenLastCalledWith({
        Scope: "Past",
        Page: 1,
        PageSize: 20,
      }),
    );
  });

  it("cancels a reservation after confirming, then reloads the list", async () => {
    renderPage(<MyReservationsPage />, memberAuth, "/reservations");

    fireEvent.click(await screen.findByRole("button", { name: cancelButtonName }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/OS-01, Monday 30 November, 13:00–17:00/)).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Yes, cancel it" }));

    await waitFor(() => expect(cancelMyReservation).toHaveBeenCalledWith(100));
    expect(notifications.show).toHaveBeenCalledWith(
      expect.objectContaining({ color: "green", message: "The reservation was cancelled." }),
    );
    await waitFor(() => expect(listMyReservations).toHaveBeenCalledTimes(2));
  });

  it("explains instead of offering cancel when the start is less than 2 hours away (BR03)", async () => {
    vi.mocked(listMyReservations).mockResolvedValue({
      ok: true,
      data: pageOf([testReservation({ canMemberCancel: false })]),
    });

    renderPage(<MyReservationsPage />, memberAuth, "/reservations");

    expect(await screen.findByText(/contact the reception to cancel/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: cancelButtonName })).not.toBeInTheDocument();
  });

  it("shows the BR03 message when the API refuses the cancellation", async () => {
    vi.mocked(cancelMyReservation).mockResolvedValue({
      ok: false,
      problem: { status: 422, ruleCode: "BR03" },
    });

    renderPage(<MyReservationsPage />, memberAuth, "/reservations");

    fireEvent.click(await screen.findByRole("button", { name: cancelButtonName }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Yes, cancel it" }),
    );

    await waitFor(() =>
      expect(notifications.show).toHaveBeenCalledWith(
        expect.objectContaining({
          color: "red",
          message: expect.stringMatching(/at the latest 2 hours before it starts/),
        }),
      ),
    );
  });
});
