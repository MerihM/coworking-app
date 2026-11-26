import { notifications } from "@mantine/notifications";
import { fireEvent, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { deleteRoom, listRooms, type Room, type RoomPage } from "../../features/rooms/roomsApi";
import { testUser } from "../../test/fixtures";
import { fakeAuth, renderPage } from "../../test/render";
import { RoomsListPage } from "./RoomsListPage";

vi.mock("../../features/rooms/roomsApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../features/rooms/roomsApi")>()),
  listRooms: vi.fn(),
  deleteRoom: vi.fn(),
}));

vi.mock("@mantine/notifications", () => ({ notifications: { show: vi.fn() } }));

const openSpace: Room = {
  id: 1,
  name: "Open Space",
  floor: 1,
  description: null,
  isActive: true,
  workspaceCount: 30,
  activeWorkspaceCount: 29,
};
const storage: Room = {
  id: 9,
  name: "Old Storage",
  floor: -1,
  description: null,
  isActive: false,
  workspaceCount: 0,
  activeWorkspaceCount: 0,
};

function page(items: Room[]): RoomPage {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 };
}

const managerAuth = fakeAuth({
  status: "authenticated",
  user: testUser({ id: 2, userType: "employee", role: "Manager", fullName: "Lejla Begić" }),
});

async function openMenuFor(name: string) {
  fireEvent.click(await screen.findByRole("button", { name: `Actions for ${name}` }));
}

describe("RoomsListPage", () => {
  beforeEach(() => {
    vi.mocked(listRooms)
      .mockReset()
      .mockResolvedValue({ ok: true, data: page([openSpace, storage]) });
    vi.mocked(deleteRoom).mockReset();
    vi.mocked(notifications.show).mockReset();
  });

  it("lists rooms with their floor, workspace counts and status", async () => {
    renderPage(<RoomsListPage />, managerAuth);

    expect(await screen.findByRole("link", { name: "Open Space" })).toHaveAttribute(
      "href",
      "/manage/rooms/1",
    );
    expect(screen.getByText("29 of 30 workspaces active")).toBeInTheDocument();
    // "Inactive" is also a status filter button, so look inside the room's own row.
    const storageRow = screen.getByRole("link", { name: "Old Storage" }).closest("tr")!;
    expect(within(storageRow).getByText("-1")).toBeInTheDocument();
    expect(within(storageRow).getByText("Inactive")).toBeInTheDocument();
  });

  it("sends the search text to the API and starts at page 1", async () => {
    renderPage(<RoomsListPage />, managerAuth);
    await screen.findByText("Open Space");

    fireEvent.change(screen.getByLabelText("Search"), { target: { value: " office " } });

    await vi.waitFor(() =>
      expect(listRooms).toHaveBeenLastCalledWith(
        expect.objectContaining({ Page: 1, Search: "office" }),
      ),
    );
  });

  it("doesn't offer delete for a room that still has workspaces", async () => {
    renderPage(<RoomsListPage />, managerAuth);

    await openMenuFor("Open Space");

    expect(await screen.findByRole("menuitem", { name: "Delete" })).toBeDisabled();
    expect(screen.getByText(/Only an empty room can be deleted/)).toBeInTheDocument();
  });

  it("deletes an empty room after confirmation", async () => {
    vi.mocked(deleteRoom).mockResolvedValue({ ok: true, data: undefined });
    renderPage(<RoomsListPage />, managerAuth);

    await openMenuFor("Old Storage");
    fireEvent.click(await screen.findByRole("menuitem", { name: "Delete" }));
    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    await vi.waitFor(() => expect(deleteRoom).toHaveBeenCalledWith(9));
    expect(notifications.show).toHaveBeenCalledWith(
      expect.objectContaining({ color: "green", message: "Old Storage was deleted." }),
    );
  });
});
