import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { centerNow } from "../../features/availability/workingHours";
import { presetPeriod } from "../../features/reports/reportPeriod";
import {
  getOccupancyRevenueReport,
  type OccupancyRevenueReport,
} from "../../features/reports/reportsApi";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { ReportPage } from "./ReportPage";

vi.mock("../../features/reports/reportsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/reports/reportsApi")>()),
  getOccupancyRevenueReport: vi.fn(),
}));

const managerAuth = fakeAuth({
  status: "authenticated",
  user: testUser({ userType: "employee", role: "Manager", fullName: "Lejla Begić" }),
});

const report: OccupancyRevenueReport = {
  from: "2026-11-01",
  to: "2026-11-30",
  generatedAt: "2026-11-27T13:00:00Z",
  occupancy: {
    availableHours: 10063,
    reservedHours: 660,
    occupancyRate: 0.0656,
    reservationCount: 100,
    cancelledCount: 4,
    byType: [
      {
        type: "HotDesk",
        workspaceCount: 24,
        availableHours: 8328,
        reservedHours: 400,
        occupancyRate: 0.048,
        reservationCount: 70,
      },
    ],
    byRoom: [
      {
        roomId: 1,
        roomName: "Open Space",
        workspaceCount: 24,
        availableHours: 8328,
        reservedHours: 400,
        occupancyRate: 0.048,
        reservationCount: 70,
      },
    ],
    workspaces: [
      {
        workspaceId: 7,
        code: "OS-04",
        type: "HotDesk",
        roomName: "Open Space",
        availableHours: 347,
        reservedHours: 152,
        occupancyRate: 0.438,
        reservationCount: 19,
      },
    ],
  },
  revenue: {
    total: 705,
    paymentCount: 18,
    byMembershipType: [{ membershipTypeId: 3, name: "Weekly", paymentCount: 8, total: 480 }],
    byMethod: [
      { method: "Cash", paymentCount: 10, total: 300 },
      { method: "Card", paymentCount: 8, total: 405 },
    ],
    byDay: [{ date: "2026-11-02", paymentCount: 2, total: 120 }],
  },
};

const emptyReport: OccupancyRevenueReport = {
  ...report,
  occupancy: {
    ...report.occupancy,
    reservedHours: 0,
    occupancyRate: 0,
    reservationCount: 0,
    cancelledCount: 0,
  },
  revenue: { total: 0, paymentCount: 0, byMembershipType: [], byMethod: [], byDay: [] },
};

describe("ReportPage", () => {
  const originalPrint = window.print;

  beforeEach(() => {
    vi.mocked(getOccupancyRevenueReport).mockReset().mockResolvedValue({ ok: true, data: report });
    window.print = vi.fn();
  });

  afterEach(() => {
    window.print = originalPrint;
  });

  it("loads this month by default and shows the totals and tables", async () => {
    renderPage(<ReportPage />, managerAuth, "/manage/reports");

    const thisMonth = presetPeriod("thisMonth", centerNow().date);
    await waitFor(() =>
      expect(getOccupancyRevenueReport).toHaveBeenCalledWith({
        From: thisMonth.from,
        To: thisMonth.to,
      }),
    );

    expect(await screen.findByText("6.6%")).toBeInTheDocument(); // occupancy 0.0656
    expect(screen.getByText("660 h reserved of 10,063 h open")).toBeInTheDocument();
    expect(screen.getByText("100")).toBeInTheDocument();
    expect(screen.getByText("4 cancelled")).toBeInTheDocument();
    expect(screen.getByText("18 payments")).toBeInTheDocument();

    // Most used workspaces: code, type and rate.
    const topRow = screen.getByText("OS-04").closest("tr");
    expect(topRow).not.toBeNull();
    if (topRow) {
      expect(within(topRow).getByText("43.8%")).toBeInTheDocument();
      expect(within(topRow).getByText("Hot desk")).toBeInTheDocument();
    }

    // Revenue: membership type and both methods.
    expect(screen.getByText("Weekly")).toBeInTheDocument();
    expect(screen.getByText("Cash")).toBeInTheDocument();
    expect(screen.getByText("Card")).toBeInTheDocument();
  });

  it("a preset changes the period that is loaded", async () => {
    renderPage(<ReportPage />, managerAuth, "/manage/reports");
    await screen.findByText("6.6%");

    fireEvent.click(screen.getByRole("radio", { name: "Last 7 days" }));

    const last7 = presetPeriod("last7", centerNow().date);
    await waitFor(() =>
      expect(getOccupancyRevenueReport).toHaveBeenLastCalledWith({
        From: last7.from,
        To: last7.to,
      }),
    );
  });

  it("says so when the period has no reservations and no payments", async () => {
    vi.mocked(getOccupancyRevenueReport).mockResolvedValue({ ok: true, data: emptyReport });

    renderPage(<ReportPage />, managerAuth, "/manage/reports");

    expect(
      await screen.findByText("There are no reservations or payments in this period."),
    ).toBeInTheDocument();
  });

  it("prints through the browser", async () => {
    renderPage(<ReportPage />, managerAuth, "/manage/reports");
    await screen.findByText("6.6%");

    fireEvent.click(screen.getByRole("button", { name: "Print" }));

    expect(window.print).toHaveBeenCalledOnce();
  });

  it("shows the error with a retry button when loading fails", async () => {
    // Status 0 = the API didn't answer (see callApi).
    vi.mocked(getOccupancyRevenueReport).mockResolvedValue({ ok: false, problem: { status: 0 } });

    renderPage(<ReportPage />, managerAuth, "/manage/reports");

    const alert = await screen.findByRole("alert");
    expect(within(alert).getByText("The report couldn't be loaded.")).toBeInTheDocument();
    expect(within(alert).getByRole("button", { name: "Try again" })).toBeInTheDocument();
  });
});
