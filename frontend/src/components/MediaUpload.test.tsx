import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { MediaUpload } from "./MediaUpload";
import { request } from "../api/client";
vi.mock("../api/client", async importOriginal => ({ ...await importOriginal<typeof import("../api/client")>(), request: vi.fn() }));
vi.mock("./uploadRules", async importOriginal => ({ ...await importOriginal<typeof import("./uploadRules")>(), validateUpload: vi.fn().mockResolvedValue(null) }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });
it("100% transport waits for server acknowledgement and suppresses duplicate writes", async () => {
  vi.stubGlobal("URL", Object.assign(URL, { createObjectURL: vi.fn(() => "blob:preview"), revokeObjectURL: vi.fn() }));
  let confirm!: () => void;
  const refresh = vi.fn().mockResolvedValue(undefined);
  vi.mocked(request).mockImplementation((_url, options) => {
    options?.onUploadProgress?.({ loaded: 100, total: 100, bytes: 100, lengthComputable: true });
    return new Promise(resolve => { confirm = () => resolve([]); });
  });
  render(<MediaUpload listingId="42" refresh={refresh} />);
  fireEvent.change(screen.getByLabelText("Choose files or drag and drop here"), { target: { files: [new File(["test"], "photo.jpg")] } });
  const button = await screen.findByRole("button", { name: "Upload selected files" });
  await waitFor(() => expect((button as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(button);
  expect(screen.getByText("100% transferred. Waiting for server confirmation…")).toBeTruthy();
  expect(screen.queryByText("Media uploaded")).toBeNull(); expect(refresh).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Uploading…" })); expect(request).toHaveBeenCalledTimes(1);
  await act(async () => confirm());
  expect(refresh).toHaveBeenCalledOnce(); expect(screen.getByText("Media uploaded")).toBeTruthy();
  expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");
});
