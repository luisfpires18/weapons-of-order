import js from "@eslint/js";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import globals from "globals";
import tseslint from "typescript-eslint";

// Type correctness is `tsc --noEmit`'s job, so the TypeScript rules here are the
// non-type-checked set: lint stays fast and does not duplicate the typecheck step.
export default tseslint.config(
  { ignores: ["dist", "dev-dist", "node_modules"] },
  js.configs.recommended,
  tseslint.configs.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    languageOptions: { globals: globals.browser },
    plugins: { "react-refresh": reactRefresh },
    rules: {
      "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
    },
  },
  // `configs.recommended` is still the legacy eslintrc shape in v7; the flat variant
  // is the one ESLint 10 accepts.
  reactHooks.configs.flat.recommended,
  {
    files: ["*.config.{js,ts}"],
    languageOptions: { globals: globals.node },
  },
);
