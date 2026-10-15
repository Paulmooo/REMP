import { describe, it, expect } from "vitest";
import { MiB, validateUpload } from "./uploadRules";
// File header reads are represented directly; browser tests exercise real Files.
const file = (name: string, size = 12, bytes = [255, 216, 255]) => ({ name, size, slice: () => ({ arrayBuffer: async () => new Uint8Array(bytes).buffer }) }) as File;
describe("media upload batch validation", () => {
  it("accepts multiple pictures and rejects non-picture batches", async () => {
    expect(await validateUpload([file("a.jpg"), file("b.JPG")], 1)).toBeNull();
    for (const type of [2, 3]) expect(await validateUpload([file("a.jpg"), file("b.jpg")], type)).toContain("only one");
  });
  it("enforces file, batch and count limits before reading files", async () => {
    expect(await validateUpload([file("a.jpg", 20 * MiB + 1)], 1)).toContain("20 MiB");
    expect(await validateUpload([file("a.mp4", 100 * MiB + 1)], 2)).toContain("100 MiB");
    expect(await validateUpload(Array.from({ length: 21 }, () => file("a.jpg")), 1)).toContain("20 pictures");
    expect(await validateUpload(Array.from({ length: 6 }, () => file("a.jpg", 20 * MiB)), 1)).toContain("100 MiB");
    expect(await validateUpload([file("a.jpg", 0)], 1)).toContain("non-empty");
  });
  it("rejects invalid extensions, forged headers, empty input and undefined VR format", async () => {
    expect(await validateUpload([file("a.exe")], 1)).toContain("not allowed");
    expect(await validateUpload([file("a.png")], 1)).toContain("do not match");
    expect(await validateUpload([], 1)).toContain("at least one");
    expect(await validateUpload([file("a.zip")], 4)).toContain("format is agreed");
  });
  it("accepts agreed video, image and PDF file headers", async () => {
    for (const [name, type, bytes] of [
      ["a.png", 1, [137,80,78,71,13,10,26,10]],
      ["a.gif", 1, [...new TextEncoder().encode("GIF89a")]],
      ["a.webp", 3, [...new TextEncoder().encode("RIFF0000WEBP")]],
      ["a.pdf", 3, [...new TextEncoder().encode("%PDF-")]],
      ["a.mp4", 2, [...new TextEncoder().encode("0000ftypisom")]],
      ["a.webm", 2, [26,69,223,163]],
    ] as [string, number, number[]][]) expect(await validateUpload([file(name, 12, bytes)], type)).toBeNull();
  });
});
