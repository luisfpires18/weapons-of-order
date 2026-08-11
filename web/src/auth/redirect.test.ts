import { describe, expect, it } from "vitest";
import { loginPathFor, safeRedirectTarget, WORLD_PATH } from "@/auth/redirect";

describe("safeRedirectTarget", () => {
  it("accepts a path inside the application", () => {
    expect(safeRedirectTarget(WORLD_PATH)).toBe("/world");
    expect(safeRedirectTarget("/world?from=title")).toBe("/world?from=title");
  });

  it.each([
    ["an absolute url", "https://evil.example/steal"],
    ["a protocol-relative url", "//evil.example/steal"],
    ["a backslash-normalised url", "/\\evil.example"],
    ["a bare host", "evil.example"],
    ["nothing", null],
    ["an empty string", ""],
  ])("refuses %s", (_label, value) => {
    expect(safeRedirectTarget(value)).toBeNull();
  });
});

describe("loginPathFor", () => {
  it("encodes the destination so a query string survives the round trip", () => {
    expect(loginPathFor("/world?from=title")).toBe("/login?next=%2Fworld%3Ffrom%3Dtitle");
  });
});
