import { describe, expect, it } from "vitest";
import { loginPathFor, RETURN_PARAM, safeRedirectTarget, WORLD_PATH } from "@/auth/redirect";

describe("safeRedirectTarget", () => {
  it.each([
    ["the world path", WORLD_PATH, "/world"],
    ["a path with a query string", "/world?from=title", "/world?from=title"],
    ["a path with a fragment", "/world#anvil", "/world#anvil"],
    ["a nested path", "/world/forge", "/world/forge"],
    // Percent-encoding inside a segment is ordinary data, not a separator.
    ["an encoded character inside a segment", "/world/a%20b", "/world/a%20b"],
  ])("accepts %s", (_label, value, expected) => {
    expect(safeRedirectTarget(value)).toBe(expected);
  });

  it.each([
    ["nothing", null],
    ["undefined", undefined],
    ["an empty string", ""],
  ])("refuses %s", (_label, value) => {
    expect(safeRedirectTarget(value)).toBeNull();
  });

  it.each([
    ["a javascript url", "javascript:alert(1)"],
    ["a cased javascript url", "JavaScript:alert(1)"],
    ["a javascript url with padding", " javascript:alert(1)"],
    ["a data url", "data:text/html,<script>alert(1)</script>"],
    ["an absolute http url", "http://evil.example/steal"],
    ["an absolute https url", "https://evil.example/steal"],
    ["a bare host", "evil.example"],
  ])("refuses %s", (_label, value) => {
    expect(safeRedirectTarget(value)).toBeNull();
  });

  it.each([
    ["a protocol-relative url", "//evil.example"],
    ["a protocol-relative url with a path", "//evil.example/steal"],
    // The router and the browser both resolve these to the same place as `//`.
    ["an encoded protocol-relative url", "/%2Fevil.example"],
    ["an upper-cased encoded protocol-relative url", "/%2fevil.example"],
    ["a fully encoded protocol-relative url", "%2F%2Fevil.example"],
    ["a triple slash", "///evil.example"],
  ])("refuses %s", (_label, value) => {
    expect(safeRedirectTarget(value)).toBeNull();
  });

  it.each([
    ["a backslash-normalised url", "/\\evil.example"],
    ["a double backslash", "\\\\evil.example"],
    ["an encoded backslash", "/%5Cevil.example"],
    ["a lower-cased encoded backslash", "/%5cevil.example"],
    ["a fully encoded backslash pair", "%5C%5Cevil.example"],
    ["a mixed slash and encoded backslash", "/%5C/evil.example"],
  ])("refuses %s", (_label, value) => {
    expect(safeRedirectTarget(value)).toBeNull();
  });

  it.each([
    // Browsers strip tabs and newlines out of URLs, which turns these back into `//`.
    ["an embedded tab", "/\t/evil.example"],
    ["an embedded newline", "/\n/evil.example"],
    ["an embedded carriage return", "/\r/evil.example"],
    ["an embedded null", "/\u0000/evil.example"],
  ])("refuses %s", (_label, value) => {
    expect(safeRedirectTarget(value)).toBeNull();
  });
});

describe("loginPathFor", () => {
  it("encodes the destination so a query string survives the round trip", () => {
    expect(loginPathFor("/world?from=title")).toBe("/login?next=%2Fworld%3Ffrom%3Dtitle");
  });

  it("round-trips through the parameter the login screen reads", () => {
    const target = "/world?from=title";
    const parsed = new URLSearchParams(loginPathFor(target).split("?")[1]);

    expect(safeRedirectTarget(parsed.get(RETURN_PARAM))).toBe(target);
  });
});
