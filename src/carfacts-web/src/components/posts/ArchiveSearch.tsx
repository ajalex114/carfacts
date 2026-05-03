"use client";
import { useState, useMemo } from "react";
import type { Post } from "@/lib/types";
import ArchiveCard from "./ArchiveCard";

interface Props {
  posts: Post[];
}

const DAYS_SHORT = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
const MONTHS_LONG = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

function buildCalendarWeeks(year: number, month: number): (number | null)[][] {
  const firstDow = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const weeks: (number | null)[][] = [];
  let week: (number | null)[] = Array(firstDow).fill(null);
  for (let d = 1; d <= daysInMonth; d++) {
    week.push(d);
    if (week.length === 7) { weeks.push(week); week = []; }
  }
  if (week.length > 0) {
    while (week.length < 7) week.push(null);
    weeks.push(week);
  }
  return weeks;
}

export default function ArchiveSearch({ posts }: Props) {
  const [query, setQuery] = useState("");
  // selectedDate: {month: 0-11, day: 1-31} | null — year-agnostic
  const [selectedDate, setSelectedDate] = useState<{ month: number; day: number } | null>(null);

  // Initialise calendar to the month of the newest post
  const newestPostDate = useMemo(() => {
    if (!posts.length) return new Date();
    const d = new Date(
      [...posts].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt))[0].publishedAt
    );
    return d;
  }, [posts]);

  const [calYear, setCalYear] = useState(() => newestPostDate.getFullYear());
  const [calMonth, setCalMonth] = useState(() => newestPostDate.getMonth());

  // Set of "month-day" keys (0-indexed month) that have at least one post
  const postDayKeys = useMemo(() => {
    const s = new Set<string>();
    posts.forEach((p) => {
      const d = new Date(p.publishedAt);
      s.add(`${d.getMonth()}-${d.getDate()}`);
    });
    return s;
  }, [posts]);

  const calendarWeeks = useMemo(() => buildCalendarWeeks(calYear, calMonth), [calYear, calMonth]);

  function prevMonth() {
    if (calMonth === 0) { setCalYear(y => y - 1); setCalMonth(11); }
    else setCalMonth(m => m - 1);
  }
  function nextMonth() {
    if (calMonth === 11) { setCalYear(y => y + 1); setCalMonth(0); }
    else setCalMonth(m => m + 1);
  }

  function toggleDay(day: number) {
    if (selectedDate?.month === calMonth && selectedDate?.day === day) {
      setSelectedDate(null); // deselect
    } else {
      setSelectedDate({ month: calMonth, day });
    }
  }

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return posts.filter((p) => {
      const matchesText =
        !q ||
        p.title.toLowerCase().includes(q) ||
        (p.subtitle ?? "").toLowerCase().includes(q);

      const matchesDate = !selectedDate || (() => {
        const d = new Date(p.publishedAt);
        return d.getMonth() === selectedDate.month && d.getDate() === selectedDate.day;
      })();

      return matchesText && matchesDate;
    });
  }, [posts, query, selectedDate]);

  const hasFilters = query || selectedDate;

  return (
    <>
      {/* ── Search controls ── */}
      <div className="mt-10 flex flex-col gap-6 lg:flex-row lg:items-start lg:gap-10">

        {/* Left: text search + results */}
        <div className="flex-1 min-w-0">
          <div className="relative">
            <svg
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
              xmlns="http://www.w3.org/2000/svg" width="16" height="16"
              viewBox="0 0 24 24" fill="none" stroke="currentColor"
              strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
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

          {/* Active filter pill */}
          {selectedDate && (
            <div className="mt-3 flex items-center gap-2">
              <span className="inline-flex items-center gap-2 border border-signal px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-signal">
                {MONTHS_LONG[selectedDate.month]} {selectedDate.day}
                <button onClick={() => setSelectedDate(null)} aria-label="Remove date filter" className="hover:text-foreground">✕</button>
              </span>
              {query && (
                <button onClick={() => { setQuery(""); setSelectedDate(null); }} className="text-xs text-muted-foreground underline-grow">
                  Clear all
                </button>
              )}
            </div>
          )}

          {/* Result count */}
          <p className="mt-4 text-xs uppercase tracking-[0.15em] text-muted-foreground">
            {filtered.length === posts.length
              ? `${posts.length} issues`
              : `${filtered.length} of ${posts.length} issues`}
            {hasFilters && (
              <button onClick={() => { setQuery(""); setSelectedDate(null); }}
                className="ml-4 text-signal underline-grow">
                Clear filters
              </button>
            )}
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
        </div>

        {/* Right: calendar picker */}
        <div className="lg:w-72 shrink-0">
          <div className="border border-border bg-background p-4">
            {/* Calendar header */}
            <div className="flex items-center justify-between mb-4">
              <button
                onClick={prevMonth}
                className="p-1 text-muted-foreground hover:text-foreground transition-colors"
                aria-label="Previous month"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"
                  viewBox="0 0 24 24" fill="none" stroke="currentColor"
                  strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="m15 18-6-6 6-6" />
                </svg>
              </button>
              <span className="text-sm font-semibold uppercase tracking-[0.14em]">
                {MONTHS_LONG[calMonth]} {calYear}
              </span>
              <button
                onClick={nextMonth}
                className="p-1 text-muted-foreground hover:text-foreground transition-colors"
                aria-label="Next month"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"
                  viewBox="0 0 24 24" fill="none" stroke="currentColor"
                  strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="m9 18 6-6-6-6" />
                </svg>
              </button>
            </div>

            {/* Day-of-week headers */}
            <div className="grid grid-cols-7 mb-1">
              {DAYS_SHORT.map((d) => (
                <div key={d} className="text-center text-[10px] font-semibold uppercase tracking-widest text-muted-foreground py-1">
                  {d}
                </div>
              ))}
            </div>

            {/* Calendar grid */}
            {calendarWeeks.map((week, wi) => (
              <div key={wi} className="grid grid-cols-7">
                {week.map((day, di) => {
                  if (!day) return <div key={di} />;
                  const hasPost = postDayKeys.has(`${calMonth}-${day}`);
                  const isSelected = selectedDate?.month === calMonth && selectedDate?.day === day;
                  return (
                    <button
                      key={di}
                      onClick={() => hasPost && toggleDay(day)}
                      disabled={!hasPost}
                      className={[
                        "relative mx-auto flex h-8 w-8 items-center justify-center text-sm transition-colors",
                        hasPost
                          ? isSelected
                            ? "bg-signal text-white font-bold"
                            : "font-medium hover:bg-signal/10 hover:text-signal cursor-pointer"
                          : "text-muted-foreground/30 cursor-default",
                      ].join(" ")}
                      aria-label={hasPost ? `Filter by ${MONTHS_LONG[calMonth]} ${day}` : undefined}
                      aria-pressed={isSelected}
                    >
                      {day}
                      {hasPost && !isSelected && (
                        <span className="absolute bottom-0.5 left-1/2 -translate-x-1/2 h-1 w-1 rounded-full bg-signal" />
                      )}
                    </button>
                  );
                })}
              </div>
            ))}

            <p className="mt-3 text-[10px] uppercase tracking-widest text-muted-foreground text-center">
              {postDayKeys.size} days with issues
            </p>
          </div>
        </div>
      </div>
    </>
  );
}
