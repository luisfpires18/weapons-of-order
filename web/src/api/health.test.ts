import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchHealth, HEALTH_URL } from "@/api/health";

function stubFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchHealth", () => {
  it("requests the same-origin health path", async () => {
    const fetchMock = stubFetch(
      jsonResponse({ service: "weapons-of-order-api", status: "Healthy", checks: {} }),
    );

    await fetchHealth();

    expect(fetchMock).toHaveBeenCalledWith(HEALTH_URL, expect.anything());
    expect(HEALTH_URL.startsWith("/api/")).toBe(true);
  });

  it("returns the parsed payload", async () => {
    stubFetch(
      jsonResponse({
        service: "weapons-of-order-api",
        status: "Healthy",
        checks: { database: "Healthy" },
      }),
    );

    await expect(fetchHealth()).resolves.toEqual({
      service: "weapons-of-order-api",
      status: "Healthy",
      checks: { database: "Healthy" },
    });
  });

  it("rejects a non-ok response instead of returning a partial result", async () => {
    stubFetch(jsonResponse({ service: "weapons-of-order-api", status: "Unhealthy" }, 503));

    await expect(fetchHealth()).rejects.toThrow("Health request failed: 503");
  });

  it("rejects a payload that does not match the contract", async () => {
    stubFetch(jsonResponse({ service: "weapons-of-order-api" }));

    await expect(fetchHealth()).rejects.toThrow();
  });
});
