import { useEffect, useRef } from "react";
import type { BattleResult } from "@/battle/api";
import { boardAspectRatio } from "@/battle/hex";
import type { BattleFrame } from "@/battle/playback";
import { frameAt, playbackLength } from "@/battle/playback";
import type { BattleStage as Stage } from "@/battle/stage/renderer";

/**
 * The battlefield surface, and the clock that drives it.
 *
 * React hosts the element and owns everything around it; PixiJS owns what is inside it. The clock
 * lives here rather than in React state because a battle is drawn sixty times a second and a
 * component tree is not something to re-render at that rate — the parent hears from
 * {@link BattleStageProps.onFrame} about ten times a second, which is enough for a readout and
 * for the roster beneath the board.
 *
 * The renderer is loaded on demand and its absence is a state, not a failure: a browser without
 * acceleration still gets the controls, the roster and the result, because none of those depend
 * on the canvas.
 */
export type BattleStageProps = {
  result: BattleResult;
  playing: boolean;
  /** Playback rate. 1 is the battle's own pace on the simulated clock. */
  speed: number;
  /** Changing this rewinds to the beginning. */
  restartToken: number;
  onFrame: (frame: BattleFrame) => void;
  /** Called once when playback reaches the end of the log. */
  onFinished: () => void;
};

/** How often the surrounding interface hears about the frame, in milliseconds. */
const REPORT_INTERVAL = 100;

export function BattleStage({
  result,
  playing,
  speed,
  restartToken,
  onFrame,
  onFinished,
}: BattleStageProps) {
  const hostRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<Stage | null>(null);
  const timeRef = useRef(0);

  // Read inside the animation frame rather than captured by it, so changing the speed or pressing
  // pause does not mean tearing down and rebuilding the loop.
  const playingRef = useRef(playing);
  const speedRef = useRef(speed);
  const callbacks = useRef({ onFrame, onFinished });

  // After every render, not during one. The animation loop reads these on its own schedule, so
  // what it needs is the latest value rather than the one the loop was created with.
  useEffect(() => {
    playingRef.current = playing;
    speedRef.current = speed;
    callbacks.current = { onFrame, onFinished };
  });

  const length = playbackLength(result);

  // A new battle, or a restart, starts from the beginning.
  useEffect(() => {
    timeRef.current = 0;
  }, [restartToken, result]);

  useEffect(() => {
    const host = hostRef.current;

    if (host === null) {
      return;
    }

    let disposed = false;
    let animation = 0;
    let previous = performance.now();
    let reportedAt = -REPORT_INTERVAL;
    let finished = false;

    const draw = (now: number) => {
      animation = requestAnimationFrame(draw);

      const elapsed = now - previous;
      previous = now;

      if (playingRef.current && timeRef.current < length) {
        timeRef.current = Math.min(length, timeRef.current + elapsed * speedRef.current);
      }

      const time = timeRef.current;
      const frame = frameAt(result, time);

      stageRef.current?.render(frame);

      if (time - reportedAt >= REPORT_INTERVAL || time >= length) {
        reportedAt = time;
        callbacks.current.onFrame(frame);
      }

      if (!finished && time >= length) {
        finished = true;
        callbacks.current.onFinished();
      }
    };

    const observer = new ResizeObserver(() => {
      stageRef.current?.resize(host.clientWidth, host.clientHeight);
    });

    void (async () => {
      const stage = await startStage(host, result.battlefield);

      if (disposed) {
        stage?.destroy();
        return;
      }

      stageRef.current = stage;
      observer.observe(host);
      animation = requestAnimationFrame(draw);
    })();

    return () => {
      disposed = true;
      cancelAnimationFrame(animation);
      observer.disconnect();
      stageRef.current?.destroy();
      stageRef.current = null;
    };
  }, [result, length, restartToken]);

  return (
    <div
      ref={hostRef}
      // Not focusable and carrying no information of its own: everything the canvas shows is also
      // in the roster and the result beneath it, so a reader loses nothing by skipping it.
      aria-hidden
      // The board keeps its shape whatever it is given, so a surface much wider than the board is
      // just dead margin. Capping the width to what the height can fill keeps the panel reading as
      // a battlefield rather than as a wide dark box with a small board inside it.
      className="relative mx-auto h-[min(58vh,26rem)] w-full overflow-hidden border-[length:var(--border-panel)] border-slate bg-void lg:h-[min(64vh,34rem)]"
      style={{ maxWidth: `calc(min(64vh, 34rem) * ${boardAspectRatio(result.battlefield)})` }}
    />
  );
}

/**
 * Brings the renderer up, or reports that it cannot come up.
 *
 * The capability is checked before the import rather than after, because a renderer that fails
 * during initialisation is slower and noisier to recover from than one that was never asked.
 */
async function startStage(
  host: HTMLElement,
  field: BattleResult["battlefield"],
): Promise<Stage | null> {
  if (!hasWebGl()) {
    return null;
  }

  try {
    const { createBattleStage } = await import("@/battle/stage/renderer");

    return await createBattleStage(host, field);
  } catch {
    // A browser that cannot draw the battlefield still shows the battle. Nothing else on the
    // screen depends on this having worked.
    return null;
  }
}

function hasWebGl(): boolean {
  try {
    const canvas = document.createElement("canvas");

    return (canvas.getContext("webgl2") ?? canvas.getContext("webgl")) !== null;
  } catch {
    return false;
  }
}
