/**
 * Marquee ticker strip — "Five facts.•Every day.•Forever." scrolling band.
 * Dark background with large display text, matching the Lovable design.
 */
export default function Marquee() {
  // Six copies keeps the band full at any screen width
  const copies = Array(6).fill(null);

  return (
    <section
      className="overflow-hidden border-b border-border bg-foreground py-4 text-background select-none"
      aria-hidden="true"
    >
      <div className="flex animate-[marquee_40s_linear_infinite] gap-12 whitespace-nowrap font-display text-2xl font-bold tracking-tight md:text-3xl">
        {copies.map((_, i) => (
          <span key={i} className="flex items-center gap-12">
            Five facts.<span className="text-signal">●</span>
            Every day.<span className="text-signal">●</span>
            Forever.<span className="text-signal">●</span>
          </span>
        ))}
      </div>
    </section>
  );
}
