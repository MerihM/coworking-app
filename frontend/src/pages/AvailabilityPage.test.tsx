import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { searchAvailability, type Availability } from "../features/availability/availabilityApi";
import { renderPage } from "../test/render";
import { AvailabilityPage } from "./AvailabilityPage";

vi.mock("../features/availability/availabilityApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../features/availability/availabilityApi")>()),
  searchAvailability: vi.fn(),
}));

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

describe("AvailabilityPage", () => {
  beforeEach(() => {
    vi.mocked(searchAvailability).mockReset().mockResolvedValue({ ok: true, data: answer });
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
});
