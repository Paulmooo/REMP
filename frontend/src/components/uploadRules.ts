export const MiB = 1024 * 1024;
export const uploadTypes = [
  { value: 1, label: "Picture", accept: ".jpg,.jpeg,.png,.webp,.gif", limit: 20 },
  { value: 2, label: "Video", accept: ".mp4,.webm", limit: 100 },
  { value: 3, label: "Floor plan", accept: ".jpg,.jpeg,.png,.webp,.gif,.pdf", limit: 20 },
  { value: 4, label: "VR tour", accept: "", limit: 0 },
];
export async function validateUpload(files: File[], type: number): Promise<string | null> {
  const rule = uploadTypes.find(t => t.value === type);
  if (!rule || type === 4) return "VR uploads are unavailable until the file format is agreed.";
  if (!files.length) return "Choose at least one file.";
  if (type !== 1 && files.length !== 1) return "Choose only one file for this media type.";
  if (files.length > 20 || files.reduce((sum, f) => sum + f.size, 0) > 100 * MiB)
    return "Choose at most 20 pictures and 100 MiB per batch.";
  for (const file of files) {
    const ext = "." + file.name.split(".").pop()?.toLowerCase();
    if (!rule.accept.split(",").includes(ext)) return `${file.name}: file type is not allowed.`;
    if (!file.size || file.size > rule.limit * MiB) return `${file.name}: file must be non-empty and at most ${rule.limit} MiB.`;
    const h = new Uint8Array(await file.slice(0, 12).arrayBuffer());
    const starts = (values: number[]) => values.every((v, i) => h[i] === v);
    const text = (start: number, end: number) => String.fromCharCode(...h.slice(start, end));
    const valid = ext === ".jpg" || ext === ".jpeg" ? starts([255,216,255])
      : ext === ".png" ? starts([137,80,78,71,13,10,26,10])
      : ext === ".gif" ? ["GIF87a", "GIF89a"].includes(text(0,6))
      : ext === ".webp" ? h.length >= 12 && text(0,4) === "RIFF" && text(8,12) === "WEBP"
      : ext === ".pdf" ? text(0,5) === "%PDF-"
      : ext === ".mp4" ? h.length >= 12 && text(4,8) === "ftyp"
      : ext === ".webm" && starts([26,69,223,163]);
    if (!valid) return `${file.name}: file contents do not match the extension.`;
  }
  return null;
}
