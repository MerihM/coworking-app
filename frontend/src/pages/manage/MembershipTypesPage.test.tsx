import { fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  listMembershipTypes,
  type MembershipType,
  type MembershipTypePage,
} from "../../features/membershipTypes/membershipTypesApi";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { MembershipTypesPage } from "./MembershipTypesPage";

vi.mock("../../features/membershipTypes/membershipTypesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/membershipTypes/membershipTypesApi")>()),
  listMembershipTypes: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const daily: MembershipType = {
  id: 1,
  name: "Daily",
  durationDays: 1,
  price: 15,
  isActive: true,
  membershipCount: 0,
};
const monthly: MembershipType = {
  id: 3,
  name: "Monthly",
  durationDays: 30,
  price: 175.5,
  isActive: true,
  membershipCount: 12,
};

function page(items: MembershipType[]): MembershipTypePage {
  return { items, page: 1, pageSize: 10, totalCount: items.length };
}

const managerAuth = fakeAuth({
  status: "authenticated",
  user: testUser({ id: 2, userType: "employee", role: "Manager", fullName: "Lejla Begić" }),
});

/** The table row of a type (texts like "Active" also appear in the filters). */
async function rowOf(name: string) {
  return (await screen.findByText(name)).closest("tr")!;
}

describe("MembershipTypesPage", () => {
  beforeEach(() => {
    vi.mocked(listMembershipTypes)
      .mockReset()
      .mockResolvedValue({ ok: true, data: page([daily, monthly]) });
  });

  it("shows duration in words and the price in the UI language", async () => {
    renderPage(<MembershipTypesPage />, managerAuth);

    const row = await rowOf("Monthly");
    expect(within(row).getByText("30 days")).toBeInTheDocument();
    // Intl uses a no-break space between currency and amount.
    expect(within(row).getByText(/^BAM\s175\.50$/)).toBeInTheDocument();
    expect(within(row).getByText("12")).toBeInTheDocument();
    expect(within(await rowOf("Daily")).getByText("1 day")).toBeInTheDocument();
  });

  it("only offers delete for a type nobody has bought", async () => {
    renderPage(<MembershipTypesPage />, managerAuth);

    fireEvent.click(await screen.findByRole("button", { name: "Actions for Monthly" }));
    expect(await screen.findByRole("menuitem", { name: "Delete" })).toBeDisabled();
    expect(screen.getByText(/already been bought/)).toBeInTheDocument();
  });

  it("sends the status filter to the API and starts at page 1", async () => {
    renderPage(<MembershipTypesPage />, managerAuth);
    await screen.findByText("Monthly");

    fireEvent.click(screen.getByRole("radio", { name: "Inactive" }));

    await vi.waitFor(() =>
      expect(listMembershipTypes).toHaveBeenLastCalledWith(
        expect.objectContaining({ Page: 1, IsActive: false }),
      ),
    );
  });
});
