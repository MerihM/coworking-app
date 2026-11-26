import { fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getRoom, listRooms, type Room } from "../../features/rooms/roomsApi";
import {
  listWorkspaces,
  type Workspace,
  type WorkspacePage,
} from "../../features/workspaces/workspacesApi";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { RoomDetailPage } from "./RoomDetailPage";

vi.mock("../../features/rooms/roomsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/rooms/roomsApi")>()),
  getRoom: vi.fn(),
  listRooms: vi.fn(),
}));

vi.mock("../../features/workspaces/workspacesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/workspaces/workspacesApi")>()),
  listWorkspaces: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const openSpace: Room = {
  id: 1,
  name: "Open Space",
  floor: 1,
  description: "Bright open-plan area.",
  isActive: true,
  workspaceCount: 2,
  activeWorkspaceCount: 2,
};

function workspace(overrides: Partial<Workspace>): Workspace {
  return {
    id: 1,
    code: "OS-01",
    type: "HotDesk",
    capacity: 1,
    isActive: true,
    roomId: 1,
    roomName: "Open Space",
    roomIsActive: true,
    isAssigned: false,
    ...overrides,
  };
}

function page(items: Workspace[]): WorkspacePage {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

const managerAuth = fakeAuth({
  status: "authenticated",
  user: testUser({ id: 2, userType: "employee", role: "Manager", fullName: "Lejla Begić" }),
});

function renderRoom(id = 1) {
  renderPage(<RoomDetailPage />, managerAuth, `/manage/rooms/${id}`, "/manage/rooms/:roomId");
}

describe("RoomDetailPage", () => {
  beforeEach(() => {
    vi.mocked(getRoom).mockReset().mockResolvedValue({ ok: true, data: openSpace });
    vi.mocked(listRooms)
      .mockReset()
      .mockResolvedValue({
        ok: true,
        data: { items: [openSpace], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 },
      });
    vi.mocked(listWorkspaces)
      .mockReset()
      .mockResolvedValue({
        ok: true,
        data: page([
          workspace({}),
          workspace({ id: 30, code: "OS-30", type: "FixedDesk", isAssigned: true }),
        ]),
      });
  });

  it("shows the room and loads only its workspaces", async () => {
    renderRoom();

    expect(await screen.findByRole("heading", { name: "Open Space" })).toBeInTheDocument();
    expect(screen.getByText("Bright open-plan area.")).toBeInTheDocument();
    // Look inside the row: the type filter's (hidden) options also contain "Fixed desk".
    const deskRow = (await screen.findByText("OS-30")).closest("tr")!;
    expect(within(deskRow).getByText("Fixed desk")).toBeInTheDocument();
    expect(within(deskRow).getByText("Assigned")).toBeInTheDocument();
    expect(getRoom).toHaveBeenCalledWith(1);
    expect(listWorkspaces).toHaveBeenCalledWith(expect.objectContaining({ RoomId: 1, Page: 1 }));
  });

  it("protects an assigned fixed desk from deactivation and deletion", async () => {
    renderRoom();

    fireEvent.click(await screen.findByRole("button", { name: "Actions for OS-30" }));

    expect(await screen.findByRole("menuitem", { name: "Deactivate" })).toBeDisabled();
    expect(screen.getByRole("menuitem", { name: "Delete" })).toBeDisabled();
  });

  it("warns about a deactivated room and blocks adding workspaces", async () => {
    vi.mocked(getRoom).mockResolvedValue({ ok: true, data: { ...openSpace, isActive: false } });
    renderRoom();

    expect(await screen.findByText("This room is deactivated")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add workspace" })).toBeDisabled();
  });

  it("filters the workspaces by type", async () => {
    renderRoom();
    await screen.findByText("OS-30");

    fireEvent.click(screen.getByRole("combobox", { name: "Type" })); // Mantine Select = combobox
    fireEvent.click(screen.getByRole("option", { name: "Meeting room" }));

    await vi.waitFor(() =>
      expect(listWorkspaces).toHaveBeenLastCalledWith(
        expect.objectContaining({ RoomId: 1, Type: "MeetingRoom", Page: 1 }),
      ),
    );
  });

  it("explains a room that doesn't exist", async () => {
    vi.mocked(getRoom).mockResolvedValue({ ok: false, problem: { status: 404 } });
    renderRoom(999);

    expect(await screen.findByText("This room doesn't exist (any more).")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /All rooms/ })).toHaveAttribute(
      "href",
      "/manage/rooms",
    );
  });
});
