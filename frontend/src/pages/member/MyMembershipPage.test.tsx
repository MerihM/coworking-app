import { fireEvent, screen, waitFor } from "@testing-library/react";
import { notifications } from "@mantine/notifications";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { addDays, centerNow } from "../../features/availability/workingHours";
import { getMembershipOffer } from "../../features/membershipTypes/membershipTypesApi";
import {
  buyMembership,
  listMyMemberships,
  type Membership,
} from "../../features/memberships/membershipsApi";
import { formatDate } from "../../lib/format";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { MyMembershipPage } from "./MyMembershipPage";

vi.mock("../../features/memberships/membershipsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/memberships/membershipsApi")>()),
  listMyMemberships: vi.fn(),
  buyMembership: vi.fn(),
  cancelMyMembership: vi.fn(),
}));

vi.mock("../../features/membershipTypes/membershipTypesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/membershipTypes/membershipTypesApi")>()),
  getMembershipOffer: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const memberAuth = fakeAuth({ status: "authenticated", user: testUser() });

// Dates relative to the real "today" in Sarajevo, so the test never goes stale.
const today = centerNow().date;

function membership(overrides: Partial<Membership>): Membership {
  return {
    id: 1,
    memberId: 1,
    memberName: "Amra Hodžić",
    memberEmail: "amra@example.com",
    membershipTypeId: 3,
    typeName: "Monthly",
    durationDays: 30,
    price: 180,
    status: "Active",
    purchasedAt: "2026-11-27T09:00:00Z",
    startDate: addDays(today, -5),
    endDate: addDays(today, 25),
    lastDay: addDays(today, 24),
    payment: {
      paidAt: "2026-11-27T09:05:00Z",
      amount: 180,
      method: "Cash",
      recordedByName: "Emir Kovač",
    },
    ...overrides,
  };
}

const unpaidDaily = membership({
  id: 9,
  typeName: "Daily",
  durationDays: 1,
  price: 15,
  status: "Unpaid",
  startDate: null,
  endDate: null,
  lastDay: null,
  payment: null,
});

describe("MyMembershipPage", () => {
  beforeEach(() => {
    vi.mocked(listMyMemberships).mockReset().mockResolvedValue({ ok: true, data: [] });
    vi.mocked(buyMembership).mockReset();
    vi.mocked(notifications.show).mockReset();
    vi.mocked(getMembershipOffer)
      .mockReset()
      .mockResolvedValue({
        ok: true,
        data: [
          { id: 1, name: "Daily", durationDays: 1, price: 15 },
          { id: 3, name: "Monthly", durationDays: 30, price: 180 },
        ],
      });
  });

  it("says so when there is no membership, and offers to buy one", async () => {
    renderPage(<MyMembershipPage />, memberAuth);

    expect(await screen.findByText("No active membership")).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Buy Monthly" })).toBeEnabled();
    expect(screen.getByText("You haven't bought any memberships yet.")).toBeInTheDocument();
  });

  it("shows the active membership with its last valid day", async () => {
    vi.mocked(listMyMemberships).mockResolvedValue({ ok: true, data: [membership({})] });

    renderPage(<MyMembershipPage />, memberAuth);

    expect(await screen.findByText("Your membership is active")).toBeInTheDocument();
    const lastDay = formatDate(addDays(today, 24), "en");
    expect(screen.getByText(`Monthly – valid until ${lastDay}.`)).toBeInTheDocument();
    expect(
      screen.getByText("A new membership starts on the day after your current one ends."),
    ).toBeInTheDocument();
  });

  it("shows an unpaid order with the amount and blocks a second purchase", async () => {
    vi.mocked(listMyMemberships).mockResolvedValue({ ok: true, data: [unpaidDaily] });

    renderPage(<MyMembershipPage />, memberAuth);

    expect(await screen.findByText("Waiting for payment")).toBeInTheDocument();
    expect(screen.getByText(/^Daily: pay BAM\s15\.00 at the reception/)).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Buy Monthly" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Cancel this order" })).toBeInTheDocument();
  });

  it("buys after confirmation and reloads the list", async () => {
    vi.mocked(buyMembership).mockResolvedValue({ ok: true, data: unpaidDaily });
    renderPage(<MyMembershipPage />, memberAuth);

    fireEvent.click(await screen.findByRole("button", { name: "Buy Monthly" }));
    expect(screen.getByText("Buy Monthly?")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Buy" }));

    await waitFor(() => expect(buyMembership).toHaveBeenCalledWith(3));
    await waitFor(() => expect(listMyMemberships).toHaveBeenCalledTimes(2));
    expect(notifications.show).toHaveBeenCalledWith(expect.objectContaining({ color: "green" }));
  });

  it("explains a refused purchase with the translated error", async () => {
    vi.mocked(buyMembership).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "memberships.unpaid_exists" },
    });
    renderPage(<MyMembershipPage />, memberAuth);

    fireEvent.click(await screen.findByRole("button", { name: "Buy Daily" }));
    fireEvent.click(screen.getByRole("button", { name: "Buy" }));

    await waitFor(() =>
      expect(notifications.show).toHaveBeenCalledWith(
        expect.objectContaining({
          color: "red",
          message:
            "There is already an unpaid membership waiting for payment. Pay or cancel it first.",
        }),
      ),
    );
  });
});
