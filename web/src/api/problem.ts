import { z } from "zod";

/**
 * Every API failure arrives as `application/problem+json` carrying a stable `code`, so the
 * UI switches on that rather than matching on prose that is free to change.
 */
const problemSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  status: z.number().optional(),
  code: z.string().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export type FieldErrors = Record<string, string[]>;

export class ApiProblem extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: FieldErrors;

  constructor(status: number, code: string, message: string, fieldErrors: FieldErrors = {}) {
    super(message);
    this.name = "ApiProblem";
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }

  /** The first message recorded against a field, for rendering next to that input. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

/**
 * Turns a failed response into an {@link ApiProblem}. A body that is missing or not
 * problem-shaped still produces one: a network edge or a proxy error page must not surface
 * as an unhandled rejection with no message.
 */
export async function readProblem(response: Response): Promise<ApiProblem> {
  let parsed: z.infer<typeof problemSchema> | undefined;

  try {
    parsed = problemSchema.parse(await response.json());
  } catch {
    parsed = undefined;
  }

  return new ApiProblem(
    response.status,
    parsed?.code ?? "unknown",
    parsed?.detail ?? parsed?.title ?? "Something went wrong. Try again.",
    parsed?.errors ?? {},
  );
}
