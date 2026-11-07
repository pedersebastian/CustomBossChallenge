// Test helper: a code for a payload, made the way the mod makes one

/** Raw deflate in base64url after the prefix */
export async function encode(payload: object | string, prefix = "CBC2:"): Promise<string> {
  const json = typeof payload === "string" ? payload : JSON.stringify(payload);
  const stream = new Blob([json]).stream().pipeThrough(new CompressionStream("deflate-raw"));
  const bytes = new Uint8Array(await new Response(stream).arrayBuffer());
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return prefix + btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
