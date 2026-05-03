import Link from "next/link";
import { SITE_CONFIG, formatHeaderDate } from "@/lib/site-config";

export default function SiteHeader() {
  const logoWords = SITE_CONFIG.logoWords;

  return (
    <header className="border-b border-border bg-background">
      <div className="mx-auto max-w-7xl px-6">
        {/* Top bar — date + tagline (desktop only) */}
        <div className="hidden items-center justify-between py-3 text-[11px] uppercase tracking-[0.18em] text-muted-foreground md:flex">
          <span>{formatHeaderDate()}</span>
          <span className="text-signal">•</span>
          <span>Know your car&apos;s history</span>
        </div>

        {/* Logo + nav */}
        <div className="flex items-center justify-between gap-6 border-t border-border py-5 md:py-7">
          <Link href="/" aria-label={SITE_CONFIG.name}>
            <span className="block font-display text-2xl font-black leading-none tracking-tight md:text-4xl">
              {logoWords[0]}
              <span className="text-signal">.</span>
              {logoWords[1]}
              <span className="text-signal">.</span>
              {logoWords[2]}
            </span>
          </Link>

          <nav className="flex items-center gap-6 text-sm font-medium md:gap-10">
            {SITE_CONFIG.nav.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                className="underline-grow text-foreground hover:text-signal transition-colors"
              >
                {item.label}
              </Link>
            ))}
          </nav>
        </div>
      </div>
    </header>
  );
}
