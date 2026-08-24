import { useCallback, useEffect, useState } from "react";
import type { BattleResult } from "@/battle/api";
import { BattleStage } from "@/battle/BattleStage";
import type { BattleFrame, CombatantFrame } from "@/battle/playback";
import { OUTCOME_LABELS, REASON_LABELS, durationLabel, frameAt } from "@/battle/playback";
import { EmberRule } from "@/components/EmberRule";
import { SectionLabel } from "@/preparation/ScreenStates";

const CONTROL =
  "min-h-11 cursor-pointer border-[length:var(--border-panel)] px-4 font-hud text-[0.8125rem]" +
  " font-semibold uppercase tracking-[0.12em] transition-colors motion-reduce:transition-none" +
  " focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ember-bright";

/** Playback rates. Enough to skip a long stalemate without turning this into an editor. */
const SPEEDS = [1, 2, 4] as const;

/**
 * Watching a battle that has already happened.
 *
 * The whole fight arrived in one answer, and this reveals it against a clock. Nothing here decides
 * anything: pausing, restarting and running at four times speed all read the same log, and the
 * result was true before the first frame was drawn.
 *
 * The board is drawn by PixiJS and everything else on the screen is ordinary markup — the roster,
 * the controls, the result. That is deliberate: a battle stays readable if the canvas never comes
 * up, and it stays readable to somebody who is not looking at it.
 */
export function BattlePlayback({ result, onLeave }: { result: BattleResult; onLeave: () => void }) {
  const [playing, setPlaying] = useState(true);
  const [speed, setSpeed] = useState<number>(1);
  const [restartToken, setRestartToken] = useState(0);
  const [finished, setFinished] = useState(false);

  // The stage owns the clock and reports about ten times a second. Holding the frame here is what
  // lets the roster below the board show live HP without React re-rendering at sixty frames.
  const [frame, setFrame] = useState<BattleFrame>(() => frameAt(result, 0));

  // Begin battle sits below the deployment panels, so pressing it usually leaves the page scrolled
  // past where the battlefield is about to appear. The first thing a player should see of a battle
  // is the start of it.
  useEffect(() => {
    window.scrollTo({ top: 0, behavior: "smooth" });
  }, [result]);

  const onFinished = useCallback(() => {
    setFinished(true);
    setPlaying(false);
  }, []);

  const restart = () => {
    setRestartToken((token) => token + 1);
    setFinished(false);
    setPlaying(true);
  };

  return (
    <div className="flex flex-col gap-6">
      <BattleStage
        result={result}
        playing={playing}
        speed={speed}
        restartToken={restartToken}
        onFrame={setFrame}
        onFinished={onFinished}
      />

      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        <button
          type="button"
          onClick={() => setPlaying((running) => !running)}
          disabled={finished}
          className={`${CONTROL} border-ember/70 bg-transparent text-ember-bright hover:bg-ember hover:text-void disabled:cursor-not-allowed disabled:border-slate/50 disabled:text-bone-dim/50 disabled:hover:bg-transparent disabled:hover:text-bone-dim/50`}
        >
          {playing ? "Pause" : "Play"}
        </button>

        <button
          type="button"
          onClick={restart}
          className={`${CONTROL} border-slate bg-transparent text-bone-dim hover:border-bone-dim hover:text-bone`}
        >
          Replay
        </button>

        <div role="group" aria-label="Playback speed" className="flex gap-2">
          {SPEEDS.map((rate) => (
            <button
              key={rate}
              type="button"
              aria-pressed={speed === rate}
              onClick={() => setSpeed(rate)}
              className={`${CONTROL} ${
                speed === rate
                  ? "border-ember bg-ember/25 text-bone"
                  : "border-slate bg-transparent text-bone-dim hover:border-bone-dim hover:text-bone"
              }`}
            >
              {rate}&times;
            </button>
          ))}
        </div>

        {/* Clamped to the battle's own length. Playback runs on a little past the last event so the
            last body can fade, and a clock reading 7.1s of a 6.5s battle is just wrong. */}
        <p className="ml-auto font-hud text-hud uppercase tracking-[0.12em] text-bone-dim tabular-nums">
          {durationLabel(Math.min(frame.time, result.durationMilliseconds))} /{" "}
          {durationLabel(result.durationMilliseconds)}
        </p>
      </div>

      {finished ? <Result result={result} onLeave={onLeave} /> : null}

      <Roster frame={frame} />

      {!finished ? (
        <button
          type="button"
          onClick={onLeave}
          className="cursor-pointer self-start font-hud text-hud font-semibold uppercase tracking-[0.1em] text-bone-dim transition-colors hover:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected motion-reduce:transition-none"
        >
          Back to deployment
        </button>
      ) : null}
    </div>
  );
}

/**
 * How it ended, and why.
 *
 * A guard expiry is reported as what it is rather than dressed up as a result. A player who fought
 * to a standstill should be told that is what happened, and told that their survivors survived.
 */
function Result({ result, onLeave }: { result: BattleResult; onLeave: () => void }) {
  const mine = result.combatants.filter((combatant) => combatant.side === "player");
  const standing = mine.filter((combatant) => combatant.endState !== "dead");

  return (
    <section
      role="status"
      aria-label="Battle result"
      className="flex flex-col gap-4 border-[length:var(--border-panel)] border-slate bg-ink-raised/50 p-5"
    >
      <div className="flex flex-col gap-3">
        <h2
          className={`font-display text-[1.5rem] font-semibold uppercase tracking-[0.12em] lg:text-[1.75rem] ${
            result.outcome === "playervictory"
              ? "text-ember-bright"
              : result.outcome === "draw"
                ? "text-bone"
                : "text-bone-dim"
          }`}
        >
          {OUTCOME_LABELS[result.outcome]}
        </h2>
        <EmberRule />
      </div>

      <p className="max-w-[40rem] font-body text-[1rem] leading-relaxed text-bone-dim">
        {REASON_LABELS[result.reason]} It took {durationLabel(result.durationMilliseconds)}.
      </p>

      <p className="font-hud text-hud uppercase tracking-[0.12em] text-bone-dim tabular-nums">
        {standing.length} of {mine.length} of your units still standing
      </p>

      <button
        type="button"
        onClick={onLeave}
        className={`${CONTROL} self-start border-ember/70 bg-transparent text-ember-bright hover:bg-ember hover:text-void`}
      >
        Back to deployment
      </button>
    </section>
  );
}

/**
 * Both armies as text, beside the board rather than instead of it.
 *
 * The same numbers the canvas is drawing, so a battle is followable without looking at the canvas
 * — and so the parts of it that matter can be asserted on without one.
 */
function Roster({ frame }: { frame: BattleFrame }) {
  return (
    <div className="grid gap-6 sm:grid-cols-2">
      <Side label="Your army" side="player" frame={frame} />
      <Side label="Opposition" side="opponent" frame={frame} />
    </div>
  );
}

function Side({
  label,
  side,
  frame,
}: {
  label: string;
  side: "player" | "opponent";
  frame: BattleFrame;
}) {
  const combatants = frame.combatants.filter((combatant) => combatant.side === side);

  return (
    <section className="flex min-w-0 flex-col gap-3">
      <SectionLabel>{label}</SectionLabel>

      <ul aria-label={label} className="flex flex-col border-t border-slate/60">
        {combatants.map((combatant) => (
          <li
            key={combatant.id}
            className="flex items-baseline gap-3 border-b border-slate/60 py-2 font-body text-body"
          >
            <span
              className={`min-w-0 flex-1 truncate ${
                combatant.state === "dead" ? "text-bone-dim/60 line-through" : "text-bone"
              }`}
            >
              {combatant.name}
            </span>

            <span className="shrink-0 font-hud text-hud uppercase tracking-[0.1em] text-bone-dim tabular-nums">
              {describe(combatant)}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function describe(combatant: CombatantFrame): string {
  if (combatant.state === "dead") {
    return "fallen";
  }

  if (combatant.state === "waiting") {
    return combatant.reserveOrder === null ? "waiting" : `reserve ${combatant.reserveOrder + 1}`;
  }

  return `${combatant.hp}/${combatant.maxHp}`;
}
