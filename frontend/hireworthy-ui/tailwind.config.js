import preset from "@plenipo/ui/tailwind-preset";

// The platform's preset carries the shell's design tokens. Inheriting it is what makes a custom
// tab look like part of the product rather than bolted beside it. Tailwind v3, not v4 — the
// platform pins v3 and mixing majors breaks the preset.
export default {
  presets: [preset],
  content: ["./index.html", "./src/**/*.{ts,tsx}", "./node_modules/@plenipo/ui/dist/**/*.js"],
};
