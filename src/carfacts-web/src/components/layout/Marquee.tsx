import { SITE_CONFIG } from "@/lib/site-config";

/**
 * Marquee ticker strip — "Five facts.•Every day.•Forever." scrolling band.
 * The items list comes from site-config so changing copy requires zero template edits.
 */
export default function Marquee() {
  // Duplicate 8× so there's always content visible regardless of screen width
  const items = Array(8).fill(SITE_CONFIG.marqueeItems).flat() as string[];

  return (
    <div
      className="overflow-hidden border-y border-border bg-background py-3 select-none"
      aria-hidden="true"
    >
      <div className="animate-marquee inline-flex gap-0">
        {/* Two copies side-by-side so the loop is seamless */}
        {[0, 1].map((copy) => (
          <span key={copy} className="inline-flex">
            {items.map((item, i) => (
              <span
                key={i}
                className="kicker mx-4 text-muted-foreground"
              >
                {item}
                <span className="mx-4 text-signal">•</span>
              </span>
            ))}
          </span>
        ))}
      </div>
    </div>
  );
}
