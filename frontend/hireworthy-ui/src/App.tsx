import { useEffect, useState } from "react";
import { PlenipoApp, defineModule } from "@plenipo/ui";
import { CandidateTab } from "./hiring/CandidateTab";

// Hireworthy's app entry (ADR-0011): the stock Plenipo shell plus the one tab that needs more than
// server-driven rendering — the candidate view, where cited spans are highlighted inside the CV
// they came from. Every other tab stays server-driven, deliberately.
//
// The key is the TAB ID, not the route. `candidate` here must match TabDescriptor.Id in
// HiringModule's manifest; renaming either side silently falls back to GenericTab with no error.
const hiring = defineModule("hiring", {
  tabs: {
    candidate: CandidateTab,
  },
});

// Brand: baked at build for the first paint, superseded at runtime by the host's
// Branding:ProductName — the same contract as the stock shell.
const buildTimeBrand = (import.meta.env.VITE_BRAND_NAME as string | undefined) ?? "Hireworthy";

export default function App() {
  const [brandName, setBrandName] = useState(buildTimeBrand);

  useEffect(() => {
    fetch("/api/platform/branding")
      .then((res) => (res.ok ? (res.json() as Promise<{ name?: string }>) : null))
      .then((body) => {
        if (body?.name) setBrandName(body.name);
      })
      .catch(() => {
        // API not up yet — the baked brand stands.
      });
  }, []);

  useEffect(() => {
    document.title = brandName;
  }, [brandName]);

  return <PlenipoApp moduleUi={[hiring]} branding={{ name: brandName }} />;
}
