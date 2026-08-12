/**
 * The client's copy of the forge's temperature model.
 *
 * It exists so the gauge can move between server responses instead of stepping once per
 * request. It decides nothing: the band a strike landed in, whether the iron burned through
 * and what the sword became are all the server's answers, and every one of them arrives in
 * the response to an action the player took. If this drifted from the server it would show
 * up as a gauge that lied, never as a different sword.
 *
 * The boundaries and rates are not duplicated here either — they are published by the API in
 * the tuning block, so tuning the forge in configuration moves both sides at once.
 */

export type HeatBandName = "cold" | "workable" | "ideal" | "burning";

export type ForgeTuning = {
  maxTemperature: number;
  workableFrom: number;
  idealFrom: number;
  burningFrom: number;
  heatRatePerSecond: number;
  coolRatePerSecond: number;
  burnGraceSeconds: number;
  strikeCooldownSeconds: number;
};

export const BAND_LABELS: Record<HeatBandName, string> = {
  cold: "Cold",
  workable: "Workable",
  ideal: "Ideal",
  burning: "Burning",
};

export function bandFor(temperature: number, tuning: ForgeTuning): HeatBandName {
  if (temperature >= tuning.burningFrom) return "burning";
  if (temperature >= tuning.idealFrom) return "ideal";
  if (temperature >= tuning.workableFrom) return "workable";
  return "cold";
}

/** Where the workpiece is after `seconds` of being held in the fire, or out of it. */
export function projectTemperature(
  from: number,
  heating: boolean,
  seconds: number,
  tuning: ForgeTuning,
): number {
  if (seconds <= 0) return from;

  return heating
    ? Math.min(tuning.maxTemperature, from + tuning.heatRatePerSecond * seconds)
    : Math.max(0, from - tuning.coolRatePerSecond * seconds);
}

/**
 * Burning time accumulated after `seconds`, given where the workpiece started.
 *
 * Solved rather than sampled, for the same reason the server solves it: a frame-rate-shaped
 * answer would drift away from the authoritative one and the burn warning would be wrong
 * exactly when it matters.
 */
export function projectBurnSeconds(
  burnedSoFar: number,
  from: number,
  heating: boolean,
  seconds: number,
  tuning: ForgeTuning,
): number {
  if (seconds <= 0) return burnedSoFar;

  const threshold = tuning.burningFrom;

  if (heating) {
    if (from >= threshold) return burnedSoFar + seconds;

    const untilBurning = (threshold - from) / tuning.heatRatePerSecond;
    return burnedSoFar + Math.max(0, seconds - untilBurning);
  }

  if (from <= threshold) return burnedSoFar;

  return burnedSoFar + Math.min(seconds, (from - threshold) / tuning.coolRatePerSecond);
}

/**
 * The colour of iron at a given temperature, as a mix between two theme colours.
 *
 * Only the ratio is computed here; the colours themselves stay custom properties, so the
 * workpiece is lit by the same ember the rest of the application is. The stops follow what
 * heated steel actually does — dark iron, dull red, orange, then near-white — which is also
 * what makes the bar readable at a glance before the player has learned the band names.
 */
export function ironColour(temperature: number, tuning: ForgeTuning): { from: string; to: string; mix: number } {
  const whiteHot = {
    from: "var(--color-ember-bright)",
    to: "var(--color-white-hot)",
    low: tuning.burningFrom,
    high: tuning.maxTemperature,
  };

  const segments = [
    { from: "var(--color-slate)", to: "var(--color-ember-deep)", low: 0, high: tuning.workableFrom },
    {
      from: "var(--color-ember-deep)",
      to: "var(--color-ember)",
      low: tuning.workableFrom,
      high: tuning.idealFrom,
    },
    {
      from: "var(--color-ember)",
      to: "var(--color-ember-bright)",
      low: tuning.idealFrom,
      high: tuning.burningFrom,
    },
    whiteHot,
  ];

  const segment = segments.find((candidate) => temperature <= candidate.high) ?? whiteHot;
  const span = segment.high - segment.low;
  const progress = span <= 0 ? 1 : (temperature - segment.low) / span;

  return {
    from: segment.from,
    to: segment.to,
    mix: Math.round(clamp(progress, 0, 1) * 100),
  };
}

/** How far along the gauge a temperature sits, as a percentage. */
export function scalePosition(temperature: number, tuning: ForgeTuning): number {
  return clamp((temperature / tuning.maxTemperature) * 100, 0, 100);
}

/** 0 to 1: how much the forge glow has come up. Cold iron does not glow at all. */
export function glowStrength(temperature: number, tuning: ForgeTuning): number {
  const start = tuning.workableFrom * 0.5;
  return clamp((temperature - start) / (tuning.maxTemperature - start), 0, 1);
}

function clamp(value: number, low: number, high: number): number {
  return Math.min(high, Math.max(low, value));
}
