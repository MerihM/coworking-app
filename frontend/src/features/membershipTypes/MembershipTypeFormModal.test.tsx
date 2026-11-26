import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderPage } from "../../test/render";
import { MembershipTypeFormModal } from "./MembershipTypeFormModal";
import { updateMembershipType, type MembershipType } from "./membershipTypesApi";

vi.mock("./membershipTypesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./membershipTypesApi")>()),
  updateMembershipType: vi.fn(),
}));

const monthly: MembershipType = {
  id: 3,
  name: "Monthly",
  durationDays: 30,
  price: 180,
  isActive: true,
  membershipCount: 12,
};

function renderForm(membershipType: MembershipType | null) {
  renderPage(
    <MembershipTypeFormModal
      opened
      membershipType={membershipType}
      onClose={vi.fn()}
      onSaved={vi.fn()}
    />,
  );
}

describe("MembershipTypeFormModal", () => {
  beforeEach(() => {
    vi.mocked(updateMembershipType).mockReset();
  });

  it("sends name, duration and price as JSON numbers", async () => {
    // Edits an existing type, so duration and price come from the form's initial values.
    // Typing a DECIMAL price into Mantine's NumberInput can't be simulated reliably with
    // fireEvent in jsdom (the number-format library tracks the caret, which jsdom doesn't move),
    // so price input is covered by the validation tests and the browser check instead.
    vi.mocked(updateMembershipType).mockResolvedValue({ ok: true, data: monthly });
    renderForm(monthly);

    fireEvent.change(screen.getByRole("textbox", { name: /name/i }), {
      target: { value: "  Monthly Plus " },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(updateMembershipType).toHaveBeenCalledWith(3, {
        name: "Monthly Plus",
        durationDays: 30,
        price: 180,
      }),
    );
  });

  it("sends a typed duration as a number", async () => {
    vi.mocked(updateMembershipType).mockResolvedValue({ ok: true, data: monthly });
    renderForm(monthly);

    fireEvent.change(screen.getByRole("textbox", { name: /duration/i }), {
      target: { value: "7" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(updateMembershipType).toHaveBeenCalledWith(
        3,
        expect.objectContaining({ durationDays: 7, price: 180 }),
      ),
    );
  });

  it("shows the duration in words while typing", () => {
    renderForm(null);

    fireEvent.change(screen.getByRole("textbox", { name: /duration/i }), {
      target: { value: "1" },
    });

    expect(screen.getByText("= 1 day")).toBeInTheDocument();
  });

  it("shows a taken name under the name field", async () => {
    vi.mocked(updateMembershipType).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "membership_types.name_taken" },
    });
    renderForm(monthly);

    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(
      await screen.findByText("Another membership type already has this name."),
    ).toBeInTheDocument();
  });

  it("tells the manager that bought memberships keep their price", () => {
    renderForm(monthly);

    expect(screen.getByText(/12 memberships were bought with this type/)).toBeInTheDocument();
  });
});
