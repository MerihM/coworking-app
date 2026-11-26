import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { fakeAuth, renderPage } from "../test/render";
import { LoginPage } from "./LoginPage";

function fillAndSubmit(email: string, password: string) {
  fireEvent.change(screen.getByLabelText(/e-mail/i), { target: { value: email } });
  fireEvent.change(screen.getByLabelText(/^password/i), { target: { value: password } });
  fireEvent.click(screen.getByRole("button", { name: "Log in" }));
}

describe("LoginPage", () => {
  it("validates in the browser before calling the API", async () => {
    const login = vi.fn();
    renderPage(<LoginPage />, fakeAuth({ login }));

    fireEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findAllByText("This field is required.")).toHaveLength(2);
    expect(login).not.toHaveBeenCalled();
  });

  it("sends the trimmed e-mail and the password", async () => {
    const login = vi.fn(async () => ({ ok: true as const }));
    renderPage(<LoginPage />, fakeAuth({ login }));

    fillAndSubmit("  amra@example.com ", "Coworking123");

    await vi.waitFor(() =>
      expect(login).toHaveBeenCalledWith({ email: "amra@example.com", password: "Coworking123" }),
    );
  });

  it("shows the translated message for a wrong password", async () => {
    const login = vi.fn(async () => ({
      ok: false as const,
      problem: { status: 401, code: "auth.invalid_credentials" },
    }));
    renderPage(<LoginPage />, fakeAuth({ login }));

    fillAndSubmit("amra@example.com", "wrong");

    expect(await screen.findByRole("alert")).toHaveTextContent("Wrong e-mail or password.");
  });

  it("keeps ?from= on the link to the registration page", () => {
    renderPage(<LoginPage />, fakeAuth(), "/login?from=%2Faccount");

    expect(screen.getByRole("link", { name: "Register" })).toHaveAttribute(
      "href",
      "/register?from=%2Faccount",
    );
  });
});
