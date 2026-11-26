import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { fakeAuth, renderPage } from "../test/render";
import { RegisterPage } from "./RegisterPage";

function fill(label: RegExp, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

function fillValidForm() {
  fill(/first name/i, "Amra");
  fill(/last name/i, "Hodžić");
  fill(/e-mail/i, "amra@example.com");
  fill(/^phone/i, "+387 61 123 456");
  fill(/^password/i, "Coworking123");
  fill(/repeat password/i, "Coworking123");
}

describe("RegisterPage", () => {
  it("shows the password rules before sending anything", async () => {
    const register = vi.fn();
    renderPage(<RegisterPage />, fakeAuth({ register }));

    fillValidForm();
    fill(/^password/i, "abcdefgh");
    fill(/repeat password/i, "abcdefgh");
    fireEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(
      await screen.findByText("The password must contain at least one digit."),
    ).toBeInTheDocument();
    expect(register).not.toHaveBeenCalled();
  });

  it("sends the registration without the repeated password", async () => {
    const register = vi.fn(async () => ({ ok: true as const }));
    renderPage(<RegisterPage />, fakeAuth({ register }));

    fillValidForm();
    fireEvent.click(screen.getByRole("button", { name: "Register" }));

    await vi.waitFor(() =>
      expect(register).toHaveBeenCalledWith({
        firstName: "Amra",
        lastName: "Hodžić",
        email: "amra@example.com",
        phone: "+387 61 123 456",
        password: "Coworking123",
      }),
    );
  });

  it("shows 'e-mail taken' under the e-mail field", async () => {
    const register = vi.fn(async () => ({
      ok: false as const,
      problem: { status: 409, code: "auth.email_taken" },
    }));
    renderPage(<RegisterPage />, fakeAuth({ register }));

    fillValidForm();
    fireEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(
      await screen.findByText("An account with this e-mail already exists."),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/e-mail/i)).toHaveAttribute("aria-invalid", "true");
  });

  it("puts server validation messages under the matching fields", async () => {
    const register = vi.fn(async () => ({
      ok: false as const,
      problem: { status: 400, errors: { phone: ["Broj telefona nije ispravan."] } },
    }));
    renderPage(<RegisterPage />, fakeAuth({ register }));

    fillValidForm();
    fireEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(await screen.findByText("Broj telefona nije ispravan.")).toBeInTheDocument();
    expect(screen.getByLabelText(/^phone/i)).toHaveAttribute("aria-invalid", "true");
  });
});
