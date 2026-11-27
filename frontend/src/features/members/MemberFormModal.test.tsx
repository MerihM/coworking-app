import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderPage } from "../../test/render";
import { MemberFormModal } from "./MemberFormModal";
import { createMember, listFixedDesks, type Member } from "./membersApi";

vi.mock("./membersApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./membersApi")>()),
  createMember: vi.fn(),
  listFixedDesks: vi.fn(),
}));

const created: Member = {
  id: 12,
  firstName: "Lejla",
  lastName: "Begić",
  email: "lejla@example.com",
  phone: "061/222-333",
  isActive: true,
  registeredAt: "2026-11-27T09:00:00Z",
  fixedDesk: null,
  currentMembership: null,
  validThrough: null,
  hasUnpaidMembership: false,
};

function fill(phone: string) {
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: "Lejla" } });
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: "Begić" } });
  fireEvent.change(screen.getByLabelText(/e-mail/i), { target: { value: "lejla@example.com" } });
  fireEvent.change(screen.getByLabelText(/^phone/i), { target: { value: phone } });
  fireEvent.change(screen.getByLabelText(/initial password/i), {
    target: { value: "Coworking123" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Create" }));
}

describe("MemberFormModal", () => {
  beforeEach(() => {
    vi.mocked(createMember).mockReset();
    vi.mocked(listFixedDesks).mockReset().mockResolvedValue({ ok: true, data: [] });
  });

  it("creates a member without a fixed desk", async () => {
    const onSaved = vi.fn();
    vi.mocked(createMember).mockResolvedValue({ ok: true, data: created });
    renderPage(<MemberFormModal opened member={null} onClose={vi.fn()} onSaved={onSaved} />);

    fill("061/222-333");

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(created, true));
    expect(createMember).toHaveBeenCalledWith({
      firstName: "Lejla",
      lastName: "Begić",
      email: "lejla@example.com",
      phone: "061/222-333",
      password: "Coworking123",
      fixedWorkspaceId: null,
    });
  });

  it("checks the phone format before calling the API", async () => {
    renderPage(<MemberFormModal opened member={null} onClose={vi.fn()} onSaved={vi.fn()} />);

    fill("call me");

    expect(
      await screen.findByText("Only digits, spaces and + ( ) / - are allowed."),
    ).toBeInTheDocument();
    expect(createMember).not.toHaveBeenCalled();
  });

  it("shows a taken e-mail next to the e-mail field", async () => {
    vi.mocked(createMember).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "auth.email_taken" },
    });
    renderPage(<MemberFormModal opened member={null} onClose={vi.fn()} onSaved={vi.fn()} />);

    fill("061/222-333");

    expect(
      await screen.findByText("An account with this e-mail already exists."),
    ).toBeInTheDocument();
  });
});
