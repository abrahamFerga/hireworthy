import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { apiGet, type ModuleTabProps } from "@plenipo/ui";

/**
 * The Candidate tab — the one screen where this product's central claim becomes visible.
 *
 * A recruiter sees the CV as the candidate wrote it, with the exact spans that evidenced each
 * rubric score highlighted **in place**, beside the score citing them. That is why this tab is
 * custom React and the requisitions tab is not (ARCH.md §5): no declarative table can express a
 * span-anchored overlay on a document.
 *
 * This component does **no** span arithmetic. The server sends `cvSegments` already split and
 * already re-verified against the stored CV text (CvHighlighting), because getting an overlapping
 * range subtly wrong would put a marker over the wrong words — and a misplaced highlight looks
 * exactly like evidence. The browser paints what it is given and decides nothing.
 */

interface Segment {
  text: string;
  criteria: string[];
}

interface Score {
  criterion: string;
  points: number;
  unresolved: boolean;
  citationText: string | null;
  note: string | null;
}

interface Candidate {
  reference: string;
  fullName: string;
  stage: string;
  requisition: string | null;
  screening: { rubricVersion: number; total: number; max: number; unresolved: number } | null;
  scores: Score[];
  cvSegments: Segment[];
}

export function CandidateTab({ tab }: ModuleTabProps) {
  const { applicantReference } = useParams<{ applicantReference: string }>();

  const { data, isLoading, error } = useQuery({
    queryKey: ["hiring", "candidate", applicantReference],
    queryFn: () => apiGet<Candidate>(`/api/hiring/candidates/${applicantReference}`),
    enabled: Boolean(applicantReference),
  });

  if (!applicantReference) {
    return <p className="p-6 text-sm opacity-70">Open a candidate from the pipeline to see them here.</p>;
  }
  if (isLoading) return <p className="p-6 text-sm opacity-70">Loading {applicantReference}…</p>;
  if (error || !data) {
    return <p className="p-6 text-sm text-red-600">Could not load {applicantReference}.</p>;
  }

  return (
    <div className="p-6 space-y-6" aria-label={tab.label}>
      <header className="space-y-1">
        <h1 className="text-xl font-semibold">
          {data.fullName} <span className="opacity-60 font-normal">({data.reference})</span>
        </h1>
        <p className="text-sm opacity-70">
          {data.requisition ?? "no requisition"} · stage {data.stage}
          {data.screening && (
            <>
              {" "}· scored <strong>{data.screening.total}/{data.screening.max}</strong> against
              rubric v{data.screening.rubricVersion}
              {data.screening.unresolved > 0 && (
                <> · <span className="text-amber-700">{data.screening.unresolved} not evidenced</span></>
              )}
            </>
          )}
        </p>
      </header>

      {!data.screening && (
        <p className="text-sm opacity-70">
          Not screened yet — nothing is highlighted because nothing has been cited.
        </p>
      )}

      <div className="grid gap-6 md:grid-cols-[3fr_2fr]">
        {/* The CV, verbatim, with cited spans marked. `whitespace-pre-wrap` is load-bearing:
            citation offsets index the raw text, so collapsing whitespace would make the
            highlights visually disagree with the offsets they came from. */}
        <section>
          <h2 className="text-sm font-medium mb-2 opacity-70">CV</h2>
          <div className="rounded border p-4 text-sm leading-relaxed whitespace-pre-wrap font-mono">
            {data.cvSegments.map((seg, i) =>
              seg.criteria.length > 0 ? (
                <mark
                  key={i}
                  className="bg-amber-200/70 rounded px-0.5"
                  title={`Evidence for: ${seg.criteria.join(", ")}`}
                >
                  {seg.text}
                </mark>
              ) : (
                <span key={i}>{seg.text}</span>
              ),
            )}
          </div>
        </section>

        <section>
          <h2 className="text-sm font-medium mb-2 opacity-70">Scores</h2>
          <ul className="space-y-3">
            {/* Keyed on position as well as name. The server now refuses a score set that repeats
                a criterion (ADR-0013), so new rows cannot collide — but rows written before that
                check can, and #53 tracks repairing them. A duplicate key made React drop or
                duplicate rendered rows, so the screen could disagree with the stored rows at
                exactly the moment somebody is reading a candidate's score. */}
            {data.scores.map((s, i) => (
              <li key={`${i}-${s.criterion}`} className="rounded border p-3 text-sm">
                <div className="flex items-baseline justify-between gap-2">
                  <span className="font-medium">{s.criterion}</span>
                  <span className={s.unresolved ? "text-amber-700" : "opacity-70"}>
                    {s.unresolved ? "not evidenced" : `${s.points}/5`}
                  </span>
                </div>
                {/* The quotation, shown as a quotation. If it is not here, the score has no
                    evidence — which the server already refuses to store, so this is belt. */}
                {s.citationText && (
                  <blockquote className="mt-2 border-l-2 pl-2 italic opacity-80">
                    “{s.citationText}”
                  </blockquote>
                )}
                {s.note && <p className="mt-1 opacity-70">{s.note}</p>}
              </li>
            ))}
          </ul>
          {data.scores.length === 0 && <p className="text-sm opacity-70">No scores yet.</p>}
        </section>
      </div>
    </div>
  );
}
