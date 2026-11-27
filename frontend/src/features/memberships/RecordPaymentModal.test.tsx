import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { addDays, centerNow } from "../availability/workingHours";
import { formatDate } from "../../lib/format";
import { renderPage } from "../../test/render";
import { recordPayment, type Membership } from "./membershipsApi";
import { RecordPaymentModal } from "./RecordPaymentModal";

vi.mock("./membershipsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./membershipsApi")>()),
  recordPayment: vi.fn(),
}));

const today = centerNow().date;

const unpaid: Membership = {
  id: 9,
  memberId: 7,
  memberName: "Amra Hodžić",
  memberEmail: "amra@example.com",
  membershipTypeId: 3,
  typeName: "Monthly",
  durationDays: 30,
  price: 180,
  status: "Unpaid",
  purchasedAt: "2026-11-27T09:00:00Z",
  startDate: null,
  endDate: null,
  lastDay: null,
  payment: null,
};

describe("RecordPaymentModal", () => {
  beforeEach(() => {
    vi.mocked(recordPayment).mockReset();
  });

  it("sends exactly the price and the chosen method", async () => {
    const onPaid = vi.fn();
    const paid: Membership = { ...unpaid, status: "Active", startDate: today };
    vi.mocked(recordPayment).mockResolvedValue({ ok: true, data: paid });
    renderPage(
      <RecordPaymentModal
        membership={unpaid}
        validThrough={null}
        onClose={vi.fn()}
        onPaid={onPaid}
      />,
    );

    expect(screen.getByText("The membership starts today.")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Card"));
    fireEvent.click(screen.getByRole("button", { name: /^Received BAM\s180\.00$/ }));

    await waitFor(() => expect(onPaid).toHaveBeenCalledWith(paid));
    expect(recordPayment).toHaveBeenCalledWith(9, { amount: 180, method: "Card" });
  });

  it("previews the renewal start (BR05) when the member is still covered", () => {
    const lastDay = addDays(today, 10);
    renderPage(
      <RecordPaymentModal
        membership={unpaid}
        validThrough={lastDay}
        onClose={vi.fn()}
        onPaid={vi.fn()}
      />,
    );

    const start = formatDate(addDays(today, 11), "en");
    expect(screen.getByText(`Renewal: the membership starts on ${start}.`)).toBeInTheDocument();
  });

  it("shows the API's refusal (e.g. already paid at the other desk)", async () => {
    vi.mocked(recordPayment).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "memberships.already_paid" },
    });
    renderPage(<RecordPaymentModal membership={unpaid} onClose={vi.fn()} onPaid={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: /^Received/ }));

    expect(await screen.findByText("This membership has already been paid.")).toBeInTheDocument();
  });
});
