import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderPage } from "../../test/render";
import { EmployeeFormModal } from "./EmployeeFormModal";
import { createEmployee, updateEmployee, type Employee } from "./employeesApi";

vi.mock("./employeesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./employeesApi")>()),
  createEmployee: vi.fn(),
  updateEmployee: vi.fn(),
}));

const emir: Employee = {
  id: 7,
  firstName: "Emir",
  lastName: "Kovač",
  email: "emir@coworking.ba",
  role: "Receptionist",
  isActive: true,
};

function fillNewEmployee() {
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: "Lejla" } });
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: "Begić" } });
  fireEvent.change(screen.getByLabelText(/e-mail/i), { target: { value: "lejla@coworking.ba" } });
  fireEvent.change(screen.getByLabelText(/initial password/i), {
    target: { value: "Coworking123" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Create" }));
}

describe("EmployeeFormModal", () => {
  beforeEach(() => {
    vi.mocked(createEmployee).mockReset();
    vi.mocked(updateEmployee).mockReset();
  });

  it("creates a receptionist by default", async () => {
    const onSaved = vi.fn();
    vi.mocked(createEmployee).mockResolvedValue({ ok: true, data: { ...emir, id: 8 } });
    renderPage(
      <EmployeeFormModal
        opened
        employee={null}
        isSelf={false}
        onClose={vi.fn()}
        onSaved={onSaved}
      />,
    );

    fillNewEmployee();

    await vi.waitFor(() =>
      expect(createEmployee).toHaveBeenCalledWith({
        firstName: "Lejla",
        lastName: "Begić",
        email: "lejla@coworking.ba",
        role: "Receptionist",
        password: "Coworking123",
      }),
    );
    await vi.waitFor(() => expect(onSaved).toHaveBeenCalledWith({ ...emir, id: 8 }, true));
  });

  it("shows a taken e-mail under the e-mail field", async () => {
    vi.mocked(createEmployee).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "auth.email_taken" },
    });
    renderPage(
      <EmployeeFormModal
        opened
        employee={null}
        isSelf={false}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    );

    fillNewEmployee();

    expect(
      await screen.findByText("An account with this e-mail already exists."),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/e-mail/i)).toHaveAttribute("aria-invalid", "true");
  });

  it("edits without a password field and locks the own role", () => {
    renderPage(
      <EmployeeFormModal opened employee={emir} isSelf onClose={vi.fn()} onSaved={vi.fn()} />,
    );

    expect(screen.getByLabelText(/first name/i)).toHaveValue("Emir");
    expect(screen.queryByLabelText(/password/i)).not.toBeInTheDocument();
    expect(screen.getByText("You can't change your own role.")).toBeInTheDocument();
  });
});
