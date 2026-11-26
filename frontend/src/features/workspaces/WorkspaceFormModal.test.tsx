import { fireEvent, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderPage } from "../../test/render";
import { WorkspaceFormModal } from "./WorkspaceFormModal";
import { createWorkspace, type Workspace } from "./workspacesApi";

vi.mock("./workspacesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./workspacesApi")>()),
  createWorkspace: vi.fn(),
  updateWorkspace: vi.fn(),
}));

const assignedDesk: Workspace = {
  id: 30,
  code: "OS-30",
  type: "FixedDesk",
  capacity: 1,
  isActive: true,
  roomId: 1,
  roomName: "Open Space",
  roomIsActive: true,
  isAssigned: true,
};

function renderCreate() {
  renderPage(
    <WorkspaceFormModal
      opened
      workspace={null}
      roomId={1}
      rooms={[{ id: 1, name: "Open Space" }]}
      onClose={vi.fn()}
      onSaved={vi.fn()}
    />,
  );
}

/** Mantine Select (role "combobox", not "textbox"): open the dropdown, then click the option. */
function chooseType(label: string) {
  fireEvent.click(screen.getByRole("combobox", { name: /^type/i }));
  fireEvent.click(screen.getByRole("option", { name: label }));
}

describe("WorkspaceFormModal", () => {
  beforeEach(() => {
    vi.mocked(createWorkspace).mockReset();
  });

  it("locks the capacity to 1 for a desk", () => {
    renderCreate();

    const capacity = screen.getByRole("textbox", { name: /capacity/i });
    expect(capacity).toBeDisabled();
    expect(capacity).toHaveValue("1");
    expect(screen.getByText("Desks are always for one person.")).toBeInTheDocument();
  });

  it("raises the capacity to the minimum when switching to a meeting room", () => {
    renderCreate();

    chooseType("Meeting room");

    const capacity = screen.getByRole("textbox", { name: /capacity/i });
    expect(capacity).toBeEnabled();
    expect(capacity).toHaveValue("2");
    expect(screen.getByText("2–50 people")).toBeInTheDocument();
  });

  it("sends the code in upper case into the current room", async () => {
    vi.mocked(createWorkspace).mockResolvedValue({ ok: true, data: { ...assignedDesk, id: 99 } });
    renderCreate();

    fireEvent.change(screen.getByRole("textbox", { name: /code/i }), {
      target: { value: " os-31 " },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await vi.waitFor(() =>
      expect(createWorkspace).toHaveBeenCalledWith({
        roomId: 1,
        code: "OS-31",
        type: "HotDesk",
        capacity: 1,
      }),
    );
  });

  it("shows a taken code under the code field", async () => {
    vi.mocked(createWorkspace).mockResolvedValue({
      ok: false,
      problem: { status: 409, code: "workspaces.code_taken" },
    });
    renderCreate();

    fireEvent.change(screen.getByRole("textbox", { name: /code/i }), {
      target: { value: "OS-01" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    expect(
      await screen.findByText("Another workspace already uses this code."),
    ).toBeInTheDocument();
  });

  it("doesn't let an assigned fixed desk change its type", () => {
    renderPage(
      <WorkspaceFormModal
        opened
        workspace={assignedDesk}
        roomId={1}
        rooms={[{ id: 1, name: "Open Space" }]}
        onClose={vi.fn()}
        onSaved={vi.fn()}
      />,
    );

    expect(screen.getByRole("combobox", { name: /^type/i })).toBeDisabled();
    expect(
      screen.getByText("Assigned to a member – the type can't be changed."),
    ).toBeInTheDocument();
  });
});
