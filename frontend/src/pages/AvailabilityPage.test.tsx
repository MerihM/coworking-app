import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { searchAvailability, type Availability } from "../features/availability/availabilityApi";
import {
  createReservation,
  getReservationAlternatives,
  type Reservation,
} from "../features/reservations/reservationsApi";
import { testUser } from "../test/fixtures";
import { fakeAuth, renderPage } from "../test/render";
import { AvailabilityPage } from "./AvailabilityPage";

vi.mock("../features/availability/availabilityApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../features/availability/availabilityApi")>()),
  searchAvailability: vi.fn(),
}));

vi.mock("../features/reservations/reservationsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../features/reservations/reservationsApi")>()),
  createReservation: vi.fn(),
  getReservationAlternatives: vi.fn(),
}));

const memberAuth = fakeAuth({ status: "authenticated", user: testUser() });

/** Tuesday 1 December 2026, 09:00–12:00. */
const searchUrl = "/availability?date=2026-12-01&from=09:00&to=12:00&type=MeetingRoom&min=6";

const answer: Availability = {
  date: "2026-12-01",
  from: "09:00:00",
  to: "12:00:00",
  startsAt: "2026-12-01T08:00:00Z",
  endsAt: "2026-12-01T11:00:00Z",
  totalFree: 2,
  rooms: [
    {
      id: 1,
      name: "Conference wing",
      floor: 2,
      description: "Quiet floor with a view",
      workspaces: [
        { id: 10, code: "MR-01", type: "MeetingRoom", capacity: 8 },
        { id: 11, code: "MR-02", type: "MeetingRoom", capacity: 12 },
      ],
    },
  ],
};

/** What POST /api/reservations/mine answers for MR-01. */
const reservation: Reservation = {
  id: 100,
  memberId: 1,
  memberName: "Amra Hodžić",
  memberEmail: "amra@example.com",
  workspaceId: 10,
  workspaceCode: "MR-01",
  workspaceType: "MeetingRoom",
  capacity: 8,
  roomName: "Conference wing",
  startsAt: "2026-12-01T08:00:00Z",
  endsAt: "2026-12-01T11:00:00Z",
  date: "2026-12-01",
  from: "09:00:00",
  to: "12:00:00",
  status: "Confirmed",
  createdAt: "2026-11-27T10:00:00Z",
  cancelledAt: null,
  cancelledByName: null,
  canMemberCancel: true,
};

/** Opens the reservation dialog for MR-01 and clicks its "Reserve" button. */
async function reserveMr01() {
  fireEvent.click(await screen.findByRole("button", { name: "Reserve MR-01" }));
  const dialog = await screen.findByRole("dialog");
  fireEvent.click(within(dialog).getByRole("button", { name: "Reserve" }));
  return dialog;
}

describe("AvailabilityPage", () => {
  beforeEach(() => {
    vi.mocked(searchAvailability).mockReset().mockResolvedValue({ ok: true, data: answer });
    vi.mocked(createReservation).mockReset().mockResolvedValue({ ok: true, data: reservation });
    vi.mocked(getReservationAlternatives).mockReset();
  });

  it("searches with the values from the URL and shows the free workspaces by room", async () => {
    renderPage(<AvailabilityPage />, undefined, searchUrl);

    expect(await screen.findByText("Conference wing")).toBeInTheDocument();
    expect(searchAvailability).toHaveBeenCalledWith({
      Date: "2026-12-01",
      From: "09:00",
      To: "12:00",
      Type: "MeetingRoom",
      MinCapacity: 6,
    });
    expect(screen.getByText("2 free workspaces")).toBeInTheDocument();
    expect(screen.getByText("Tuesday 1 December, 09:00–12:00")).toBeInTheDocument();
    expect(screen.getByText("Floor 2")).toBeInTheDocument();
    expect(screen.getByText("MR-01")).toBeInTheDocument();
    expect(screen.getByText("12 people")).toBeInTheDocument();
  });

  it("doesn't search before the visitor does, then searches from the form", async () => {
    renderPage(<AvailabilityPage />, undefined, "/availability");

    expect(screen.getByText("Choose a date and time, then search.")).toBeInTheDocument();
    expect(searchAvailability).not.toHaveBeenCalled();

    // The form starts with a valid default search (next open day, 09:00–11:00 if possible).
    fireEvent.click(screen.getByRole("button", { name: "Search" }));

    await waitFor(() => expect(searchAvailability).toHaveBeenCalledTimes(1));
    expect(vi.mocked(searchAvailability).mock.calls[0]?.[0]).toEqual({
      Date: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      From: expect.stringMatching(/^\d{2}:\d{2}$/),
      To: expect.stringMatching(/^\d{2}:\d{2}$/),
    });
  });

  it("shows a hint when nothing is free", async () => {
    vi.mocked(searchAvailability).mockResolvedValue({
      ok: true,
      data: { ...answer, totalFree: 0, rooms: [] },
    });

    renderPage(<AvailabilityPage />, undefined, searchUrl);

    expect(await screen.findByText(/Nothing is free at this time/)).toBeInTheDocument();
  });

  it("explains a BR04 refusal (e.g. a hand-edited URL for a Sunday) in the UI language", async () => {
    vi.mocked(searchAvailability).mockResolvedValue({
      ok: false,
      problem: { status: 422, ruleCode: "BR04", detail: "The center is closed on Sundays." },
    });

    renderPage(
      <AvailabilityPage />,
      undefined,
      "/availability?date=2026-11-29&from=10:00&to=12:00",
    );

    expect(await screen.findByText(/outside our working hours/)).toBeInTheDocument();
  });

  it("explains a search for a time that has passed", async () => {
    vi.mocked(searchAvailability).mockResolvedValue({
      ok: false,
      problem: { status: 422, code: "availability.in_past" },
    });

    renderPage(<AvailabilityPage />, undefined, searchUrl);

    expect(await screen.findByText(/already passed/)).toBeInTheDocument();
  });

  // ----- CW-30: reserving from the results (UC04, NFR03: search → Reserve → Reserve → done) -----

  it("lets a member reserve a workspace and shows the confirmation", async () => {
    renderPage(<AvailabilityPage />, memberAuth, searchUrl);

    const dialog = await reserveMr01();

    await waitFor(() =>
      expect(createReservation).toHaveBeenCalledWith({
        workspaceId: 10,
        date: "2026-12-01",
        from: "09:00:00",
        to: "12:00:00",
      }),
    );
    expect(
      await within(dialog).findByText(
        "MR-01 is reserved for you: Tuesday 1 December, 09:00–12:00.",
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: "My reservations" })).toHaveAttribute(
      "href",
      "/reservations",
    );
    // The search reloads: MR-01 is no longer free.
    await waitFor(() => expect(searchAvailability).toHaveBeenCalledTimes(2));
  });

  it("offers alternatives when the slot was taken, and reserves the one the member picks", async () => {
    vi.mocked(createReservation)
      .mockResolvedValueOnce({
        ok: false,
        problem: { status: 409, code: "reservations.slot_taken", ruleCode: "BR01" },
      })
      .mockResolvedValueOnce({
        ok: true,
        data: { ...reservation, workspaceId: 11, workspaceCode: "MR-02" },
      });
    vi.mocked(getReservationAlternatives).mockResolvedValue({
      ok: true,
      data: {
        otherWorkspaces: [
          {
            id: 11,
            code: "MR-02",
            type: "MeetingRoom",
            capacity: 12,
            roomId: 1,
            roomName: "Conference wing",
          },
        ],
        nextSlots: [
          {
            date: "2026-12-01",
            from: "12:00:00",
            to: "15:00:00",
            startsAt: "2026-12-01T11:00:00Z",
            endsAt: "2026-12-01T14:00:00Z",
          },
        ],
      },
    });
    renderPage(<AvailabilityPage />, memberAuth, searchUrl);

    const dialog = await reserveMr01();

    expect(await within(dialog).findByText(/MR-01 is already reserved/)).toBeInTheDocument();
    expect(getReservationAlternatives).toHaveBeenCalledWith({
      WorkspaceId: 10,
      Date: "2026-12-01",
      From: "09:00",
      To: "12:00",
    });
    expect(
      await within(dialog).findByRole("button", { name: "Tuesday 1 December, 12:00–15:00" }),
    ).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: /MR-02/ }));

    await waitFor(() =>
      expect(createReservation).toHaveBeenLastCalledWith({
        workspaceId: 11,
        date: "2026-12-01",
        from: "09:00:00",
        to: "12:00:00",
      }),
    );
    expect(await within(dialog).findByText(/MR-02 is reserved for you/)).toBeInTheDocument();
  });

  it("offers buying a membership when the member has none for that date (BR02)", async () => {
    vi.mocked(createReservation).mockResolvedValue({
      ok: false,
      problem: { status: 422, ruleCode: "BR02" },
    });
    renderPage(<AvailabilityPage />, memberAuth, searchUrl);

    const dialog = await reserveMr01();

    expect(await within(dialog).findByText(/You need an active membership/)).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: "Buy a membership" })).toHaveAttribute(
      "href",
      "/memberships",
    );
    expect(getReservationAlternatives).not.toHaveBeenCalled();
  });

  it("invites a visitor to log in and come back to the same search", async () => {
    renderPage(<AvailabilityPage />, undefined, searchUrl);

    const link = await screen.findByRole("link", { name: "Log in to reserve" });
    const back = `/availability?${new URLSearchParams(searchUrl.split("?")[1]).toString()}`;
    expect(link).toHaveAttribute("href", `/login?from=${encodeURIComponent(back)}`);
    expect(screen.queryByRole("button", { name: "Reserve MR-01" })).not.toBeInTheDocument();
  });

  it("shows no reserve button to employees", async () => {
    renderPage(
      <AvailabilityPage />,
      fakeAuth({
        status: "authenticated",
        user: testUser({ userType: "employee", role: "Receptionist" }),
      }),
      searchUrl,
    );

    expect(await screen.findByText("MR-01")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reserve MR-01" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Log in to reserve" })).not.toBeInTheDocument();
  });
});
