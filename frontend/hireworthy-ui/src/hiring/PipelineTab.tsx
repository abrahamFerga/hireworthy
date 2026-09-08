import { useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { apiGet, type ModuleTabProps } from "@plenipo/ui";

/**
 * The Pipeline board — stage columns with draggable candidate cards.
 *
 * **Dragging a card proposes a move. It does not make one.** That is the design, not a stub, and
 * the reason is worth stating where the next person will read it:
 *
 * Advancing and rejecting are this product's consequential acts, and both go through
 * `advance_candidates` / `reject_candidate`, which are approval-gated *and* permission-checked by
 * the agent runner before the model is ever offered them. A board endpoint that wrote the stage
 * would skip both. Worse, the obvious "safe" alternative — have the drop queue a PendingApproval
 * for `advance_candidates` — is a privilege escalation: the platform's ApprovalExecutor re-invokes
 * an approved tool without re-checking that tool's permission, and `hiring-recruiter` deliberately
 * holds ManageApprovals while *not* holding `tools.hiring.advance_candidates`. So the drop composes
 * the request, and a human makes it through the assistant, where the role model still applies.
 *
 * The server is the only source of stage truth. Proposals live in component state and vanish on
 * refetch; nothing here optimistically rewrites the board.
 */

interface Screening {
  total: number;
  max: number;
  unresolved: number;
}

interface Card {
  reference: string;
  fullName: string;
  screening: Screening | null;
}

interface Column {
  stage: string;
  terminal: boolean;
  candidates: Card[];
}

interface Board {
  requisitions: { reference: string; title: string }[];
  requisition: string | null;
  title: string | null;
  columns: Column[];
}

/** A move the user has dragged but nobody has approved. */
interface Proposal {
  reference: string;
  fullName: string;
  from: string;
  to: string;
}

/** The phrasing that reaches the approval-gated tool, in the assistant. */
function requestFor(p: Proposal): string {
  return p.to === "Rejected"
    ? `Reject ${p.reference} — give the reason against the approved rubric.`
    : `Advance ${p.reference} to ${p.to}.`;
}

export function PipelineTab({ tab }: ModuleTabProps) {
  const [requisition, setRequisition] = useState<string | null>(null);
  const [proposals, setProposals] = useState<Proposal[]>([]);
  // A ref, not state: the drag source is read inside onDrop, and state read there would come from
  // the closure of the render that installed the handler. A real pointer drag always re-renders in
  // between so state happens to work — but "happens to work because the user is slow" is not a
  // property worth depending on, and it fails silently when it fails (the drop just does nothing).
  const dragging = useRef<{ reference: string; from: string } | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["hiring", "pipeline", requisition],
    queryFn: () =>
      apiGet<Board>(`/api/hiring/pipeline${requisition ? `?requisition=${encodeURIComponent(requisition)}` : ""}`),
  });

  // Keyed by reference so dragging the same person twice replaces the earlier proposal rather
  // than stacking two contradictory ones.
  const proposedFor = useMemo(
    () => new Map(proposals.map((p) => [p.reference, p])),
    [proposals],
  );

  if (isLoading) return <p className="p-6 text-sm opacity-70">Loading the pipeline…</p>;
  if (error || !data) return <p className="p-6 text-sm text-red-600">Could not load the pipeline.</p>;
  if (!data.requisition) {
    return <p className="p-6 text-sm opacity-70">No requisitions yet — there is no pipeline to show.</p>;
  }

  function onDrop(to: string) {
    const source = dragging.current;
    dragging.current = null;

    if (!source || source.from === to) return;

    const card = data!.columns.flatMap((c) => c.candidates).find((c) => c.reference === source.reference);
    if (!card) return;

    setProposals((prev) => [
      ...prev.filter((p) => p.reference !== source.reference),
      { reference: card.reference, fullName: card.fullName, from: source.from, to },
    ]);
  }

  return (
    <div className="p-6 space-y-4" aria-label={tab.label}>
      <header className="flex flex-wrap items-baseline justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Pipeline</h1>
          <p className="text-sm opacity-70">{data.title}</p>
        </div>
        <label className="text-sm">
          <span className="sr-only">Requisition</span>
          <select
            className="rounded border px-2 py-1 text-sm"
            value={data.requisition}
            onChange={(e) => {
              setRequisition(e.target.value);
              setProposals([]);
            }}
          >
            {data.requisitions.map((r) => (
              <option key={r.reference} value={r.reference}>
                {r.reference} — {r.title}
              </option>
            ))}
          </select>
        </label>
      </header>

      {/* Said once, plainly, above the board rather than buried in a tooltip. A recruiter who
          believes a drag moved someone has been misled by the interface. */}
      <p className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
        Dragging a card <strong>proposes</strong> a move. Nobody is advanced or rejected until a
        named human approves it — ask the assistant, and the approval gate does the rest.
      </p>

      <div className="flex gap-3 overflow-x-auto pb-2">
        {data.columns.map((col) => (
          <section
            key={col.stage}
            onDragOver={(e) => e.preventDefault()}
            onDrop={() => onDrop(col.stage)}
            className={`min-w-56 flex-1 rounded border p-3 ${col.terminal ? "bg-black/5" : ""}`}
            aria-label={`${col.stage} — ${col.candidates.length} candidate(s)`}
          >
            <h2 className="mb-2 flex items-baseline justify-between text-sm font-medium">
              <span>{col.stage}</span>
              <span className="opacity-60">{col.candidates.length}</span>
            </h2>

            <ul className="space-y-2">
              {col.candidates.map((c) => {
                const proposal = proposedFor.get(c.reference);
                return (
                  <li
                    key={c.reference}
                    draggable
                    onDragStart={() => { dragging.current = { reference: c.reference, from: col.stage }; }}
                    className="cursor-grab rounded border bg-white/60 p-2 text-sm active:cursor-grabbing dark:bg-white/5"
                  >
                    <div className="font-medium">{c.fullName}</div>
                    <div className="opacity-60">{c.reference}</div>
                    {c.screening ? (
                      <div className="mt-1 opacity-70">
                        {c.screening.total}/{c.screening.max}
                        {c.screening.unresolved > 0 && (
                          <span className="text-amber-700"> · {c.screening.unresolved} not evidenced</span>
                        )}
                      </div>
                    ) : (
                      <div className="mt-1 opacity-50">not screened</div>
                    )}
                    {proposal && (
                      <div className="mt-1 text-amber-800">proposed → {proposal.to}</div>
                    )}
                  </li>
                );
              })}
              {col.candidates.length === 0 && (
                <li className="rounded border border-dashed p-2 text-xs opacity-50">empty</li>
              )}
            </ul>
          </section>
        ))}
      </div>

      {proposals.length > 0 && (
        <section className="rounded border p-4">
          <div className="flex items-baseline justify-between gap-2">
            <h2 className="text-sm font-medium">
              {proposals.length} proposed move{proposals.length === 1 ? "" : "s"} — not yet made
            </h2>
            <button className="text-sm underline opacity-70" onClick={() => setProposals([])}>
              Clear
            </button>
          </div>
          <ul className="mt-3 space-y-2 text-sm">
            {proposals.map((p) => (
              <li key={p.reference} className="rounded border p-2">
                <div>
                  <strong>{p.fullName}</strong> ({p.reference}) · {p.from} → {p.to}
                </div>
                {/* The exact words that reach the gated tool. Shown rather than sent, because
                    sending it from here would route around the runner's permission check. */}
                <code className="mt-1 block rounded bg-black/5 px-2 py-1 text-xs">{requestFor(p)}</code>
              </li>
            ))}
          </ul>
          <p className="mt-3 text-sm opacity-70">
            Ask the assistant for these in the Chat tab. It will park each one on the approval gate
            for a human with the authority to decide it.
          </p>
        </section>
      )}
    </div>
  );
}
