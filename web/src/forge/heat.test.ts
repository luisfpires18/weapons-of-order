import { describe, expect, it } from "vitest";
import type { ForgeTuning } from "@/forge/heat";
import {
  bandFor,
  glowStrength,
  ironColour,
  projectBurnSeconds,
  projectTemperature,
  scalePosition,
} from "@/forge/heat";

/** The values the API publishes, so these assertions describe the real forge. */
const tuning: ForgeTuning = {
  maxTemperature: 100,
  workableFrom: 40,
  idealFrom: 65,
  burningFrom: 85,
  heatRatePerSecond: 30,
  coolRatePerSecond: 18,
  burnGraceSeconds: 3,
  strikeCooldownSeconds: 0.35,
};

describe("temperature projection", () => {
  it("rises at the published rate while the iron is in the fire", () => {
    expect(projectTemperature(0, true, 2, tuning)).toBe(60);
  });

  it("falls at the published rate once it is out", () => {
    expect(projectTemperature(60, false, 2, tuning)).toBe(24);
  });

  it("stops at both ends of the scale", () => {
    expect(projectTemperature(0, true, 60, tuning)).toBe(100);
    expect(projectTemperature(30, false, 60, tuning)).toBe(0);
  });

  it("does not move backwards for a response that arrives out of order", () => {
    expect(projectTemperature(42, true, -3, tuning)).toBe(42);
  });
});

/**
 * These have to agree with the server's own arithmetic, because the gauge is what a player
 * times a blow against. The expected values are the same ones asserted in ForgeRulesTests.
 */
describe("burn projection", () => {
  it("only counts time spent at or above the burning boundary", () => {
    expect(projectBurnSeconds(0, 0, true, 4, tuning)).toBeCloseTo(4 - 85 / 30, 6);
  });

  it("counts nothing while the iron is below it", () => {
    expect(projectBurnSeconds(0, 40, true, 1, tuning)).toBe(0);
    expect(projectBurnSeconds(0, 84, false, 10, tuning)).toBe(0);
  });

  it("stops counting once the iron cools out of the band", () => {
    expect(projectBurnSeconds(0, 94, false, 10, tuning)).toBeCloseTo(0.5, 6);
  });

  it("carries what has already burned, because the damage is cumulative", () => {
    expect(projectBurnSeconds(2.5, 90, true, 0.6, tuning)).toBeCloseTo(3.1, 6);
  });
});

describe("bands", () => {
  it.each([
    [0, "cold"],
    [39.9, "cold"],
    [40, "workable"],
    [64.9, "workable"],
    [65, "ideal"],
    [84.9, "ideal"],
    [85, "burning"],
    [100, "burning"],
  ] as const)("reads %s as %s", (temperature, expected) => {
    expect(bandFor(temperature, tuning)).toBe(expected);
  });
});

describe("appearance", () => {
  it("keeps the colour identity in the theme and only computes the ratio", () => {
    const cold = ironColour(0, tuning);
    expect(cold.from).toBe("var(--color-slate)");
    expect(cold.mix).toBe(0);

    const ideal = ironColour(65, tuning);
    expect(ideal.to).toBe("var(--color-ember)");
    expect(ideal.mix).toBe(100);
  });

  it("places a temperature along the gauge as a percentage of the scale", () => {
    expect(scalePosition(0, tuning)).toBe(0);
    expect(scalePosition(65, tuning)).toBe(65);
    expect(scalePosition(140, tuning)).toBe(100);
  });

  it("does not light cold iron", () => {
    expect(glowStrength(0, tuning)).toBe(0);
    expect(glowStrength(100, tuning)).toBe(1);
  });
});
