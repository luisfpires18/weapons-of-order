import { useEffect, useRef, useState } from "react";
import type { ForgeSession } from "@/forge/api";
import type { ForgeTuning, HeatBandName } from "@/forge/heat";
import {
  BAND_LABELS,
  bandFor,
  glowStrength,
  ironColour,
  projectBurnSeconds,
  projectTemperature,
  scalePosition,
} from "@/forge/heat";

/**
 * What each band asks the player to do next. Direction rather than mood: the band name says
 * what is true, this says what to do about it.
 */
const BAND_DIRECTION: Record<HeatBandName, string> = {
  cold: "Heat the iron",
  workable: "Hotter is better",
  ideal: "Strike now",
  burning: "Take it out of the fire",
};

const BAND_TEXT: Record<HeatBandName, string> = {
  cold: "text-bone-dim",
  workable: "text-ember",
  ideal: "text-ember-bright",
  burning: "text-white-hot",
};

/** How a landed blow is reported back. */
const BLOW_LABELS: Record<HeatBandName, string> = {
  ideal: "Ideal",
  workable: "Workable",
  burning: "Too hot",
  cold: "Too cold",
};

/**
 * A billet, not a bar. The silhouette is a rough sword blank — wide at the tang, tapering to
 * a point — so the thing being heated is legibly a weapon in progress rather than a progress
 * indicator that happens to be orange.
 */
const BILLET = "polygon(0% 6%, 88% 0%, 100% 50%, 88% 100%, 0% 94%)";

/**
 * The workpiece and the scale it is read against.
 *
 * This is the screen's one loud element, and everything around it is kept quiet on purpose.
 * The billet carries the feel — iron going from cold grey through dull red to near-white,
 * with the forge glow coming up behind it — and the rail underneath carries the precision,
 * because colour alone is not something a player can time a blow against. The ideal window
 * is the lit length of that rail, which is the same device the shell uses to mark the
 * current destination.
 */
export function Anvil({
  session,
  tuning,
  onBurnedThrough,
}: {
  session: ForgeSession | null;
  tuning: ForgeTuning;
  onBurnedThrough: () => void;
}) {
  const live = session?.status === "active";
  const { temperature, burnSeconds } = useHeatReadout(session, tuning);

  const band = bandFor(temperature, tuning);
  const colour = ironColour(temperature, tuning);
  const iron = `color-mix(in srgb, ${colour.to} ${colour.mix}%, ${colour.from})`;
  const glow = glowStrength(temperature, tuning);
  const burnRemaining = Math.max(0, tuning.burnGraceSeconds - burnSeconds);

  useBurnWatch(live ? session : null, burnSeconds >= tuning.burnGraceSeconds, onBurnedThrough);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex items-baseline justify-between gap-4">
        <p
          className={`font-display text-[1.5rem] font-semibold uppercase leading-none tracking-[0.14em] lg:text-[2rem] ${BAND_TEXT[band]}`}
        >
          {BAND_LABELS[band]}
        </p>
        <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
          {live ? BAND_DIRECTION[band] : "Cold iron"}
        </p>
      </div>

      <div className="relative isolate py-4">
        {/* The forge light. It is behind the billet and scales with the heat, so the screen
            itself gets warmer as the iron does. */}
        {/* Inset to the billet's own box rather than spread beyond it: the blur is what
            carries the light past the edges, and it does that without the element itself
            being wider than the column, which on a phone is a horizontal scrollbar. */}
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 -z-10 blur-3xl motion-reduce:transition-none"
          style={{
            background: `radial-gradient(60% 100% at 40% 50%, color-mix(in srgb, var(--color-ember) ${Math.round(glow * 60)}%, transparent) 0%, transparent 70%)`,
            transition: "background 120ms linear",
          }}
        />

        <div
          role="meter"
          aria-label="Workpiece temperature"
          aria-valuemin={0}
          aria-valuemax={tuning.maxTemperature}
          aria-valuenow={Math.round(temperature)}
          aria-valuetext={`${BAND_LABELS[band]}`}
          className="h-14 w-full lg:h-20"
          style={{
            clipPath: BILLET,
            background:
              `linear-gradient(180deg,` +
              ` color-mix(in srgb, var(--color-white-hot) ${Math.round(glow * 30)}%, ${iron}) 0%,` +
              ` ${iron} 46%,` +
              ` color-mix(in srgb, var(--color-void) 55%, ${iron}) 100%)`,
          }}
        />
      </div>

      <div>
        <div className="relative h-[2px] w-full bg-slate">
          <div
            aria-hidden
            className="absolute inset-y-0 bg-ember"
            style={{
              left: `${percent(tuning.idealFrom, tuning)}%`,
              width: `${percent(tuning.burningFrom, tuning) - percent(tuning.idealFrom, tuning)}%`,
              boxShadow: "0 0 10px color-mix(in srgb, var(--color-ember) 55%, transparent)",
            }}
          />
          <div
            aria-hidden
            className="absolute inset-y-0 right-0 bg-white-hot/50"
            style={{ left: `${percent(tuning.burningFrom, tuning)}%` }}
          />

          {/* Where the iron is now. A hairline rather than a handle: nothing here is dragged. */}
          <div
            aria-hidden
            className="absolute -top-[0.6rem] h-[1.45rem] w-[2px] bg-bone"
            style={{ left: `${scalePosition(temperature, tuning)}%` }}
          />
        </div>

        <div aria-hidden className="relative mt-2 h-4">
          {(
            [
              ["cold", 0],
              ["workable", tuning.workableFrom],
              ["ideal", tuning.idealFrom],
              ["burning", tuning.burningFrom],
            ] as const
          ).map(([name, at], index, all) => (
            <span
              key={name}
              className={`absolute top-0 font-hud text-[0.5rem] font-semibold uppercase leading-none tracking-[0.08em] sm:text-[0.625rem] sm:tracking-[0.12em] ${
                name === "ideal" ? "text-ember" : "text-bone-dim/70"
              }`}
              // Each label sits at the boundary its band begins on, except the last, which is
              // pinned to the end of the scale. Left-anchoring that one puts its tail past
              // the right edge of a phone and gives the page a horizontal scrollbar.
              style={
                index === all.length - 1
                  ? { right: 0, paddingRight: "0.125rem" }
                  : { left: `${percent(at, tuning)}%`, paddingLeft: "0.25rem" }
              }
            >
              {BAND_LABELS[name]}
            </span>
          ))}
        </div>
      </div>

      {live && burnSeconds > 0.15 ? (
        <p className="flex items-center gap-3 font-hud text-hud font-semibold uppercase tracking-[0.16em] text-white-hot">
          <span
            aria-hidden
            className="h-[2px] w-16 shrink-0 bg-slate"
            style={{
              background: `linear-gradient(to right, var(--color-white-hot) ${Math.round(
                (burnRemaining / tuning.burnGraceSeconds) * 100,
              )}%, var(--color-slate) 0%)`,
            }}
          />
          Burning through
        </p>
      ) : null}
    </div>
  );
}

/**
 * The blows that have landed, and the ones still to come.
 */
export function BlowRow({
  strikes,
  required,
}: {
  strikes: readonly { ordinal: number; band: HeatBandName }[];
  required: number;
}) {
  const latest = strikes.at(-1);

  return (
    <div className="flex flex-col gap-3">
      <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
        Blows
      </p>

      <ol aria-label="Blows" className="flex gap-3">
        {Array.from({ length: required }, (_, index) => {
          const blow = strikes[index];

          return (
            <li key={index} className="flex min-w-0 flex-1 flex-col gap-2">
              <span
                aria-hidden
                className={`h-[3px] w-full ${blow ? BLOW_MARK[blow.band] : "bg-slate"}`}
              />
              <span
                className={`truncate font-hud text-[0.625rem] font-semibold uppercase tracking-[0.1em] ${
                  blow ? BAND_TEXT[blow.band] : "text-bone-dim/60"
                }`}
              >
                {blow ? BLOW_LABELS[blow.band] : "—"}
              </span>
            </li>
          );
        })}
      </ol>

      {/* Announced rather than only drawn, so the result of a blow reaches a screen reader
          at the moment it lands. */}
      <p role="status" className="sr-only">
        {latest ? `${BLOW_LABELS[latest.band]} strike, blow ${latest.ordinal} of ${required}` : ""}
      </p>
    </div>
  );
}

const BLOW_MARK: Record<HeatBandName, string> = {
  cold: "bg-bone-dim/50",
  workable: "bg-ember",
  ideal: "bg-ember-bright",
  burning: "bg-white-hot",
};

function percent(temperature: number, tuning: ForgeTuning): number {
  return (temperature / tuning.maxTemperature) * 100;
}

/**
 * Runs the published temperature model forward between server responses.
 *
 * Anchored on the browser's own monotonic clock rather than on the server's timestamp: the
 * two clocks do not have to agree, and only the elapsed time since the response matters. The
 * loop stops whenever nothing can change — cold iron out of the fire — so a settled forge is
 * not repainting sixty times a second for no reason.
 */
function useHeatReadout(session: ForgeSession | null, tuning: ForgeTuning) {
  const temperature = session?.temperature ?? 0;
  const burnSeconds = session?.burnSeconds ?? 0;
  const heating = session?.heating ?? false;
  const live = session?.status === "active";

  // Cold iron out of the fire is not going anywhere, so there is nothing to animate.
  const moving = live && (heating || temperature > 0);

  // What the frames below are counting from. A new answer is a new anchor, and the elapsed
  // time from the previous one has to be discarded with it — otherwise the first frame after
  // a blow would draw the old heat against the new temperature.
  const anchor = `${session?.observedAt ?? ""}|${temperature}|${heating}`;
  const [ticked, setTicked] = useState({ anchor, seconds: 0 });

  useEffect(() => {
    if (!moving) return;

    const from = performance.now();
    let frame = 0;

    const tick = () => {
      setTicked({ anchor, seconds: (performance.now() - from) / 1000 });
      frame = requestAnimationFrame(tick);
    };

    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [moving, anchor]);

  const seconds = ticked.anchor === anchor ? ticked.seconds : 0;

  return {
    temperature: projectTemperature(temperature, heating, seconds, tuning),
    burnSeconds: projectBurnSeconds(burnSeconds, temperature, heating, seconds, tuning),
  };
}

/**
 * Tells the screen once, per workpiece, that the projection has passed the ruin threshold.
 *
 * The server already knows — the ruin follows from what it stored — but it has no reason to
 * say so until someone asks. This is what makes it ask, so a player watching the iron burn
 * sees it die rather than finding out on their next press.
 */
function useBurnWatch(session: ForgeSession | null, burnedThrough: boolean, notify: () => void) {
  const reported = useRef<string | null>(null);

  useEffect(() => {
    if (!session || !burnedThrough || reported.current === session.id) return;

    reported.current = session.id;
    notify();
  }, [session, burnedThrough, notify]);
}
