import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getMembershipOffer } from "../features/membershipTypes/membershipTypesApi";
import { testUser } from "../test/fixtures";
import { fakeAuth, renderPage } from "../test/render";
import { HomePage } from "./HomePage";

vi.mock("../features/membershipTypes/membershipTypesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../features/membershipTypes/membershipTypesApi")>()),
  getMembershipOffer: vi.fn(),
}));

// The API status badge would call /health – not part of this test.
vi.mock("../features/health/useApiHealth", () => ({ useApiHealth: () => "Healthy" }));

describe("HomePage", () => {
  beforeEach(() => {
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

  it("shows the public offer with prices and durations", async () => {
    renderPage(<HomePage />);

    expect(await screen.findByText("Monthly")).toBeInTheDocument();
    expect(screen.getByText("Daily")).toBeInTheDocument();
    expect(screen.getByText(/^BAM\s15\.00$/)).toBeInTheDocument();
    expect(screen.getByText("30 days")).toBeInTheDocument();
  });

  it("invites visitors to search and to register", async () => {
    renderPage(<HomePage />);

    expect(screen.getByRole("link", { name: "Find a free workspace" })).toHaveAttribute(
      "href",
      "/availability",
    );
    expect(screen.getByRole("link", { name: "Create an account" })).toHaveAttribute(
      "href",
      "/register",
    );
    await screen.findByText("Monthly"); // let the offer load before the test ends
  });

  it("doesn't offer registration to logged-in users", async () => {
    renderPage(<HomePage />, fakeAuth({ status: "authenticated", user: testUser() }));

    await screen.findByText("Monthly");
    expect(screen.queryByRole("link", { name: "Create an account" })).not.toBeInTheDocument();
  });

  it("says so when the offer can't be loaded", async () => {
    vi.mocked(getMembershipOffer).mockResolvedValue({ ok: false, problem: { status: 0 } });

    renderPage(<HomePage />);

    expect(await screen.findByText("The offer could not be loaded.")).toBeInTheDocument();
  });
});
