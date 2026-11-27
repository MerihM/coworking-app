import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { listMembers, type Member, type MemberPage } from "../../features/members/membersApi";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { MembersListPage } from "./MembersListPage";

vi.mock("../../features/members/membersApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/members/membersApi")>()),
  listMembers: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const amra: Member = {
  id: 7,
  firstName: "Amra",
  lastName: "Hodžić",
  email: "amra@example.com",
  phone: "+387 61 123 456",
  isActive: true,
  registeredAt: "2026-11-27T09:00:00Z",
  fixedDesk: null,
  currentMembership: null,
  validThrough: "2026-12-26",
  hasUnpaidMembership: true,
};

function page(items: Member[]): MemberPage {
  return { items, page: 1, pageSize: 10, totalCount: items.length };
}

const receptionAuth = fakeAuth({
  status: "authenticated",
  user: testUser({ id: 3, userType: "employee", role: "Receptionist", fullName: "Emir Kovač" }),
});

describe("MembersListPage", () => {
  beforeEach(() => {
    vi.mocked(listMembers)
      .mockReset()
      .mockResolvedValue({ ok: true, data: page([amra]) });
  });

  it("shows each member with the membership summary and a link to their page", async () => {
    renderPage(<MembersListPage />, receptionAuth);

    const link = await screen.findByRole("link", { name: "Amra Hodžić" });
    expect(link).toHaveAttribute("href", "/reception/members/7");
    expect(screen.getByText("Valid until 26 Dec 2026")).toBeInTheDocument();
    expect(screen.getByText("Unpaid")).toBeInTheDocument();
  });

  it("starts with active members and asks the API for inactive ones on request", async () => {
    renderPage(<MembersListPage />, receptionAuth);
    await screen.findByText("Amra Hodžić");
    expect(listMembers).toHaveBeenLastCalledWith(expect.objectContaining({ IsActive: true }));

    fireEvent.click(screen.getByText("Inactive"));

    await waitFor(() =>
      expect(listMembers).toHaveBeenLastCalledWith(
        expect.objectContaining({ IsActive: false, Page: 1 }),
      ),
    );
  });
});
