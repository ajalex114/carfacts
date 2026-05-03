"use client";
import { useState, useMemo } from "react";
import type { Post } from "@/lib/types";
import ArchiveCard from "./ArchiveCard";

interface Props {
  posts: Post[];
}

interface MonthOption {
  label: string;   // "May 2026"
  value: string;   // "2026-05"
}

export default function ArchiveSearch({ posts }: Props) {
  const [query, setQuery] = useState("");
  const [selectedMonth, setSelectedMonth] = useState("");

  // Build unique sorted month options from post dates (newest first)
  const monthOptions = useMemo<MonthOption[]>(() => {
    const seen = new Set<string>();
    const opts: MonthOption[] = [];
    [...posts]
      .sort((a, b) => b.publishedAt.localeCompare(a.publishedAt))
      .forEach((p) => {
        const d = new Date(p.publishedAt);
        const value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
        if (!seen.has(value)) {
          seen.add(value);
          opts.push({
            value,
            label: d.toLocaleDateString("en-US", { month: "long", year: "numeric" }),
          });
        }
      });
    return opts;
  }, [posts]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return posts.filter((p) => {
      const matchesText =
        !q ||
        p.title.toLowerCase().includes(q) ||
        (p.subtitle ?? "").toLowerCase().includes(q);

      const matchesMonth = !selectedMonth || (() => {
        const d = new Date(p.publishedAt);
        const val = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
        return val === selectedMonth;
      })();

      return matchesText && matchesMonth;
    });
  }, [posts, query, selectedMonth]);

  return (
    <>
      {/* Search controls */}
      <div className="mt-10 flex flex-col gap-4 sm:flex-row sm:items-center sm:gap-6">
        {/* Text search */}
        <div className="relative flex-1">
          <svg
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
            xmlns="http://www.w3.org/2000/svg"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.3-4.3" />
          </svg>
          <input
            type="search"
            placeholder="Search by title or topic…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            className="w-full rounded-none border border-border bg-background py-2.5 pl-9 pr-4 text-sm placeholder:text-muted-foreground focus:border-foreground focus:outline-none focus:ring-0"
          />
        </div>

        {/* Month filter */}
        <div className="relative sm:w-56">
          <select
            value={selectedMonth}
            onChange={(e) => setSelectedMonth(e.target.value)}
            className="w-full appearance-none rounded-none border border-border bg-background px-4 py-2.5 pr-8 text-sm focus:border-foreground focus:outline-none"
          >
            <option value="">All months</option>
            {monthOptions.map((m) => (
              <option key={m.value} value={m.value}>
                {m.label}
              </option>
            ))}
          </select>
          <svg
            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground"
            xmlns="http://www.w3.org/2000/svg"
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="m6 9 6 6 6-6" />
          </svg>
        </div>

        {/* Clear filters */}
        {(query || selectedMonth) && (
          <button
            onClick={() => { setQuery(""); setSelectedMonth(""); }}
            className="text-sm text-signal underline-grow self-start sm:self-auto"
          >
            Clear
          </button>
        )}
      </div>

      {/* Result count */}
      <p className="mt-4 text-xs uppercase tracking-[0.15em] text-muted-foreground">
        {filtered.length === posts.length
          ? `${posts.length} issues`
          : `${filtered.length} of ${posts.length} issues`}
      </p>

      {/* Results */}
      <div className="mt-2">
        {filtered.length === 0 ? (
          <p className="py-16 text-center text-muted-foreground">
            No issues match your search.
          </p>
        ) : (
          filtered.map((post) => <ArchiveCard key={post.id} post={post} />)
        )}
      </div>
    </>
  );
}
