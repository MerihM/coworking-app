import { notifications } from "@mantine/notifications";
import { fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  deleteEmployee,
  listEmployees,
  type Employee,
  type EmployeePage,
} from "../../features/employees/employeesApi";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { StaffListPage } from "./StaffListPage";

vi.mock("../../features/employees/employeesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/employees/employeesApi")>()),
  listEmployees: vi.fn(),
  deleteEmployee: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const admin: Employee = {
  id: 1,
  firstName: "Admin",
  lastName: "Coworking",
  email: "admin@coworking.ba",
  role: "Administrator",
  isActive: true,
};
const emir: Employee = {
  id: 7,
  firstName: "Emir",
  lastName: "Kovač",
  email: "emir@coworking.ba",
  role: "Receptionist",
  isActive: true,
};

function page(items: Employee[]): EmployeePage {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 } as EmployeePage;
}

/** Logged in as the administrator from the first row. */
const adminAuth = fakeAuth({
  status: "authenticated",
  user: testUser({
    id: 1,
    userType: "employee",
    role: "Administrator",
    email: admin.email,
    fullName: "Admin Coworking",
  }),
});

async function openMenuFor(name: string) {
  fireEvent.click(await screen.findByRole("button", { name: `Actions for ${name}` }));
}

describe("StaffListPage", () => {
  beforeEach(() => {
    vi.mocked(listEmployees)
      .mockReset()
      .mockResolvedValue({ ok: true, data: page([admin, emir]) });
    vi.mocked(deleteEmployee).mockReset();
    vi.mocked(notifications.show).mockReset();
  });

  it("lists the employees and marks the own account", async () => {
    renderPage(<StaffListPage />, adminAuth);

    const ownRow = (await screen.findByText("Admin Coworking")).closest("tr")!;
    expect(within(ownRow).getByText("You")).toBeInTheDocument();
    expect(screen.getByText("emir@coworking.ba")).toBeInTheDocument();
    expect(screen.getByText("1–2 of 2")).toBeInTheDocument();
  });

  it("doesn't offer deactivate or delete for the own account", async () => {
    renderPage(<StaffListPage />, adminAuth);

    await openMenuFor("Admin Coworking");

    expect(await screen.findByRole("menuitem", { name: "Deactivate" })).toBeDisabled();
    expect(screen.getByRole("menuitem", { name: "Delete" })).toBeDisabled();
  });

  it("sends the search text to the API (after a short pause)", async () => {
    renderPage(<StaffListPage />, adminAuth);
    await screen.findByText("Admin Coworking");

    fireEvent.change(screen.getByLabelText("Search"), { target: { value: " emir " } });

    await vi.waitFor(() =>
      expect(listEmployees).toHaveBeenLastCalledWith(
        expect.objectContaining({ Page: 1, Search: "emir" }),
      ),
    );
  });

  it("explains why an employee that is still in use can't be deleted", async () => {
    vi.mocked(deleteEmployee).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "conflict.in_use" },
    });
    renderPage(<StaffListPage />, adminAuth);

    await openMenuFor("Emir Kovač");
    fireEvent.click(await screen.findByRole("menuitem", { name: "Delete" }));
    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    await vi.waitFor(() =>
      expect(notifications.show).toHaveBeenCalledWith(
        expect.objectContaining({
          color: "red",
          message:
            "This record is still used by other data and can't be deleted. Deactivate it instead.",
        }),
      ),
    );
    expect(deleteEmployee).toHaveBeenCalledWith(7);
  });
});
